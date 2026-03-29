using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core
{
    internal class ScreenRecorder
    {
        // Import user32.dll for interacting with the Windows API
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;


        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        // Import necessary Windows API functions
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public const uint SWP_NOSIZE = 0x0001;
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;


        public static void RecordScreenArea(string outputPath, Rectangle captureArea, int durationInSeconds, int fps)
        {
            using (var writer = new VideoWriter(outputPath, FourCC.XVID, fps, new OpenCvSharp.Size(captureArea.Width, captureArea.Height)))
            {
                var startTime = DateTime.Now;
                var endTime = startTime.AddSeconds(durationInSeconds);
                var frameDuration = TimeSpan.FromSeconds(1.0 / fps);

                while (DateTime.Now < endTime)
                {
                    var frame = CaptureScreenArea(captureArea);
                    writer.Write(frame);
                    frame.Dispose();
                    System.Threading.Thread.Sleep(frameDuration);
                }
            }
        }

        private static Mat CaptureScreenArea(Rectangle captureArea)
        {
            using (var bitmap = new Bitmap(captureArea.Width, captureArea.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(captureArea.Left, captureArea.Top, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Bmp);
                    ms.Seek(0, SeekOrigin.Begin);
                    return Mat.FromStream(ms, ImreadModes.Color);
                }
            }
        }

        public static void ClickPosition(int x, int y)
        {
            /*
            // Find the window handle
            IntPtr hWnd = FindWindow(null, "SNAP");

            if (hWnd != IntPtr.Zero)
            {
                // Bring the window to the foreground
                SetForegroundWindow(hWnd);

                // Convert client area coordinates to screen coordinates
                POINT point = new POINT { X = x, Y = y };
                ClientToScreen(hWnd, ref point);

                // Combine X and Y coordinates into a single parameter
                IntPtr lParam = (IntPtr)((point.Y << 16) | (point.X & 0xFFFF));

                // Send mouse down and up messages to simulate a click
                PostMessage(hWnd, WM_LBUTTONDOWN, IntPtr.Zero, lParam);
                PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);

                Console.WriteLine($"Clicked at ({x}, {y}) in the application.");
            }
            else
            {
                Console.WriteLine("Window not found.");
            }
            */

            SetCursorPos(x, y);

            // Simulate mouse down and up to perform a click
            INPUT[] inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void ClickOffVariant()
        {
            ClickPosition(544, 1600);
        }

        public static void ClickOnVariant()
        {
            ClickPosition(1516, 700);
        }
    }
}
