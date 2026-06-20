using System;
using System.IO;
using System.Threading;
using NarratorSvc.Voice;
using Newtonsoft.Json;

namespace NarratorSvc.Ipc
{
    internal sealed class EventTailer : IDisposable
    {
        private readonly string _eventsPath;
        private readonly NarratorService _narrator;
        private readonly Timer _pollTimer;
        private long _offset;
        private string _partialLine = "";

        public EventTailer(string eventsPath, NarratorService narrator)
        {
            _eventsPath = eventsPath;
            _narrator = narrator;
            SeekToEndOfEventsFile();
            _pollTimer = new Timer(Poll, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        private void SeekToEndOfEventsFile()
        {
            if (!File.Exists(_eventsPath))
            {
                Console.WriteLine("[NarratorSvc] No existing events file; waiting for new dialogue events.");
                return;
            }

            var info = new FileInfo(_eventsPath);
            _offset = info.Length;
            if (_offset > 0)
            {
                Console.WriteLine("[NarratorSvc] Skipping " + _offset + " bytes of existing events (tail-only mode).");
            }
        }

        public void Dispose()
        {
            _pollTimer.Dispose();
        }

        private void Poll(object state)
        {
            try
            {
                if (!File.Exists(_eventsPath))
                {
                    return;
                }

                using (var stream = new FileStream(_eventsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (_offset > stream.Length)
                    {
                        _offset = 0;
                        _partialLine = "";
                    }

                    stream.Seek(_offset, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        string chunk = reader.ReadToEnd();
                        _offset = stream.Position;
                        ProcessChunk(chunk);
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NarratorSvc] Event tail error: " + ex.Message);
            }
        }

        private void ProcessChunk(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            string data = _partialLine + chunk;
            string[] lines = data.Split(new[] { '\n' }, StringSplitOptions.None);
            _partialLine = lines[lines.Length - 1];

            for (int i = 0; i < lines.Length - 1; i++)
            {
                ProcessLine(lines[i]);
            }
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            DialogueEvent evt;
            try
            {
                evt = JsonConvert.DeserializeObject<DialogueEvent>(line);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[NarratorSvc] Bad event JSON: " + ex.Message);
                return;
            }

            if (evt == null || string.IsNullOrWhiteSpace(evt.Command))
            {
                return;
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
            }
        }
    }
}
