using System;

namespace NarratorSvc.Voice
{
    internal struct VoiceSelection
    {
        public string VoiceId;
        public double Speed;
    }

    internal static class VoiceResolver
    {
        public static VoiceSelection Resolve(Settings settings, string speakerHint)
        {
            var selection = new VoiceSelection
            {
                VoiceId = settings != null ? settings.DefaultVoiceId : null,
                Speed = settings != null ? settings.SpeechSpeed : ElevenLabsDefaults.DefaultSpeed,
            };

            if (settings == null || string.IsNullOrWhiteSpace(speakerHint) || settings.VoiceMappings == null)
            {
                return selection;
            }

            string target = speakerHint.Trim();
            foreach (VoiceMapping mapping in settings.VoiceMappings)
            {
                if (mapping == null
                    || string.IsNullOrWhiteSpace(mapping.Speaker)
                    || string.IsNullOrWhiteSpace(mapping.VoiceId))
                {
                    continue;
                }

                if (string.Equals(mapping.Speaker.Trim(), target, StringComparison.OrdinalIgnoreCase))
                {
                    selection.VoiceId = mapping.VoiceId.Trim();
                    selection.Speed = mapping.Speed > 0d ? mapping.Speed : selection.Speed;
                    if (settings.VerboseLogging)
                    {
                        Console.WriteLine("[NarratorSvc] Speaker '" + speakerHint + "' -> mapped voice " + selection.VoiceId);
                    }

                    return selection;
                }
            }

            if (settings.VerboseLogging)
            {
                Console.WriteLine("[NarratorSvc] Speaker '" + speakerHint + "' -> default voice " + selection.VoiceId);
            }

            return selection;
        }
    }
}
