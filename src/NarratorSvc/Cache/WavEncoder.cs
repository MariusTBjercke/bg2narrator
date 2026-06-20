using System.IO;
using System.Text;

namespace NarratorSvc.Cache
{
    internal static class WavEncoder
    {
        public static byte[] EncodePcm16Le(byte[] pcm, int sampleRate, short channels)
        {
            if (channels <= 0)
            {
                channels = 1;
            }

            short blockAlign = (short)(channels * 2);
            const short bitsPerSample = 16;
            int byteRate = sampleRate * blockAlign;
            int dataSize = pcm.Length;
            int chunkSize = 36 + dataSize;

            using (var stream = new MemoryStream(44 + dataSize))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(chunkSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                writer.Write(pcm);
                return stream.ToArray();
            }
        }
    }
}
