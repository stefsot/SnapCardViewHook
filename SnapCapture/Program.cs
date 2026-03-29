using System.Drawing;

namespace SnapCapture
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var captureArea = new Rectangle(1500, 280, 820, 1000);
            ScreenRecorder.RecordScreenArea("output.avi", captureArea, 5, 30);
            Console.WriteLine("Recording completed.");
        }
    }
}
