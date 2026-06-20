using System.Collections.Generic;

namespace NarratorSvc
{
    public sealed class Settings
    {
        public string ApiKey { get; set; } = "";

        public string DefaultVoiceId { get; set; } = ElevenLabsDefaults.VoiceId;

        public string ModelId { get; set; } = ElevenLabsDefaults.ModelId;

        public double SpeechSpeed { get; set; } = ElevenLabsDefaults.DefaultSpeed;

        public float Volume { get; set; } = 1f;

        public bool OnlyUnvoicedLines { get; set; } = true;

        public bool OnlyQuotedDialogue { get; set; }

        public bool VerboseLogging { get; set; }

        public string LanguageFolder { get; set; } = "en_US";

        public List<VoiceMapping> VoiceMappings { get; set; } = new List<VoiceMapping>();
    }

    public sealed class VoiceMapping
    {
        public string Speaker { get; set; } = "";

        public string VoiceId { get; set; } = "";

        public double Speed { get; set; } = ElevenLabsDefaults.DefaultSpeed;
    }
}
