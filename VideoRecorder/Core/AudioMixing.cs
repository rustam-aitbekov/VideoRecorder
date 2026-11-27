using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VideoRecorder.Core
{
    internal static class AudioMixing
    {
        /// <summary>
        /// Converts <paramref name="source"/> to the given sample rate and channel count so it can be fed
        /// into a <see cref="MixingSampleProvider"/> together with other sources.
        /// Sources with more than two channels keep their first (front left/right) channels.
        /// </summary>
        public static ISampleProvider ConvertTo(ISampleProvider source, int sampleRate, int channels)
        {
            if (channels is < 1 or > 2)
                throw new ArgumentOutOfRangeException(nameof(channels), channels, "Only mono or stereo output is supported.");

            ISampleProvider result = source;

            if (result.WaveFormat.Channels != channels)
            {
                result = (result.WaveFormat.Channels, channels) switch
                {
                    (1, 2) => new MonoToStereoSampleProvider(result),
                    (2, 1) => new StereoToMonoSampleProvider(result),
                    _ => new MultiplexingSampleProvider(new[] { result }, channels),
                };
            }

            if (result.WaveFormat.SampleRate != sampleRate)
                result = new WdlResamplingSampleProvider(result, sampleRate);

            return result;
        }
    }
}
