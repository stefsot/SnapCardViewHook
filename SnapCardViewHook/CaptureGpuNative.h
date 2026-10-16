#pragma once

namespace CaptureGpuNative
{
    long Create(void* color, void* black, void* white, void* output, int width, int height,
        bool linear, bool transparent, bool premultiplied, bool swapRedBlue, int* eventId) noexcept;
    void Release(int eventId) noexcept;
    long Begin(int eventId) noexcept;
    long Result(int eventId) noexcept;
    void* RenderEventFunction() noexcept;
}
