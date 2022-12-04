using System.Text.Json.Serialization;

namespace UIQ_CronTab_Console.Models
{
    public class BatchConfig
    {
        [JsonPropertyName("model_member_nick")]
        public string Model_Member_Nick { get; set; }

        [JsonPropertyName("batch")]
        public string Batch { get; set; }

        [JsonPropertyName("position")]
        public int Position { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("dtg")]
        public string Dtg { get; set; }

        [JsonPropertyName("time")]
        public int Time { get; set; }
    }
}
