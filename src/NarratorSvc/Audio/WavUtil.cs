using System;
using System.IO;
using System.Text;

namespace NarratorSvc.Audio
{
    internal static class WavUtil
    {
        public static bool TryGetWavDurationSeconds(byte[] wav, out float seconds)
        {
            seconds = 0f;
            if (wav == null || wav.Length < 44)
            {
                return false;
            }

            if (Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" || Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
            {
                return false;
            }

            int index = 12;
            short channels = 1;
            int sampleRate = 44100;
            short bitsPerSample = 16;
            int dataSize = 0;
            bool haveFmt = false;
            bool haveData = false;

            while (index + 8 <= wav.Length)
            {
                string chunkId = Encoding.ASCII.GetString(wav, index, 4);
                int chunkSize = BitConverter.ToInt32(wav, index + 4);
                index += 8;

                if (index + chunkSize > wav.Length)
                {
                    break;
                }

                if (chunkId == "fmt ")
                {
                    channels = BitConverter.ToInt16(wav, index + 2);
                    sampleRate = BitConverter.ToInt32(wav, index + 4);
                    if (chunkSize >= 16)
                    {
                        bitsPerSample = BitConverter.ToInt16(wav, index + 14);
                    }

                    haveFmt = true;
                }
                else if (chunkId == "data")
                {
                    dataSize = chunkSize;
                    haveData = true;
                }

                index += chunkSize + (chunkSize % 2);
            }

            if (!haveFmt || !haveData || channels <= 0 || sampleRate <= 0 || bitsPerSample <= 0)
            {
                return false;
            }

            int bytesPerSecond = sampleRate * channels * (bitsPerSample / 8);
            if (bytesPerSecond <= 0)
            {
                return false;
            }

            seconds = (float)dataSize / bytesPerSecond;
            return seconds > 0f;
        }
    }
}
