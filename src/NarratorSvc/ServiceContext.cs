using System;
using System.IO;
using NarratorSvc.Audio;
using NarratorSvc.Ipc;
using NarratorSvc.Tlk;
using NarratorSvc.Voice;
using Newtonsoft.Json;

namespace NarratorSvc
{
    internal sealed class ServiceContext : IDisposable
    {
        public ServiceContext(string gameFolder, string modFolderName = "BG2Narrator")
        {
            GameFolder = gameFolder;
            if (string.IsNullOrWhiteSpace(modFolderName))
            {
                modFolderName = "BG2Narrator";
            }

            ModFolderName = modFolderName.Trim();
            IsPstNarrator = string.Equals(ModFolderName, "PSTNarrator", StringComparison.OrdinalIgnoreCase);
            CacheLocale = IsPstNarrator ? "pstee" : "bg2ee";
            DataFolder = Path.Combine(gameFolder, ModFolderName);
            EventsPath = Path.Combine(DataFolder, "events.jsonl");
            SettingsPath = Path.Combine(DataFolder, "settings.json");
            CacheRoot = Path.Combine(DataFolder, "cache");

            Directory.CreateDirectory(DataFolder);
            Settings = LoadSettings();
            TlkIndex = TlkIndex.Load(gameFolder, Settings.LanguageFolder);
            VoicedFilter = new VoicedLineFilter(TlkIndex);
            DiskCache = new Cache.DiskCache(CacheRoot);
            Playback = new AudioPlaybackService(Settings);
            Narrator = new NarratorService(this);
            EventTailer = new EventTailer(EventsPath, Narrator);

            string baldurLuaPath = IsPstNarrator
                ? BaldurIniTailer.ResolvePstBaldurLuaPath()
                : BaldurIniTailer.ResolveBg2BaldurLuaPath();
            BaldurIniTailer = new BaldurIniTailer(baldurLuaPath, DataFolder, Narrator, ModFolderName);
            BaldurLuaPath = baldurLuaPath;
        }

        public string GameFolder { get; }

        public string ModFolderName { get; }

        public bool IsPstNarrator { get; }

        public string CacheLocale { get; }

        public string DataFolder { get; }

        public string EventsPath { get; }

        public string SettingsPath { get; }

        public string CacheRoot { get; }

        public Settings Settings { get; private set; }

        public TlkIndex TlkIndex { get; }

        public VoicedLineFilter VoicedFilter { get; }

        public Cache.DiskCache DiskCache { get; }

        public AudioPlaybackService Playback { get; }

        public NarratorService Narrator { get; }

        public EventTailer EventTailer { get; }

        public BaldurIniTailer BaldurIniTailer { get; }

        public string BaldurLuaPath { get; }

        public void ReloadSettings()
        {
            Settings = LoadSettings();
            Playback.UpdateVolume(Settings.Volume);
        }

        private Settings LoadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                return new Settings();
            }

            try
            {
                string json = File.ReadAllText(SettingsPath);
                Settings loaded = JsonConvert.DeserializeObject<Settings>(json);
                return loaded ?? new Settings();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NarratorSvc] Failed to load settings: " + ex.Message);
                return new Settings();
            }
        }

        public void LogVerbose(string message)
        {
            if (Settings.VerboseLogging)
            {
                Console.WriteLine("[NarratorSvc] " + message);
            }
        }

        public void Dispose()
        {
            EventTailer.Dispose();
            if (BaldurIniTailer != null)
            {
                BaldurIniTailer.Dispose();
            }
            Playback.Dispose();
        }
    }
}

