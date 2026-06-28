using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using NarratorSvc.Voice;
using Newtonsoft.Json;

namespace NarratorSvc.Ipc
{
    /// <summary>
    /// PST:EE fallback IPC: Lua writes dialogue events via Infinity_SetINIValue into Baldur.lua.
    /// The game flushes profile changes to that file during play (not only on exit).
    /// </summary>
    internal sealed class BaldurIniTailer : IDisposable
    {
        private readonly Regex _profileEntry;

        private const int StaleBaselineThreshold = 8;

        private readonly string _baldurLuaPath;
        private readonly string _ipcStatePath;
        private readonly NarratorService _narrator;
        private readonly Timer _pollTimer;
        private readonly HashSet<string> _seenPayloads = new HashSet<string>(StringComparer.Ordinal);
        private int _lastSeq = -1;
        private bool _primed;

        public BaldurIniTailer(
            string baldurLuaPath,
            string modDataFolder,
            NarratorService narrator,
            string profileSection = "PSTNarrator")
        {
            if (string.IsNullOrWhiteSpace(profileSection))
            {
                profileSection = "PSTNarrator";
            }

            _profileEntry = new Regex(
                @"SetPrivateProfileString\('" + Regex.Escape(profileSection.Trim()) + @"','([^']+)',(?:'((?:\\'|[^'])*)'|\[\[(.*?)\]\])\)",
                RegexOptions.Compiled | RegexOptions.Singleline);
            _baldurLuaPath = baldurLuaPath;
            _ipcStatePath = Path.Combine(modDataFolder, "ipc_last_seq.txt");
            _narrator = narrator;
            _pollTimer = new Timer(Poll, null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
        }

        public void Dispose()
        {
            _pollTimer.Dispose();
        }

        private void Poll(object state)
        {
            try
            {
                if (!File.Exists(_baldurLuaPath))
                {
                    return;
                }

                string content = File.ReadAllText(_baldurLuaPath);
                var slots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (Match match in _profileEntry.Matches(content))
                {
                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Success
                        ? UnescapeLuaString(match.Groups[2].Value)
                        : match.Groups[3].Value;
                    if (key.StartsWith("Event", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(key, "LastSeq", StringComparison.OrdinalIgnoreCase))
                    {
                        slots[key] = value;
                    }
                }

                if (slots.TryGetValue("LastSeq", out string seqText)
                    && int.TryParse(seqText, out int seq))
                {
                    EnsurePrimed(seq);

                    if (seq > _lastSeq)
                    {
                        int processed = 0;
                        for (int s = _lastSeq + 1; s <= seq; s++)
                        {
                            string slotKey = "Event" + (s % 8);
                            if (!slots.TryGetValue(slotKey, out string payload)
                                || string.IsNullOrWhiteSpace(payload))
                            {
                                continue;
                            }

                            if (ProcessPayload(payload))
                            {
                                processed++;
                            }
                        }

                        Console.WriteLine(
                            "[NarratorSvc] Processed Baldur.ini seq "
                            + (_lastSeq + 1)
                            + ".."
                            + seq
                            + " ("
                            + processed
                            + " new events)");
                        _lastSeq = seq;
                        SavePersistedSeq(_lastSeq);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NarratorSvc] Baldur.ini tail error: " + ex.Message);
            }
        }

        private void EnsurePrimed(int baldurSeq)
        {
            if (_primed)
            {
                return;
            }

            _primed = true;
            _lastSeq = LoadPersistedSeq();
            if (_lastSeq >= 0)
            {
                Console.WriteLine(
                    "[NarratorSvc] IPC resumed at sidecar seq "
                    + _lastSeq
                    + " (Baldur LastSeq="
                    + baldurSeq
                    + "; waiting for seq > "
                    + _lastSeq
                    + ")");
                return;
            }

            if (baldurSeq >= StaleBaselineThreshold)
            {
                _lastSeq = baldurSeq;
                SavePersistedSeq(_lastSeq);
                Console.WriteLine(
                    "[NarratorSvc] IPC baselined at LastSeq="
                    + baldurSeq
                    + " (no sidecar state; skipping stale ring-buffer history; waiting for seq > "
                    + baldurSeq
                    + ")");
                return;
            }

            Console.WriteLine("[NarratorSvc] IPC ready (no sidecar state; processing new Baldur.ini events)");
        }

        private int LoadPersistedSeq()
        {
            if (!File.Exists(_ipcStatePath))
            {
                return -1;
            }

            string text = File.ReadAllText(_ipcStatePath).Trim();
            if (int.TryParse(text, out int seq))
            {
                return seq;
            }

            return -1;
        }

        private void SavePersistedSeq(int seq)
        {
            File.WriteAllText(_ipcStatePath, seq.ToString());
        }

        private bool ProcessPayload(string payload)
        {
            if (!_seenPayloads.Add(payload))
            {
                return false;
            }

            DialogueEvent evt;
            try
            {
                evt = JsonConvert.DeserializeObject<DialogueEvent>(payload);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NarratorSvc] Bad Baldur.ini event JSON: " + ex.Message);
                return false;
            }

            if (evt == null || string.IsNullOrWhiteSpace(evt.Command))
            {
                return false;
            }

            switch (evt.Command)
            {
                case "speak":
                    _narrator.HandleSpeak(evt);
                    break;
                case "stop":
                    _narrator.HandleStop(evt);
                    break;
                case "cancelSpeak":
                    _narrator.HandleCancelSpeak(evt.Id, evt.Reason ?? "cancel");
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static string UnescapeLuaString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Replace("\\'", "'");
        }

        public static string ResolvePstBaldurLuaPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Planescape Torment - Enhanced Edition",
                "Baldur.lua");
        }

        public static string ResolveBg2BaldurLuaPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Baldur's Gate II - Enhanced Edition",
                "Baldur.lua");
        }

        public static string ResolveDefaultBaldurLuaPath()
        {
            return ResolvePstBaldurLuaPath();
        }
    }
}
