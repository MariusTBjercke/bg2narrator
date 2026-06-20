using System;
using System.Threading;
using NarratorSvc.Ipc;

namespace NarratorSvc.Voice
{
    internal sealed class NarratorService
    {
        private readonly ServiceContext _context;
        private string _pendingSpeakId = "";

        public NarratorService(ServiceContext context)
        {
            _context = context;
        }

        public void HandleStop(DialogueEvent evt)
        {
            string reason = evt.Reason ?? "stop";
            _context.LogVerbose("Stop: " + reason + " gen=" + evt.Generation);

            if (string.Equals(reason, "player-choice", StringComparison.OrdinalIgnoreCase))
            {
                // Only cut audio that is already playing. If ElevenLabs is still
                // synthesizing (common after a cache wipe), let that line finish.
                if (_context.Playback.IsActivelyPlaying())
                {
                    _context.Playback.Stop(reason);
                    _pendingSpeakId = "";
                }

                return;
            }

            if (IsDialogEnd(reason))
            {
                _context.Playback.Stop(reason);
            }
            else if (evt.Generation > 0)
            {
                int playbackGen = string.Equals(reason, "advance", StringComparison.OrdinalIgnoreCase)
                    ? evt.Generation - 1
                    : evt.Generation;
                if (playbackGen < 0)
                {
                    playbackGen = 0;
                }

                _context.Playback.SetGeneration(playbackGen);
            }
            else
            {
                _context.Playback.Stop(reason);
            }

            _pendingSpeakId = "";
        }

        public void HandleCancelSpeak(string id, string reason)
        {
            if (!string.IsNullOrWhiteSpace(id) && string.Equals(_pendingSpeakId, id, StringComparison.Ordinal))
            {
                _context.LogVerbose("Cancel speak " + id + ": " + reason);
                _pendingSpeakId = "";
            }

            if (IsDialogEnd(reason))
            {
                _context.Playback.Stop(reason);
            }
        }

        private static bool IsDialogEnd(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return false;
            }

            return reason.IndexOf("dialog", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(reason, "shutdown", StringComparison.OrdinalIgnoreCase);
        }

        public void HandleSpeak(DialogueEvent evt)
        {
            Settings settings = _context.Settings;
            if (settings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.DefaultVoiceId))
            {
                Console.WriteLine("[NarratorSvc] Skip speak: configure ApiKey and DefaultVoiceId in "
                    + _context.ModFolderName + "/settings.json");
                return;
            }

            if (settings.OnlyUnvoicedLines && evt.StrRef >= 0 && _context.VoicedFilter.IsVoiced(evt.StrRef))
            {
                Console.WriteLine("[NarratorSvc] Skip voiced line strRef=" + evt.StrRef + " speaker=" + evt.Speaker);
                return;
            }

            string speaker = evt.Speaker;
            string rawForTts = evt.RawText;
            if (_context.IsPstNarrator)
            {
                if (!string.IsNullOrWhiteSpace(evt.SpeakText))
                {
                    speaker = evt.Speaker;
                    rawForTts = evt.SpeakText;
                }
                else if (!PstTextNormalizer.TryParseSpeakLine(evt.RawText, out speaker, out string speakText))
                {
                    Console.WriteLine("[NarratorSvc] Skip speak: PST filter rejected speaker=" + evt.Speaker);
                    return;
                }
                else
                {
                    rawForTts = speakText;
                }

                if (settings.OnlyQuotedDialogue)
                {
                    string quoted = PstTextNormalizer.ExtractQuotedDialogue(rawForTts);
                    if (string.IsNullOrWhiteSpace(quoted))
                    {
                        Console.WriteLine("[NarratorSvc] Skip speak: no quoted dialogue speaker=" + speaker);
                        return;
                    }

                    rawForTts = quoted;
                }
            }

            string clean = Bg2TextNormalizer.Normalize(rawForTts);
            if (string.IsNullOrWhiteSpace(clean))
            {
                _context.LogVerbose("Skip speak: empty text after normalize.");
                return;
            }

            VoiceSelection selection = VoiceResolver.Resolve(settings, speaker);
            if (string.IsNullOrWhiteSpace(selection.VoiceId))
            {
                return;
            }

            int generation = evt.Generation;
            _context.Playback.SetGeneration(generation);
            _pendingSpeakId = evt.Id ?? "";
            string speakId = _pendingSpeakId;

            _context.LogVerbose("Speak gen=" + generation + " speaker=" + speaker + " text=" + Truncate(clean));
            Console.WriteLine("[NarratorSvc] Speak (ini) speaker=" + speaker);

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    CachedSpeechResult result = CachedSpeechService.GetOrSynthesize(
                        _context,
                        clean,
                        selection.VoiceId,
                        settings.ModelId,
                        selection.Speed,
                        speaker,
                        evt.StrRef >= 0 ? evt.StrRef.ToString() : null);

                    if (!string.Equals(_pendingSpeakId, speakId, StringComparison.Ordinal))
                    {
                        _context.LogVerbose("Dropped stale speak id=" + speakId);
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.AudioFilePath))
                    {
                        _context.Playback.EnqueuePlayFile(result.AudioFilePath, generation);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[NarratorSvc] Narration failed: " + ex.Message);
                }
            });
        }

        private static string Truncate(string text)
        {
            return text.Length <= 80 ? text : text.Substring(0, 80) + "...";
        }
    }
}
