using IL2CppApi.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SnapCardViewHook.Core.Capture
{
    internal static class CardVideoCapture
    {
        public static async Task<CardVideoResult> RecordAsync(CardVideoOptions options, CancellationToken stop,
            IProgress<CardVideoProgress> progress = null)
        {
            if (options == null) 
                throw new ArgumentNullException(nameof(options));
            
            var snapshot = options.Snapshot();
            stop.ThrowIfCancellationRequested();
            
            using (CardCaptureGate.Enter())
                return await Task.Run(() => RecordThenEncodeAsync(snapshot, stop, progress), stop).ConfigureAwait(false);
        }

        internal static int FrameIndex(long timestamp, long start, int framesPerSecond) =>
            checked((int)Math.Floor(Math.Max(0, (timestamp - start) / (double)Stopwatch.Frequency) * framesPerSecond));

        internal static int FrameCount(double seconds, int framesPerSecond, int maximum) =>
            Math.Max(1, Math.Min(maximum, checked((int)Math.Ceiling(Math.Max(0, seconds) * framesPerSecond))));
        
        private static async Task<CardVideoResult> RecordThenEncodeAsync(CardVideoOptions options,
            CancellationToken stop, IProgress<CardVideoProgress> progress)
        {
            var recording = new RecordingState(options, progress);
            try
            {
                await CaptureFramesAsync(recording, stop).ConfigureAwait(false);
                var endFrameCount = GetOutputFrameCount(recording, stop);
                // start encoding only after capture and all Unity cleanup have finished
                return await EncodeFramesAsync(recording, endFrameCount).ConfigureAwait(false);
            }
            finally { recording.Frames.Clear(); }
        }

        private static async Task CaptureFramesAsync(RecordingState recording, CancellationToken stop)
        {
            using var halt = new CancellationTokenSource();
            using (stop.Register(() =>
                   {
                       Interlocked.CompareExchange(ref recording.StopTimestamp, Stopwatch.GetTimestamp(), 0);
                       halt.Cancel();
                   }))
            {
                try { await CaptureLoopAsync(recording, halt).ConfigureAwait(false); }
                catch (OperationCanceledException) when (halt.IsCancellationRequested) { }
                catch (Exception error) { recording.CaptureError = error; }
            }
        }

        private static async Task CaptureLoopAsync(RecordingState recording, CancellationTokenSource halt)
        {
            var options = recording.Options;
            var maximum = checked(options.DurationSeconds * options.FramesPerSecond);
            var frameBytes = options.BytesPerCapturedFrame;
            var ramLimit = options.RamLimitMegabytes * 1024L * 1024;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stage = 0; // 0 = waiting to start; 1 = owns Unity resources; 2 = finished or abandoned.
            Il2CppRuntime runtime = null;
            UnityCardCapture capture = null;
            Action callback = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(halt.Token, timeout.Token);
            var token = linked.Token;

            void Finish(Exception error = null)
            {
                if (Interlocked.Exchange(ref stage, 2) == 2) return;
                SnapTypeDataCollector.RemoveGameUiCallback(callback);
                try
                {
                    try { capture?.Dispose(); }
                    finally { runtime?.Dispose(); }
                }
                catch (Exception cleanupError)
                {
                    error = error == null ? cleanupError : new AggregateException(error, cleanupError);
                }
                // Publish completion only after native cleanup, before encoding can begin.
                if (error is OperationCanceledException) completion.TrySetCanceled();
                else if (error != null) completion.TrySetException(error);
                else completion.TrySetResult(true);
            }

            callback = () =>
            {
                if (Interlocked.CompareExchange(ref stage, 1, 0) == 2) return;
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (capture == null)
                    {
                        runtime = new Il2CppRuntime();
                        capture = new UnityCardCapture(runtime);
                        capture.Prepare(options.Frames, token);
                    }

                    var timestamp = Stopwatch.GetTimestamp();
                    var index = recording.StartTimestamp == 0 ? 0 : FrameIndex(timestamp, recording.StartTimestamp, options.FramesPerSecond);
                    if (index >= maximum) { Finish(); return; }
                    // Check the output frame slot before doing a readback when the game runs faster than the video.
                    if (index <= recording.LastFrameIndex) return;
                    if (recording.BufferedBytes + frameBytes > ramLimit)
                    {
                        recording.Warning = "Capture reached the selected RAM limit. The buffered frames were encoded; increase the limit or lower resolution/duration for a longer capture.";
                        Finish();
                        return;
                    }

                    var pixels = capture.Capture(token);
                    pixels.CaptureTimestamp = timestamp;
                    if (recording.StartTimestamp == 0)
                    {
                        recording.StartTimestamp = timestamp;
                        var remaining = options.DurationSeconds - (Stopwatch.GetTimestamp() - timestamp) / (double)Stopwatch.Frequency;
                        halt.CancelAfter(TimeSpan.FromSeconds(Math.Max(0, remaining)));
                    }
                    recording.Frames.Add(new Frame { Index = index, Pixels = pixels });
                    recording.LastFrameIndex = index;
                    recording.BufferedBytes += frameBytes;
                    timeout.CancelAfter(TimeSpan.FromSeconds(30));
                    ReportProgress(recording, encoding: false);
                    if (index == maximum - 1) Finish();
                }
                catch (Exception error) { Finish(error); }
            };

            using (token.Register(() =>
            {
                // Once setup starts, let the game callback dispose its resources on their owning thread.
                if (Interlocked.CompareExchange(ref stage, 2, 0) == 0) completion.TrySetCanceled();
            }))
            {
                SnapTypeDataCollector.RegisterGameUiCallback(callback);
                try { await completion.Task.ConfigureAwait(false); }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !halt.IsCancellationRequested)
                {
                    throw new TimeoutException("No complete card frame arrived within 30 seconds. Keep the card-details view open and the game running.");
                }
                finally { SnapTypeDataCollector.RemoveGameUiCallback(callback); }
            }
        }

        private static int GetOutputFrameCount(RecordingState recording, CancellationToken stop)
        {
            if (recording.Frames.Count == 0)
            {
                if (recording.CaptureError != null)
                    throw new InvalidOperationException("Recording stopped before the first frame. No movie was created.", recording.CaptureError);
                throw new OperationCanceledException("Recording stopped before the first frame. No movie was created.", stop);
            }
            // Errors/RAM limit keep just the captured timeline, not a long frozen tail.
            if (recording.CaptureError != null || recording.Warning != null) return recording.LastFrameIndex + 1;

            var options = recording.Options;
            var requestedStop = Interlocked.Read(ref recording.StopTimestamp);
            var seconds = requestedStop == 0 ? options.DurationSeconds :
                Math.Min(options.DurationSeconds, Math.Max(0, (requestedStop - recording.StartTimestamp) / (double)Stopwatch.Frequency));
            var maximum = checked(options.DurationSeconds * options.FramesPerSecond);
            return Math.Max(recording.LastFrameIndex + 1, FrameCount(seconds, options.FramesPerSecond, maximum));
        }

        private static async Task<CardVideoResult> EncodeFramesAsync(RecordingState recording, int endFrameCount)
        {
            var options = recording.Options;
            ReportProgress(recording, encoding: true, force: true);
            byte[] last = null;
            using (var writer = new FfmpegProResWriter(options))
            {
                foreach (var frame in recording.Frames)
                {
                    await RepeatFramesAsync(recording, writer, last, frame.Index).ConfigureAwait(false);
                    CardCaptureDispatcher.PrepareVideoPixels(frame.Pixels, options.Frames.TransparentBackground, CancellationToken.None);
                    last = frame.Pixels.Rgba;
                    frame.Pixels = null; // Release frame/matte references as we consume them.
                    await writer.WriteAsync(last).ConfigureAwait(false);
                    recording.BufferedBytes -= options.BytesPerCapturedFrame;
                    recording.EncodedFrames++;
                    ReportProgress(recording, encoding: true);
                }
                await RepeatFramesAsync(recording, writer, last, endFrameCount).ConfigureAwait(false);
                ReportProgress(recording, encoding: true, finalizing: true, force: true);
                await writer.CompleteAsync().ConfigureAwait(false);
            }
            return new CardVideoResult
            {
                OutputPath = options.Frames.OutputPath, CapturedFrames = recording.Frames.Count,
                EncodedFrames = recording.EncodedFrames, RepeatedFrames = recording.RepeatedFrames,
                VideoSeconds = recording.EncodedFrames / (double)options.FramesPerSecond,
                Warning = recording.CaptureError == null ? recording.Warning : "Recording ended early: " + recording.CaptureError.Message
            };
        }

        private static async Task RepeatFramesAsync(RecordingState recording, FfmpegProResWriter writer,
            byte[] pixels, int endFrameCount)
        {
            // Repeats take no extra RAM and preserve real-time playback speed.
            while (pixels != null && recording.EncodedFrames < endFrameCount)
            {
                await writer.WriteAsync(pixels).ConfigureAwait(false);
                recording.EncodedFrames++;
                recording.RepeatedFrames++;
                ReportProgress(recording, encoding: true);
            }
        }

        private static void ReportProgress(RecordingState recording, bool encoding, bool finalizing = false, bool force = false)
        {
            if (!force && recording.ReportedAt.ElapsedMilliseconds < 250) return;
            recording.ReportedAt.Restart();
            recording.Progress?.Report(new CardVideoProgress
            {
                CapturedFrames = recording.Frames.Count, EncodedFrames = recording.EncodedFrames, RepeatedFrames = recording.RepeatedFrames,
                VideoSeconds = (encoding ? recording.EncodedFrames : recording.LastFrameIndex + 1) / (double)recording.Options.FramesPerSecond,
                BufferedBytes = recording.BufferedBytes, Encoding = encoding, Finalizing = finalizing
            });
        }
        
        private sealed class Frame
        {
            public int Index;
            public CapturedCardPixels Pixels;
        }

        private sealed class RecordingState
        {
            public readonly CardVideoOptions Options;
            public readonly IProgress<CardVideoProgress> Progress;
            public readonly List<Frame> Frames = new List<Frame>();
            public readonly Stopwatch ReportedAt = Stopwatch.StartNew();
            public long StartTimestamp, StopTimestamp, BufferedBytes;
            public int LastFrameIndex = -1;
            public int EncodedFrames, RepeatedFrames;
            public Exception CaptureError;
            public string Warning;

            public RecordingState(CardVideoOptions options, IProgress<CardVideoProgress> progress)
            {
                Options = options;
                Progress = progress;
            }
        }
    }
}
