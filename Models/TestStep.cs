using System.Text.Json.Serialization;

namespace Task2_AiTestRunner.Models
{
    public class TestStep
    {
        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("target")]
        public string? Target { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("timeoutMs")]
        public int? TimeoutMs { get; set; }
    }
}
