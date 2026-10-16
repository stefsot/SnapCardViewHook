#include "pch.h"
#include "CaptureGpuPass.h"
#include "CaptureGpuNative.h"

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace SnapCardViewHook::Native;

CaptureGpuPass::CaptureGpuPass(IntPtr color, IntPtr black, IntPtr white, IntPtr output,
    int width, int height, bool linear, bool transparent, bool premultiplied, bool swapRedBlue)
{
    int id = 0;
    const auto result = CaptureGpuNative::Create(color.ToPointer(), black.ToPointer(), white.ToPointer(), output.ToPointer(),
        width, height, linear, transparent, premultiplied, swapRedBlue, &id);
    if (result < 0)
        throw gcnew InvalidOperationException("Could not initialize the Direct3D 11 capture shader.", Marshal::GetExceptionForHR(result));
    _eventId = id;
}

CaptureGpuPass::~CaptureGpuPass() { this->!CaptureGpuPass(); }
CaptureGpuPass::!CaptureGpuPass()
{
    const auto id = System::Threading::Interlocked::Exchange(_eventId, 0);
    if (id != 0) CaptureGpuNative::Release(id);
}

int CaptureGpuPass::EventId::get()
{
    if (_eventId == 0) throw gcnew ObjectDisposedException("CaptureGpuPass");
    return _eventId;
}

IntPtr CaptureGpuPass::RenderEvent::get() { return IntPtr(CaptureGpuNative::RenderEventFunction()); }
void CaptureGpuPass::Begin() { Marshal::ThrowExceptionForHR(CaptureGpuNative::Begin(EventId)); }
void CaptureGpuPass::CheckCompleted()
{
    const auto result = CaptureGpuNative::Result(EventId);
    if (result < 0)
        throw gcnew InvalidOperationException("The capture shader did not complete before Unity's readback.", Marshal::GetExceptionForHR(result));
}
