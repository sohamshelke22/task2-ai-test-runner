using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Task2_AiTestRunner.Models
{
    public class TestPlan
    {
        [JsonPropertyName("testCase")]
        public string TestCase { get; set; } = string.Empty;

        [JsonPropertyName("targetUrl")]
        public string TargetUrl { get; set; } = string.Empty;

        [JsonPropertyName("steps")]
        public List<TestStep> Steps { get; set; } = new List<TestStep>();
    }
}

