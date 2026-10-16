// This translation unit is native: Unity calls RenderEvent on its render thread.
#define NOMINMAX
#include <d3d11.h>
#include <wrl/client.h>
#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstdint>
#include <limits>
#include <map>
#include <memory>
#include <mutex>
#include <vector>
#include "CaptureGpuNative.h"
#include "CaptureAlphaBytecode.h"

using Microsoft::WRL::ComPtr;

namespace
{
    enum ShaderFlags : unsigned { Transparent = 1, Premultiplied = 2, SwapRedBlue = 4, HardwareSrgb = 8 };
    struct Settings { unsigned Width, Height, Flags, Padding; };
    static_assert(sizeof(Settings) == 16, "HLSL constant buffer layout");

    struct Pass
    {
        ComPtr<ID3D11Device> Device;
        ComPtr<ID3D11DeviceContext> Context;
        ComPtr<ID3D11CommandList> Commands;
        std::atomic<HRESULT> Status{ S_OK };
    };

    std::mutex registryMutex;
    std::map<int, std::shared_ptr<Pass>> passes;
    int nextEventId = 1;

    void Check(HRESULT result) { if (FAILED(result)) throw result; }
    double Clamp(double value) { return (std::max)(0.0, (std::min)(1.0, value)); }
    std::uint8_t Alpha(double value) { return static_cast<std::uint8_t>(std::ceil(Clamp(value) * 255)); }

    // Generated once per color space, not once per frame. RGBA stores coverage,
    // minimum alpha, straight RGB, and premultiplied RGB respectively.
    using LookupBytes = std::vector<std::uint8_t>;
    LookupBytes BuildLookup(bool linear)
    {
        LookupBytes bytes(256 * 256 * 4);
        double values[256];
        for (int x = 0; x < 256; ++x)
        {
            const double value = x / 255.0;
            values[x] = !linear ? value : value <= 0.04045 ? value / 12.92 : std::pow((value + 0.055) / 1.055, 2.4);
        }
        for (int y = 0; y < 256; ++y)
            for (int x = 0; x < 256; ++x)
            {
                const auto i = (y * 256 + x) * 4;
                bytes[i] = Alpha(1 - (values[y] - values[x]));
                bytes[i + 1] = Alpha(values[x]);
                if (y == 0) continue;
                auto straight = static_cast<std::uint8_t>(x);
                if (y != 255)
                {
                    double value = Clamp(values[x] * (255.0 / y));
                    if (linear) value = value <= 0.0031308 ? value * 12.92 : 1.055 * std::pow(value, 1.0 / 2.4) - 0.055;
                    straight = static_cast<std::uint8_t>(std::floor(Clamp(value) * 255 + 0.5));
                }
                bytes[i + 2] = straight;
                bytes[i + 3] = static_cast<std::uint8_t>((straight * y + 127) / 255);
            }
        return bytes;
    }

    const LookupBytes& Lookup(bool linear)
    {
        if (linear) { static const auto bytes = BuildLookup(true); return bytes; }
        static const auto bytes = BuildLookup(false); return bytes;
    }

    ComPtr<ID3D11Texture2D> Texture(void* pointer, int width, int height)
    {
        if (!pointer) throw E_POINTER;
        ComPtr<ID3D11Texture2D> texture;
        Check(static_cast<IUnknown*>(pointer)->QueryInterface(IID_PPV_ARGS(&texture)));
        D3D11_TEXTURE2D_DESC desc{};
        texture->GetDesc(&desc);
        if (desc.Width != static_cast<unsigned>(width) || desc.Height != static_cast<unsigned>(height) ||
            desc.ArraySize != 1 || desc.MipLevels != 1 || desc.SampleDesc.Count != 1)
            throw E_INVALIDARG;
        return texture;
    }

    ComPtr<ID3D11ShaderResourceView> InputView(ID3D11Device* device, ID3D11Texture2D* texture, bool& hardwareSrgb)
    {
        D3D11_TEXTURE2D_DESC textureDesc{};
        texture->GetDesc(&textureDesc);
        D3D11_SHADER_RESOURCE_VIEW_DESC desc{};
        desc.Format = textureDesc.Format;
        if (desc.Format == DXGI_FORMAT_R8G8B8A8_TYPELESS) desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        if (desc.Format == DXGI_FORMAT_B8G8R8A8_TYPELESS) desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        hardwareSrgb = desc.Format == DXGI_FORMAT_R8G8B8A8_UNORM_SRGB || desc.Format == DXGI_FORMAT_B8G8R8A8_UNORM_SRGB;
        if (!hardwareSrgb && desc.Format != DXGI_FORMAT_R8G8B8A8_UNORM && desc.Format != DXGI_FORMAT_B8G8R8A8_UNORM)
            throw E_INVALIDARG;
        desc.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D;
        desc.Texture2D.MipLevels = 1;
        ComPtr<ID3D11ShaderResourceView> view;
        Check(device->CreateShaderResourceView(texture, &desc, &view));
        return view;
    }

    std::shared_ptr<Pass> CreatePass(void* color, void* black, void* white, void* output, int width, int height,
        bool linear, bool transparent, bool premultiplied, bool swapRedBlue)
    {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096) throw E_INVALIDARG;
        std::array<ComPtr<ID3D11Texture2D>, 4> textures = {
            Texture(color, width, height), Texture(transparent ? black : color, width, height),
            Texture(transparent ? white : color, width, height), Texture(output, width, height)
        };
        auto pass = std::make_shared<Pass>();
        textures[0]->GetDevice(&pass->Device);
        if (pass->Device->GetFeatureLevel() < D3D_FEATURE_LEVEL_11_0 ||
            (pass->Device->GetCreationFlags() & D3D11_CREATE_DEVICE_SINGLETHREADED))
            throw DXGI_ERROR_UNSUPPORTED;
        for (const auto& texture : textures)
        {
            ComPtr<ID3D11Device> device;
            texture->GetDevice(&device);
            if (device.Get() != pass->Device.Get()) throw E_INVALIDARG;
        }
        if (textures[3].Get() == textures[0].Get() || textures[3].Get() == textures[1].Get() ||
            textures[3].Get() == textures[2].Get()) throw E_INVALIDARG;
        pass->Device->GetImmediateContext(&pass->Context);

        std::array<ComPtr<ID3D11ShaderResourceView>, 4> views;
        bool hardwareSrgb = false;
        for (int i = 0; i < 3; ++i)
        {
            bool srgb;
            views[i] = InputView(pass->Device.Get(), textures[i].Get(), srgb);
            if (i != 0 && srgb != hardwareSrgb) throw E_INVALIDARG;
            hardwareSrgb = srgb;
        }
        D3D11_TEXTURE2D_DESC tableDesc{};
        tableDesc.Width = tableDesc.Height = 256;
        tableDesc.MipLevels = tableDesc.ArraySize = tableDesc.SampleDesc.Count = 1;
        tableDesc.Format = DXGI_FORMAT_R8G8B8A8_UINT;
        tableDesc.Usage = D3D11_USAGE_IMMUTABLE;
        tableDesc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA tableData{};
        tableData.pSysMem = Lookup(linear).data();
        tableData.SysMemPitch = 256 * 4;
        ComPtr<ID3D11Texture2D> table;
        Check(pass->Device->CreateTexture2D(&tableDesc, &tableData, &table));
        Check(pass->Device->CreateShaderResourceView(table.Get(), nullptr, &views[3]));

        D3D11_UNORDERED_ACCESS_VIEW_DESC outputDesc{};
        outputDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        outputDesc.ViewDimension = D3D11_UAV_DIMENSION_TEXTURE2D;
        ComPtr<ID3D11UnorderedAccessView> outputView;
        Check(pass->Device->CreateUnorderedAccessView(textures[3].Get(), &outputDesc, &outputView));
        ComPtr<ID3D11ComputeShader> shader;
        Check(pass->Device->CreateComputeShader(CaptureAlphaBytecode, sizeof(CaptureAlphaBytecode), nullptr, &shader));
        Settings settings{ static_cast<unsigned>(width), static_cast<unsigned>(height),
            (transparent ? Transparent : 0u) | (premultiplied ? Premultiplied : 0u) |
            (swapRedBlue ? SwapRedBlue : 0u) | (hardwareSrgb ? HardwareSrgb : 0u), 0 };
        D3D11_BUFFER_DESC constantDesc{};
        constantDesc.ByteWidth = sizeof(settings);
        constantDesc.Usage = D3D11_USAGE_IMMUTABLE;
        constantDesc.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA constantData{};
        constantData.pSysMem = &settings;
        ComPtr<ID3D11Buffer> constants;
        Check(pass->Device->CreateBuffer(&constantDesc, &constantData, &constants));

        // Record once and replay on Unity's render thread. The command list owns
        // its resources and restores Unity's complete context state on execution.
        ComPtr<ID3D11DeviceContext> recorder;
        Check(pass->Device->CreateDeferredContext(0, &recorder));
        recorder->ClearState();
        recorder->CSSetShader(shader.Get(), nullptr, 0);
        ID3D11ShaderResourceView* inputs[] = { views[0].Get(), views[1].Get(), views[2].Get(), views[3].Get() };
        recorder->CSSetShaderResources(0, 4, inputs);
        recorder->CSSetUnorderedAccessViews(0, 1, outputView.GetAddressOf(), nullptr);
        recorder->CSSetConstantBuffers(0, 1, constants.GetAddressOf());
        recorder->Dispatch((width + 7) / 8, (height + 7) / 8, 1);
        Check(recorder->FinishCommandList(FALSE, &pass->Commands));
        return pass;
    }

    std::shared_ptr<Pass> Find(int id)
    {
        std::lock_guard<std::mutex> lock(registryMutex);
        const auto found = passes.find(id);
        return found == passes.end() ? nullptr : found->second;
    }

    void __stdcall RenderEvent(int id) noexcept
    {
        auto pass = Find(id);
        // Cancellation/disposal removes the ID. Already executing callbacks keep
        // their own reference; a late queued callback never dereferences freed data.
        if (!pass || pass->Status.load(std::memory_order_acquire) != E_PENDING) return;
        pass->Context->ExecuteCommandList(pass->Commands.Get(), TRUE);
        pass->Status.store(pass->Device->GetDeviceRemovedReason(), std::memory_order_release);
    }
}

long CaptureGpuNative::Create(void* color, void* black, void* white, void* output, int width, int height,
    bool linear, bool transparent, bool premultiplied, bool swapRedBlue, int* eventId) noexcept
{
    if (!eventId) return E_POINTER;
    *eventId = 0;
    try
    {
        auto pass = CreatePass(color, black, white, output, width, height, linear, transparent, premultiplied, swapRedBlue);
        std::lock_guard<std::mutex> lock(registryMutex);
        if (nextEventId == (std::numeric_limits<int>::max)()) return E_OUTOFMEMORY;
        const auto id = nextEventId++;
        passes.emplace(id, std::move(pass));
        *eventId = id;
        return S_OK;
    }
    catch (HRESULT error) { return error; }
    catch (const std::bad_alloc&) { return E_OUTOFMEMORY; }
    catch (...) { return E_FAIL; }
}

void CaptureGpuNative::Release(int eventId) noexcept
{
    std::lock_guard<std::mutex> lock(registryMutex);
    passes.erase(eventId);
}

long CaptureGpuNative::Begin(int eventId) noexcept
{
    auto pass = Find(eventId);
    if (!pass) return HRESULT_FROM_WIN32(ERROR_INVALID_HANDLE);
    return pass->Status.exchange(E_PENDING, std::memory_order_acq_rel) == E_PENDING ? E_PENDING : S_OK;
}

long CaptureGpuNative::Result(int eventId) noexcept
{
    auto pass = Find(eventId);
    return pass ? pass->Status.load(std::memory_order_acquire) : HRESULT_FROM_WIN32(ERROR_INVALID_HANDLE);
}

void* CaptureGpuNative::RenderEventFunction() noexcept { return reinterpret_cast<void*>(&RenderEvent); }
