using OpenCvSharp;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class ScreenRecorder
{
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
}