using System;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class CardCaptureAnchor
    {
        public bool Initialized;
        public IntPtr Card, SourceCamera;
        public int CardInstanceId, SourceCameraInstanceId;
        public CaptureFraming Framing;
        public CaptureQuaternion Rotation;
        public CaptureVector3 Forward;
        public bool Orthographic;
        public float FieldOfView;
    }
}
