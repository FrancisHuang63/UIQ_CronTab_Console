using System.Text.Json.Serialization;

namespace UIQ_CronTab_Console.Models
{
    public class BatchView
    {
        [JsonPropertyName("model")]
        public int Model { get; set; }

        [JsonPropertyName("member")]
        public int Member { get; set; }

        [JsonPropertyName("batch")]
        public int Batch { get; set; }

        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("type")]
        public int Type { get; set; }

        [JsonPropertyName("dtg")]
        public int Dtg { get; set; }

        [JsonPropertyName("time")]
        public int Time { get; set; }

        [JsonPropertyName("relay")]
        public int Relay { get; set; }
    }
}