using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Task2_AiTestRunner.Models;

namespace Task2_AiTestRunner.AI
{
    public class AiPlanGenerator : IAiPlanGenerator
    {
        private readonly string? _apiKey;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly HttpClient _httpClient;

        public AiPlanGenerator(string? apiKey = null, string? endpoint = null, string? model = null)
        {
            _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                             ?? Environment.GetEnvironmentVariable("AI_API_KEY");
            _endpoint = endpoint ?? "https://api.openai.com/v1/chat/completions";
            _model = model ?? "gpt-4o-mini";
            _httpClient = new HttpClient();
        }

        public async Task<string> GenerateJsonPlanAsync(string testCase, string targetUrl)
        {
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                try
                {
                    Console.WriteLine("[AI Provider] Live LLM configured. Calling API...");
                    return await CallLlmApiAsync(testCase, targetUrl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Provider Warning] Live LLM call failed ({ex.Message}). Falling back to built-in semantic NLP generator.");
                }
            }
            else
            {
                Console.WriteLine("[AI Provider] No external LLM API key detected. Using built-in deterministic NLP semantic plan generator.");
            }

            return GenerateDeterministicPlan(testCase, targetUrl);
        }

        private async Task<string> CallLlmApiAsync(string testCase, string targetUrl)
        {
            var systemPrompt = @"You are an expert QA Automation Engineer.
Translate a natural language test case and target URL into a strictly formatted JSON execution plan for a Playwright test runner.

Return ONLY raw JSON conforming to this schema (no markdown, no backticks, no comments):
{
  ""testCase"": ""original test case"",
  ""targetUrl"": ""target URL"",
  ""steps"": [
    {
      ""stepNumber"": 1,
      ""action"": ""navigate"" | ""click"" | ""fill"" | ""select"" | ""check"" | ""uncheck"" | ""assertVisible"" | ""assertText"" | ""assertTitle"" | ""assertUrl"" | ""waitForTimeout"" | ""screenshot"",
      ""target"": ""css, text=..., role, or url"",
      ""value"": ""text value (optional depending on action)"",
      ""description"": ""human readable explanation""
    }
  ]
}";

            var userPrompt = $"Target URL: {targetUrl}\nTest Case: {testCase}";

            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.1
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var content = doc.RootElement
                             .GetProperty("choices")[0]
                             .GetProperty("message")
                             .GetProperty("content")
                             .GetString() ?? string.Empty;

            // Remove markdown code fences if model enclosed response in ```json ... ```
            content = Regex.Replace(content, @"^```(json)?", "", RegexOptions.Multiline).Trim();
            content = Regex.Replace(content, @"```$", "", RegexOptions.Multiline).Trim();

            return content;
        }

        public string GenerateDeterministicPlan(string testCase, string targetUrl)
        {
            var steps = new List<TestStep>();
            int stepCounter = 1;

            // Step 1: Always navigate to the target URL
            steps.Add(new TestStep
            {
                StepNumber = stepCounter++,
                Action = "navigate",
                Target = targetUrl,
                Description = $"Navigate to {targetUrl}"
            });

            // Split into clauses by common conjunctions or punctuation
            var rawClauses = Regex.Split(testCase, @"(?:\b(?:and\s+then|then|after\s+that|and)\b|[;.,])", RegexOptions.IgnoreCase);

            foreach (var rawClause in rawClauses)
            {
                var clause = rawClause.Trim();
                if (string.IsNullOrWhiteSpace(clause)) continue;

                // Match: "open / navigate / go to"
                if (Regex.IsMatch(clause, @"^(?:open|navigate|go\s+to|visit)\b", RegexOptions.IgnoreCase))
                {
                    // Check if navigating to a specific URL
                    var urlMatch = Regex.Match(clause, @"https?://[^\s]+", RegexOptions.IgnoreCase);
                    if (urlMatch.Success && !urlMatch.Value.Equals(targetUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        steps.Add(new TestStep
                        {
                            StepNumber = stepCounter++,
                            Action = "navigate",
                            Target = urlMatch.Value,
                            Description = $"Navigate to {urlMatch.Value}"
                        });
                    }
                    continue; // Base navigation already handled in step 1
                }

                // Match: "verify / check / assert / ensure / confirm"
                if (Regex.IsMatch(clause, @"^(?:verify|check|assert|ensure|confirm)\b", RegexOptions.IgnoreCase))
                {
                    // Strip the assertion verb prefix: "verify that ", "check that ", "assert ", etc.
                    string assertionTarget = Regex.Replace(clause, @"^(?:verify|check|assert|ensure|confirm)\s+(?:that\s+)?", "", RegexOptions.IgnoreCase).Trim();

                    // Check for: "title contains / is ..."
                    var titleMatch = Regex.Match(assertionTarget, @"(?:title\s+(?:contains|is|has))\s+['""]?([^'""]+)['""]?", RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        steps.Add(new TestStep
                        {
                            StepNumber = stepCounter++,
                            Action = "assertTitle",
                            Value = titleMatch.Groups[1].Value.Trim(),
                            Description = $"Verify that page title contains '{titleMatch.Groups[1].Value.Trim()}'"
                        });
                        continue;
                    }

                    // Check for: "url contains ..."
                    var urlCheckMatch = Regex.Match(assertionTarget, @"(?:url\s+(?:contains|is))\s+['""]?([^'""]+)['""]?", RegexOptions.IgnoreCase);
                    if (urlCheckMatch.Success)
                    {
                        steps.Add(new TestStep
                        {
                            StepNumber = stepCounter++,
                            Action = "assertUrl",
                            Value = urlCheckMatch.Groups[1].Value.Trim(),
                            Description = $"Verify that URL contains '{urlCheckMatch.Groups[1].Value.Trim()}'"
                        });
                        continue;
                    }

                    // Check for: "[the] <entity> [page] is displayed / visible / loaded"
                    var pageDisplayedMatch = Regex.Match(assertionTarget, @"^(?:the\s+)?([\w\s-]+?)(?:\s+page)?\s+(?:is\s+)?(?:displayed|visible|shown|loaded)$", RegexOptions.IgnoreCase);
                    if (pageDisplayedMatch.Success)
                    {
                        var entityName = pageDisplayedMatch.Groups[1].Value.Trim();
                        if (!string.Equals(entityName, "it", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(entityName, "page", StringComparison.OrdinalIgnoreCase))
                        {
                            steps.Add(new TestStep
                            {
                                StepNumber = stepCounter++,
                                Action = "assertVisible",
                                Target = $"text={entityName}",
                                Description = $"Verify that '{entityName}' element/page indicator is visible"
                            });
                            continue;
                        }
                    }

                    // Check for: "page contains 'xyz'" or "'xyz' is visible"
                    var textMatch = Regex.Match(assertionTarget, @"(?:contains\s+['""]([^'""]+)['""]|['""]([^'""]+)['""]\s+(?:is\s+displayed|is\s+visible))", RegexOptions.IgnoreCase);
                    if (textMatch.Success)
                    {
                        var textVal = (textMatch.Groups[1].Success ? textMatch.Groups[1].Value : textMatch.Groups[2].Value).Trim();
                        steps.Add(new TestStep
                        {
                            StepNumber = stepCounter++,
                            Action = "assertVisible",
                            Target = $"text={textVal}",
                            Description = $"Verify that '{textVal}' is visible"
                        });
                        continue;
                    }

                    // Fallback assertion on page body
                    steps.Add(new TestStep
                    {
                        StepNumber = stepCounter++,
                        Action = "assertVisible",
                        Target = "body",
                        Description = $"Verify page content is loaded for: {assertionTarget}"
                    });
                    continue;
                }

                // Match: "click / tap / press / select"
                var clickMatch = Regex.Match(clause, @"^(?:click|tap|press|select)\s+(?:on\s+)?(?:the\s+)?(?:['""]([^'""]+)['""]|([\w\s-]+?))(?:\s+button|\s+link)?$", RegexOptions.IgnoreCase);
                if (clickMatch.Success)
                {
                    var targetName = (clickMatch.Groups[1].Success ? clickMatch.Groups[1].Value : clickMatch.Groups[2].Value).Trim();
                    steps.Add(new TestStep
                    {
                        StepNumber = stepCounter++,
                        Action = "click",
                        Target = $"text={targetName}",
                        Description = $"Click on '{targetName}'"
                    });
                    continue;
                }

                // Match: "fill / type / enter ... into/in ..."
                var fillMatch = Regex.Match(clause, @"^(?:fill|type|enter|input)\s+['""]?([^'""]+?)['""]?\s+(?:in|into|with)\s+(?:the\s+)?['""]?([^'""]+?)['""]?$", RegexOptions.IgnoreCase);
                if (fillMatch.Success)
                {
                    var val = fillMatch.Groups[1].Value.Trim();
                    var field = fillMatch.Groups[2].Value.Trim();
                    steps.Add(new TestStep
                    {
                        StepNumber = stepCounter++,
                        Action = "fill",
                        Target = $"input[name*='{field}' i], input[placeholder*='{field}' i], input[id*='{field}' i]",
                        Value = val,
                        Description = $"Fill '{field}' with '{val}'"
                    });
                    continue;
                }
            }

            // Ensure we have at least one assertion if none was explicitly generated
            if (steps.Count == 1 && steps[0].Action == "navigate")
            {
                steps.Add(new TestStep
                {
                    StepNumber = stepCounter++,
                    Action = "assertVisible",
                    Target = "body",
                    Description = "Verify page body is displayed"
                });
            }

            var plan = new TestPlan
            {
                TestCase = testCase,
                TargetUrl = targetUrl,
                Steps = steps
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            return JsonSerializer.Serialize(plan, options);
        }
    }
}
