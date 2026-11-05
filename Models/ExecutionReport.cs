using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Task2_AiTestRunner.Models
{
    public class StepExecutionResult
    {
        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "PENDING"; // PASS, FAIL, SKIPPED

        [JsonPropertyName("durationMs")]
        public long DurationMs { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("screenshotPath")]
        public string? ScreenshotPath { get; set; }
    }

    public class ExecutionReport
    {
        [JsonPropertyName("testCase")]
        public string TestCase { get; set; } = string.Empty;

        [JsonPropertyName("targetUrl")]
        public string TargetUrl { get; set; } = string.Empty;

        [JsonPropertyName("overallStatus")]
        public string OverallStatus { get; set; } = "FAIL"; // PASS or FAIL

        [JsonPropertyName("totalSteps")]
        public int TotalSteps { get; set; }

        [JsonPropertyName("passedSteps")]
        public int PassedSteps { get; set; }

        [JsonPropertyName("failedSteps")]
        public int FailedSteps { get; set; }

        [JsonPropertyName("totalDurationMs")]
        public long TotalDurationMs { get; set; }

        [JsonPropertyName("executedAt")]
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("steps")]
        public List<StepExecutionResult> Steps { get; set; } = new List<StepExecutionResult>();
    }
}
