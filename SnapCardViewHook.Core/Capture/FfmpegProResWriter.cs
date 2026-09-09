using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Capture
{
    internal sealed class FfmpegProResWriter : IDisposable
    {
        private readonly CardVideoOptions _options;
        private readonly Queue<string> _errors = new Queue<string>();
        private readonly object _errorLock = new object();
        private readonly Process _process;
        private readonly Task<int> _exit;
        private bool _completed;
        public string PartialPath { get; }

        public FfmpegProResWriter(CardVideoOptions options)
        {
            _options = options;
            var directory = Path.GetDirectoryName(options.Frames.OutputPath);
            Directory.CreateDirectory(directory);
            PartialPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(options.Frames.OutputPath) +
                ".partial-" + Guid.NewGuid().ToString("N") + ".mov");
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = options.FfmpegPath,
                    Arguments = BuildArguments(options, PartialPath),
                    WorkingDirectory = Path.GetDirectoryName(options.FfmpegPath),
                    UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true, RedirectStandardError = true
                }
            };
            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data == null) return;
                lock (_errorLock)
                {
                    if (_errors.Count == 80) _errors.Dequeue();
                    _errors.Enqueue(args.Data.Length > 2000 ? args.Data.Substring(0, 2000) : args.Data);
                }
            };
            try
            {
                if (!_process.Start()) throw new InvalidOperationException("Could not start the selected FFmpeg executable.");
                _process.BeginErrorReadLine();
                _exit = Task.Run(() =>
                {
                    _process.WaitForExit(); // Also drains redirected stderr. Never runs on the game/UI thread.
                    return _process.ExitCode;
                });
                // Also observe a watcher failure when an earlier pipe error takes the abort path.
                _ = _exit.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch
            {
                Abort();
                _process.Dispose();
                throw;
            }
        }

        internal static string BuildArguments(CardVideoOptions options, string outputPath)
        {
            var frames = options.Frames;
            // The worker supplies bottom-up RGBA with premultiplied/black alpha for transparent
            // video. Do not premultiply/unpremultiply again here. Flip, preserve alpha and encode
            // 4:4:4 color with alpha. RGB samples are sRGB; explicitly label transfer/primaries and
            // convert to limited-range BT.709 YUV so the encoder does not guess SD color matrices.
            // setparams is necessary too: prores_ks writes color tags from each input frame.
            // This local FFmpeg build has no MOV alpha-association switch. The comment is a
            // human-readable import instruction, NOT a tag guaranteed to configure every editor.
            var alphaDescription = frames.TransparentBackground
                ? "SnapCardViewHook: alpha is premultiplied with black. Interpret footage as Premultiplied / black, not Straight."
                : "SnapCardViewHook: opaque video.";
            var quality = options.ProResQuantizer == 0 ? "" :
                "-qscale:v " + options.ProResQuantizer.ToString(CultureInfo.InvariantCulture) + " ";
            return string.Format(CultureInfo.InvariantCulture,
                "-hide_banner -loglevel warning -nostats -nostdin -n -f rawvideo -pixel_format rgba " +
                "-video_size {0}x{1} -framerate {2} -i pipe:0 -an " +
                "-vf \"vflip,scale=in_range=full:out_range=tv:out_color_matrix=bt709,format=yuva444p10le," +
                "setparams=range=tv:color_primaries=bt709:color_trc=iec61966-2-1:colorspace=bt709\" " +
                "-c:v prores_ks -profile:v 4 -pix_fmt yuva444p10le -alpha_bits 16 -threads {3} {6}" +
                "-color_range tv -colorspace bt709 -color_primaries bt709 -color_trc iec61966-2-1 " +
                "-metadata comment={5} -movflags +faststart -f mov {4}",
                frames.Width, frames.Height, options.FramesPerSecond,
                Math.Max(1, Math.Min(8, Environment.ProcessorCount / 2)), Quote(outputPath), Quote(alphaDescription), quality);
        }

        private static string Quote(string value)
        {
            // Windows command-line quoting, including backslashes before quotes/end of argument.
            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var c in value)
            {
                if (c == '\\') { slashes++; continue; }
                result.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                result.Append(c);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        public void CheckRunning()
        {
            if (_process.HasExited) throw Error("FFmpeg exited before recording was finished (exit " + _process.ExitCode + ").");
        }

        public async Task WriteAsync(byte[] rgba)
        {
            if (rgba == null || rgba.Length != checked(_options.Frames.Width * _options.Frames.Height * 4))
                throw new ArgumentException("Incorrect video frame buffer size.");
            CheckRunning();
            try
            {
                // No stop token here: finish this whole frame, then close stdin to finalize MOV.
                // A watchdog still terminates our child if a pipe/encoder stops making progress.
                await WithTimeout(_process.StandardInput.BaseStream.WriteAsync(rgba, 0, rgba.Length), 30000).ConfigureAwait(false);
            }
            catch (Exception error) { throw Error("Could not send a complete frame to FFmpeg.", error); }
        }

        public async Task CompleteAsync()
        {
            try
            {
                _process.StandardInput.Close(); // EOF, not killing: lets FFmpeg write the MOV index.
                await WithTimeout(_exit, 60000).ConfigureAwait(false);
            }
            catch (Exception error) { throw Error("Could not finish the FFmpeg recording.", error); }
            if (await _exit.ConfigureAwait(false) != 0) throw Error("FFmpeg failed to finalize the recording.");
            if (!File.Exists(PartialPath) || new FileInfo(PartialPath).Length == 0)
                throw Error("FFmpeg did not produce a movie.");
            try
            {
                // File.Move does not overwrite. A destination created during recording remains safe.
                File.Move(PartialPath, _options.Frames.OutputPath);
                _completed = true;
            }
            catch (Exception error) { throw Error("The movie was encoded but could not be moved to its final filename.", error); }
        }

        private async Task WithTimeout(Task operation, int milliseconds)
        {
            using (var deadline = new System.Threading.CancellationTokenSource())
            {
                var timeout = Task.Delay(milliseconds, deadline.Token);
                if (await Task.WhenAny(operation, timeout).ConfigureAwait(false) != operation)
                {
                    Abort();
                    // Observe a late pipe failure without extending the timeout indefinitely.
                    _ = operation.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    throw Error("FFmpeg stopped responding and was terminated. The partial file was kept.");
                }
                deadline.Cancel();
                await operation.ConfigureAwait(false);
            }
        }

        private Exception Error(string message, Exception inner = null)
        {
            string details;
            lock (_errorLock) details = string.Join(Environment.NewLine, _errors);
            return new IOException(message + "\nPartial recording, if created, is retained at:\n" + PartialPath +
                (details.Length == 0 ? "" : "\n\nFFmpeg:\n" + details), inner);
        }

        private void Abort()
        {
            try { if (!_process.HasExited) _process.Kill(); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            try { _process.WaitForExit(5000); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }

        public void Dispose()
        {
            if (!_completed) Abort();
            _process.Dispose();
            // Never delete a partial movie: failed output may still be useful for recovery.
        }
    }
}
