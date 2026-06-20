using System;
using System.IO;
using NLayer;
using NarratorSvc.Cache;

namespace NarratorSvc.Audio
{
    internal static class Mp3Decoder
    {
        public static byte[] ToWav(byte[] mp3Bytes, float gain)
        {
            if (mp3Bytes == null || mp3Bytes.Length == 0)
            {
                throw new ArgumentException("MP3 data is empty.", nameof(mp3Bytes));
            }

            using (var stream = new MemoryStream(mp3Bytes))
            using (var mpeg = new MpegFile(stream))
            {
                int channels = mpeg.Channels;
                int sampleRate = mpeg.SampleRate;
                long framesPerChannel = mpeg.Length;
                if (framesPerChannel <= 0 || channels <= 0 || sampleRate <= 0)
                {
                    throw new InvalidOperationException("MP3 decode produced no samples.");
                }

                long totalSamplesLong = framesPerChannel * channels;
                if (totalSamplesLong > int.MaxValue)
                {
                    throw new InvalidOperationException("MP3 clip is too long to decode.");
                }

                int sampleCount = (int)totalSamplesLong;
                var floats = new float[sampleCount];
                int read = 0;
                while (read < sampleCount)
                {
                    int chunk = mpeg.ReadSamples(floats, read, sampleCount - read);
                    if (chunk <= 0)
                    {
                        break;
                    }

                    read += chunk;
                }

                if (read <= 0)
                {
                    throw new InvalidOperationException("MP3 decode failed.");
                }

                if (read < sampleCount)
                {
                    Array.Resize(ref floats, read);
                }

                byte[] pcm = FloatsToPcm16Le(floats, gain);
                return WavEncoder.EncodePcm16Le(pcm, sampleRate, (short)channels);
            }
        }

        public static bool LooksLikeMp3(byte[] data)
        {
            if (data == null || data.Length < 3)
            {
                return false;
            }

            if (data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
            {
                return true;
            }

            return data[0] == 0xFF && (data[1] & 0xE0) == 0xE0;
        }

        private static byte[] FloatsToPcm16Le(float[] floats, float gain)
        {
            var pcm = new byte[floats.Length * 2];
            for (int i = 0; i < floats.Length; i++)
            {
                float sample = floats[i] * gain;
                if (sample > 1f)
                {
                    sample = 1f;
                }
                else if (sample < -1f)
                {
                    sample = -1f;
                }

                short value = (short)(sample * 32767f);
                pcm[i * 2] = (byte)(value & 0xFF);
                pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
            }

            return pcm;
        }
    }
}
