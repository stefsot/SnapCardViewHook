#pragma once

namespace SnapCardViewHook { namespace Native
{
    public ref class CaptureGpuPass sealed
    {
        int _eventId;
    public:
        CaptureGpuPass(System::IntPtr color, System::IntPtr black, System::IntPtr white, System::IntPtr output,
            int width, int height, bool linear, bool transparent, bool premultiplied, bool swapRedBlue);
        ~CaptureGpuPass();
        !CaptureGpuPass();
        property int EventId { int get(); }
        property static System::IntPtr RenderEvent { System::IntPtr get(); }
        void Begin();
        void CheckCompleted();
    };
}}
