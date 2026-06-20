using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NarratorSvc.Tlk
{
    internal sealed class TlkEntry
    {
        public int Flags;
        public string SoundResRef = "";
    }

    internal sealed class TlkIndex
    {
        private const int HeaderSize = 0x12;
        private const int EntrySize = 0x28;
        private const int SoundExistsFlag = 0x02;

        private readonly List<TlkEntry> _entries = new List<TlkEntry>();

        public int Count
        {
            get { return _entries.Count; }
        }

        public bool TryGetEntry(int strRef, out TlkEntry entry)
        {
            entry = null;
            if (strRef < 0 || strRef >= _entries.Count)
            {
                return false;
            }

            entry = _entries[strRef];
            return entry != null;
        }

        public bool HasSound(int strRef)
        {
            TlkEntry entry;
            if (!TryGetEntry(strRef, out entry))
            {
                return false;
            }

            return (entry.Flags & SoundExistsFlag) != 0 && !string.IsNullOrWhiteSpace(entry.SoundResRef);
        }

        public static TlkIndex Load(string gameFolder, string languageFolder)
        {
            var index = new TlkIndex();
            string lang = string.IsNullOrWhiteSpace(languageFolder) ? "en_US" : languageFolder.Trim();

            string[] candidates =
            {
                Path.Combine(gameFolder, "lang", lang, "dialog.tlk"),
                Path.Combine(gameFolder, "dialog.tlk"),
                Path.Combine(gameFolder, "lang", lang, "dialogf.tlk"),
                Path.Combine(gameFolder, "dialogf.tlk"),
            };

            bool loadedDialog = false;
            foreach (string path in candidates)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                if (path.EndsWith("dialogf.tlk", StringComparison.OrdinalIgnoreCase) && loadedDialog)
                {
                    continue;
                }

                LoadFile(path, index);
                Console.WriteLine("[NarratorSvc] TLK loaded: " + path);
                if (path.EndsWith("dialog.tlk", StringComparison.OrdinalIgnoreCase))
                {
                    loadedDialog = true;
                }
            }
            if (!loadedDialog)
            {
                Console.WriteLine("[NarratorSvc] Warning: no dialog.tlk found under " + gameFolder);
            }

            Console.WriteLine("[NarratorSvc] TLK index loaded: " + index.Count + " entries");
            return index;
        }

        private static void LoadFile(string path, TlkIndex index)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < HeaderSize)
            {
                return;
            }

            string signature = Encoding.ASCII.GetString(data, 0, 4);
            string version = Encoding.ASCII.GetString(data, 4, 4);
            if (signature != "TLK " || version != "V1  ")
            {
                Console.WriteLine("[NarratorSvc] Unsupported TLK format in " + path);
                return;
            }

            int entryCount = BitConverter.ToInt32(data, 0x0A);
            int start = HeaderSize;
            int required = start + (entryCount * EntrySize);
            if (data.Length < required)
            {
                Console.WriteLine("[NarratorSvc] TLK truncated: " + path);
                return;
            }

            int baseCount = index._entries.Count;
            if (baseCount == 0)
            {
                index._entries.Capacity = entryCount;
                for (int i = 0; i < entryCount; i++)
                {
                    index._entries.Add(ParseEntry(data, start + (i * EntrySize)));
                }
            }
            else
            {
                for (int i = 0; i < entryCount; i++)
                {
                    int target = i;
                    if (target >= index._entries.Count)
                    {
                        index._entries.Add(ParseEntry(data, start + (i * EntrySize)));
                    }
                }
            }
        }

        private static TlkEntry ParseEntry(byte[] data, int offset)
        {
            int flags = BitConverter.ToUInt16(data, offset);
            string resref = Encoding.ASCII.GetString(data, offset + 2, 8).TrimEnd('\0', ' ');
            return new TlkEntry
            {
                Flags = flags,
                SoundResRef = resref,
            };
        }
    }
}
