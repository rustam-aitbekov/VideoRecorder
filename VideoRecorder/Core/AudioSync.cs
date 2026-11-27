namespace VideoRecorder.Core
{
    internal static class AudioSync
    {
        /// <summary>
        /// WASAPI loopback delivers no packets while nothing is playing, so the speaker track would come out
        /// shorter than real time and drift out of sync. This returns how many frames of silence must be
        /// written before <paramref name="incomingFrames"/> so the track stays aligned with the wall clock.
        /// </summary>
        /// <param name="elapsed">Time since recording started, measured when the packet arrived.</param>
        /// <param name="sampleRate">Frames per second of the stream.</param>
        /// <param name="writtenFrames">Frames already written to the track.</param>
        /// <param name="incomingFrames">Frames in the packet that is about to be written (0 when padding at stop).</param>
        /// <param name="toleranceFrames">Gaps up to this size are treated as normal buffering latency and ignored.</param>
        public static long SilenceFramesToInsert(TimeSpan elapsed, int sampleRate, long writtenFrames, long incomingFrames, long toleranceFrames)
        {
            long expectedFrames = (long)(elapsed.TotalSeconds * sampleRate);
            long gap = expectedFrames - (writtenFrames + incomingFrames);
            return gap > toleranceFrames ? gap : 0;
        }
    }
}
