using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NarratorSvc.Cache
{
    internal sealed class CacheEntry
    {
        public string Text { get; set; }
        public string Locale { get; set; }
        public string VoiceId { get; set; }
        public string ModelId { get; set; }
        public double Speed { get; set; }
        public string SpeakerHint { get; set; }
        public string LocalizationKey { get; set; }
        public string CreatedUtc { get; set; }
        public string File { get; set; }
    }

    internal static class CacheKey
    {
        public static string Compute(string locale, string voiceId, string modelId, double speed, string normalizedText)
        {
            string raw = (locale ?? "bg2ee") + "|"
                + (voiceId ?? "") + "|"
                + (modelId ?? "") + "|"
                + speed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "|"
                + (normalizedText ?? "");

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }

    internal sealed class CacheManifest
    {
        public Dictionary<string, CacheEntry> Entries { get; set; } = new Dictionary<string, CacheEntry>();

        public static CacheManifest Load(string path)
        {
            if (!File.Exists(path))
            {
                return new CacheManifest();
            }

            try
            {
                var manifest = new CacheManifest();
                JObject root = JObject.Parse(File.ReadAllText(path));
                JToken entriesToken = root["entries"];
                if (entriesToken is JObject entriesObject)
                {
                    foreach (JProperty property in entriesObject.Properties())
                    {
                        CacheEntry entry = property.Value.ToObject<CacheEntry>();
                        if (entry != null)
                        {
                            manifest.Entries[property.Name] = entry;
                        }
                    }
                }

                return manifest;
            }
            catch
            {
                return new CacheManifest();
            }
        }

        public void Save(string path)
        {
            var entries = new JObject();
            foreach (KeyValuePair<string, CacheEntry> pair in Entries)
            {
                if (pair.Value != null)
                {
                    entries[pair.Key] = JObject.FromObject(pair.Value);
                }
            }

            var root = new JObject { ["entries"] = entries };
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, root.ToString(Formatting.Indented));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }
    }

    internal sealed class DiskCache
    {
        private readonly object _gate = new object();
        private readonly string _rootPath;
        private readonly string _manifestPath;
        private CacheManifest _manifest;

        public DiskCache(string rootPath)
        {
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
            _manifestPath = Path.Combine(_rootPath, "manifest.json");
            _manifest = CacheManifest.Load(_manifestPath);
        }

        public string GetAudioFilePath(string cacheId)
        {
            lock (_gate)
            {
                CacheEntry entry;
                if (!_manifest.Entries.TryGetValue(cacheId, out entry))
                {
                    return null;
                }

                return ResolveAudioPath(entry);
            }
        }

        public bool TryGet(string cacheId, out byte[] audio, out CacheEntry entry)
        {
            audio = null;
            entry = null;

            lock (_gate)
            {
                CacheEntry manifestEntry;
                if (!_manifest.Entries.TryGetValue(cacheId, out manifestEntry))
                {
                    return false;
                }

                string filePath = ResolveAudioPath(manifestEntry);
                if (!File.Exists(filePath))
                {
                    _manifest.Entries.Remove(cacheId);
                    _manifest.Save(_manifestPath);
                    return false;
                }

                entry = manifestEntry;
                audio = File.ReadAllBytes(filePath);
                return audio.Length > 0;
            }
        }

        public void Put(
            string locale,
            string cacheId,
            byte[] audio,
            string text,
            string voiceId,
            string modelId,
            double speed,
            string speakerHint,
            string localizationKey)
        {
            if (audio == null || audio.Length == 0)
            {
                throw new ArgumentException("Audio is empty.", nameof(audio));
            }

            string safeLocale = SanitizeLocale(locale);
            string localeDir = Path.Combine(_rootPath, safeLocale);
            Directory.CreateDirectory(localeDir);

            string relativeFile = safeLocale + "/" + cacheId + ".mp3";
            string absoluteFile = Path.Combine(_rootPath, relativeFile.Replace('/', Path.DirectorySeparatorChar));

            lock (_gate)
            {
                File.WriteAllBytes(absoluteFile, audio);
                _manifest.Entries[cacheId] = new CacheEntry
                {
                    Text = text,
                    Locale = safeLocale,
                    VoiceId = voiceId,
                    ModelId = modelId,
                    Speed = speed,
                    SpeakerHint = speakerHint,
                    LocalizationKey = localizationKey,
                    CreatedUtc = DateTime.UtcNow.ToString("o"),
                    File = relativeFile,
                };

                _manifest.Save(_manifestPath);
            }
        }

        private string ResolveAudioPath(CacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.File))
            {
                return null;
            }

            return Path.Combine(_rootPath, entry.File.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string SanitizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                return "bg2ee";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                locale = locale.Replace(c, '_');
            }

            return locale;
        }
    }
}
