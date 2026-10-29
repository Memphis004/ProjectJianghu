using System;
using UnityEngine;

namespace ProjectJianghu.Game.Presentation.Quality
{
    /// <summary>One window of frame times: what an FPS claim in the stage report is made from.</summary>
    public readonly struct FrameRateSample
    {
        public FrameRateSample(int frames, float seconds, float averageFps, float onePercentLowFps, float worstMillis)
        {
            Frames = frames;
            Seconds = seconds;
            AverageFps = averageFps;
            OnePercentLowFps = onePercentLowFps;
            WorstMillis = worstMillis;
        }

        /// <summary>Frames counted in this window (a frame whose delta is zero is not counted).</summary>
        public int Frames { get; }

        /// <summary>Real seconds the window covered.</summary>
        public float Seconds { get; }

        /// <summary>Frames per second over the window.</summary>
        public float AverageFps { get; }

        /// <summary>The 1 % low: the 99th-percentile frame time, expressed as FPS.</summary>
        public float OnePercentLowFps { get; }

        /// <summary>The single longest frame, in milliseconds.</summary>
        public float WorstMillis { get; }

        /// <summary>An empty window.</summary>
        public static FrameRateSample Empty => new FrameRateSample(0, 0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// Measures the frame rate over a fixed window and hands out a sample with an average and a 1 %
    /// low.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An average alone hides the stall that a player feels, so the window also reports the 99th
    /// percentile frame time as a "1 % low" FPS and the single worst frame. Both are computed from an
    /// array that is sorted on demand — no LINQ, no per-frame allocation (AGENTS rule 7).
    /// </para>
    /// <para>
    /// The probe measures the <b>player</b>, not the simulation: <c>Time.unscaledDeltaTime</c> is the
    /// frame the compositor drew, which is the number a browser can act on.
    /// </para>
    /// </remarks>
    public sealed class FrameRateProbe
    {
        private readonly float[] _frameMillis;
        private readonly float _windowSeconds;
        private int _count;
        private float _elapsed;

        /// <summary>Creates a probe that samples a window of <paramref name="windowSeconds"/>.</summary>
        public FrameRateProbe(float windowSeconds = 3f, int capacity = 4096)
        {
            _windowSeconds = Mathf.Max(0.5f, windowSeconds);
            _frameMillis = new float[Mathf.Max(64, capacity)];
            Last = FrameRateSample.Empty;
        }

        /// <summary>The most recent completed or in-progress window.</summary>
        public FrameRateSample Last { get; private set; }

        /// <summary>True when the window has collected at least one frame.</summary>
        public bool HasFrames => _count > 0;

        /// <summary>Adds one frame's delta time and completes the window when it is full.</summary>
        /// <returns>True when the window just completed (a caller may publish then).</returns>
        public bool Add(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0f)
            {
                return false;
            }

            if (_count < _frameMillis.Length)
            {
                _frameMillis[_count] = unscaledDeltaSeconds * 1000f;
                _count++;
            }

            _elapsed += unscaledDeltaSeconds;
            if (_elapsed < _windowSeconds)
            {
                return false;
            }

            Last = Snapshot();
            _count = 0;
            _elapsed = 0f;
            return true;
        }

        /// <summary>The current window as a sample, without closing it.</summary>
        public FrameRateSample Snapshot()
        {
            if (_count == 0 || _elapsed <= 0f)
            {
                return FrameRateSample.Empty;
            }

            var sorted = new float[_count];
            Array.Copy(_frameMillis, sorted, _count);
            Array.Sort(sorted);

            float total = 0f;
            for (int i = 0; i < sorted.Length; i++)
            {
                total += sorted[i];
            }

            int percentileIndex = Mathf.Clamp((int)(sorted.Length * 0.99f), 0, sorted.Length - 1);
            float percentileMillis = sorted[percentileIndex];

            return new FrameRateSample(
                _count,
                _elapsed,
                _count / _elapsed,
                percentileMillis <= 0f ? 0f : 1000f / percentileMillis,
                sorted[sorted.Length - 1]);
        }

        /// <summary>Discards the window, so the next sample starts at the tier switch.</summary>
        public void Reset()
        {
            _count = 0;
            _elapsed = 0f;
            Last = FrameRateSample.Empty;
        }
    }
}
