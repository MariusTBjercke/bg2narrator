using Newtonsoft.Json;

namespace NarratorSvc.Ipc
{
    internal sealed class DialogueEvent
    {
        [JsonProperty("v")]
        public int Version { get; set; }

        [JsonProperty("cmd")]
        public string Command { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("gen")]
        public int Generation { get; set; }

        [JsonProperty("speaker")]
        public string Speaker { get; set; }

        [JsonProperty("strRef")]
        public int StrRef { get; set; } = -1;

        [JsonProperty("rawText")]
        public string RawText { get; set; }

        [JsonProperty("speakText")]
        public string SpeakText { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("ts")]
        public long Timestamp { get; set; }
    }
}
