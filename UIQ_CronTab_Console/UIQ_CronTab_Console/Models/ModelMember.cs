using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace UIQ_CronTab_Console.Models
{
    public class ModelMember
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonPropertyName("model_id")]
        public int Model_Id { get; set; }

        [JsonPropertyName("model_name")]
        public string Model_Name { get; set; }

        [JsonPropertyName("model_position")]
        public int Model_Position { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [JsonPropertyName("member_id")]
        public int Member_Id { get; set; }

        [JsonPropertyName("member_name")]
        public string Member_Name { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("account")]
        public string Account { get; set; }

        [JsonPropertyName("model_group")]
        public string Model_Group { get; set; }

        [JsonPropertyName("member_path")]
        public string Member_Path { get; set; }

        [JsonPropertyName("member_position")]
        public int Member_Position { get; set; }

        [JsonPropertyName("member_dtg_value")]
        public int Member_Dtg_Value { get; set; }

        [JsonPropertyName("reset_model")]
        public string Reset_Model { get; set; }

        [JsonPropertyName("dtg_adjust")]
        public string Dtg_Adjust { get; set; }

        [JsonPropertyName("fix_failed_model")]
        public string Fix_Failed_Model { get; set; }

        [JsonPropertyName("submit_model")]
        public string Submit_Model { get; set; }

        [JsonPropertyName("fix_failed_target_directory")]
        public string Fix_Failed_Target_Directory { get; set; }

        [JsonPropertyName("maintainer_status")]
        public int Maintainer_Status { get; set; }

        [JsonPropertyName("normal_pre_time")]
        public int Normal_Pre_Time { get; set; }

        [JsonPropertyName("typhoon_pre_time")]
        public int Typhoon_Pre_Time { get; set; }

        [JsonPropertyName("typhoon_model")]
        public int Typhoon_Model { get; set; }
    }
}
