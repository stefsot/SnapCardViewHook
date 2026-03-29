using ImageMagick.Formats;
using ImageMagick;
using OpenCvSharp;

namespace Converter
{
    internal class Program
    {
        public static string OutputDir = "C:\\snap\\export\\ConvertedRenders\\Borders";
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            //foreach (FileInfo finfo in )
            Parallel.ForEach(new DirectoryInfo("C:\\snap\\export\\Renders\\Borders").GetFiles("*"), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, (finfo) =>
            {
                ConvertFile(finfo.FullName);
            }
            );
        }

        public static void ConvertFile(string input)
        {
            string fileName = Path.GetFileName(input);
            string outputPath = Path.Combine(OutputDir, fileName.Replace(".avi", ".webp"));
            int animationDelay = 0;
            // Extract frames from AVI video
            List<string> frameFiles = ExtractFrames(input, out animationDelay);

            // Convert frames to WebP animation
            ConvertFramesToWebP(frameFiles, outputPath, animationDelay);

            // Clean up temporary frame files
            foreach (var file in frameFiles)
            {
                File.Delete(file);
            }
        }

        static List<string> ExtractFrames(string videoPath, out int animationDelay)
        {
            List<string> frameFiles = new List<string>();
            using (var capture = new VideoCapture(videoPath))
            {
                int frameRate = (int)capture.Fps; // Frames per second

                // Calculate the animation delay (100 / frameRate) for WebP
                animationDelay = 100 / frameRate;

                int frameCount = capture.FrameCount;
                Mat frame = new Mat();
                int frameIndex = 0;

                while (capture.Read(frame))
                {
                    //string frameFile = Path.Combine(Path.GetTempPath(), $"frame_{frameIndex}.png");
                    string frameFile = Path.GetTempFileName() + ".png";
                    Cv2.ImWrite(frameFile, frame);
                    frameFiles.Add(frameFile);
                    frameIndex++;
                }
            }
            return frameFiles;
        }

        static void ConvertFramesToWebP(List<string> frameFiles, string outputWebPPath, int animationDelay)
        {
            using (var collection = new MagickImageCollection())
            {
                foreach (var frameFile in frameFiles)
                {
                    var image = new MagickImage(frameFile);
                    image.AnimationDelay = animationDelay; // Set delay between frames (100 = 1 second)
                    collection.Add(image);
                }

                // Set loop count for the animation (0 = infinite loop)
                foreach (var image in collection)
                {
                    image.AnimationIterations = 0;
                }

                // Configure WebP settings
                var settings = new WebPWriteDefines
                {
                    Method = 2, // Compression method (0=fastest, 6=best)

                };
                collection.Optimize();
                collection.Write(outputWebPPath, settings);
            }
        }
    }
}
