using System;
using NarratorSvc.Cache;
using NarratorSvc.ElevenLabs;

namespace NarratorSvc.Voice
{
    internal sealed class CachedSpeechResult
    {
        public CachedSpeechResult(byte[] audio, bool fromCache, string cacheId, string audioFilePath)
        {
            Audio = audio;
            FromCache = fromCache;
            CacheId = cacheId;
            AudioFilePath = audioFilePath;
        }

        public byte[] Audio { get; }

        public bool FromCache { get; }

        public string CacheId { get; }

        public string AudioFilePath { get; }
    }

    internal static class CachedSpeechService
    {
        public static CachedSpeechResult GetOrSynthesize(
            ServiceContext context,
            string text,
            string voiceId,
            string modelId,
            double speed,
            string speakerHint,
            string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text is empty.", nameof(text));
            }

            string locale = context.CacheLocale;
            string cacheId = CacheKey.Compute(locale, voiceId, modelId, speed, text);
            byte[] cachedAudio;
            CacheEntry entry;
            if (context.DiskCache.TryGet(cacheId, out cachedAudio, out entry))
            {
                string cachedPath = context.DiskCache.GetAudioFilePath(cacheId);
                context.LogVerbose("Cache hit " + cacheId);
                return new CachedSpeechResult(cachedAudio, true, cacheId, cachedPath);
            }

            byte[] audio = ElevenLabsClient.Synthesize(context.Settings.ApiKey, text, voiceId, modelId, speed);
            context.DiskCache.Put(
                locale,
                cacheId,
                audio,
                text,
                voiceId,
                modelId,
                speed,
                speakerHint,
                localizationKey);

            string filePath = context.DiskCache.GetAudioFilePath(cacheId);
            context.LogVerbose("Cache miss synthesized " + cacheId);
            return new CachedSpeechResult(audio, false, cacheId, filePath);
        }
    }
}
