using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Task2_AiTestRunner.AI;
using Task2_AiTestRunner.Models;
using Task2_AiTestRunner.Runner;
using Task2_AiTestRunner.Validation;

namespace Task2_AiTestRunner
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                // Default values
                string url = "https://phptravels.com/demo";
                string testCase = "Open the demo website and verify that the demo page is displayed.";
                bool headless = true;
                string? apiKey = null;
                string? endpoint = null;
                string? model = null;

                // Load config from appsettings.json if present
                string configPath = "appsettings.json";
                if (!File.Exists(configPath))
                {
                    configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                }

                if (File.Exists(configPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(configPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        if (root.TryGetProperty("Url", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString()))
                            url = urlProp.GetString()!;

                        if (root.TryGetProperty("TestCase", out var tcProp) && !string.IsNullOrWhiteSpace(tcProp.GetString()))
                            testCase = tcProp.GetString()!;

                        if (root.TryGetProperty("Headless", out var hlProp))
                            headless = hlProp.GetBoolean();

                        if (root.TryGetProperty("AiConfig", out var aiProp))
                        {
                            if (aiProp.TryGetProperty("ApiKey", out var kProp) && !string.IsNullOrWhiteSpace(kProp.GetString()))
                                apiKey = kProp.GetString();
                            if (aiProp.TryGetProperty("Endpoint", out var epProp) && !string.IsNullOrWhiteSpace(epProp.GetString()))
                                endpoint = epProp.GetString();
                            if (aiProp.TryGetProperty("Model", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString()))
                                model = mProp.GetString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Config Warning] Could not parse appsettings.json: {ex.Message}");
                    }
                }

                // Parse command line arguments
                // Usage: dotnet run -- "<url>" "<testCase>" [--headless/--headed]
                if (args.Length > 0 && !args[0].StartsWith("--"))
                {
                    url = args[0];
                }
                if (args.Length > 1 && !args[1].StartsWith("--"))
                {
                    testCase = args[1];
                }

                foreach (var arg in args)
                {
                    if (arg.Equals("--headless", StringComparison.OrdinalIgnoreCase)) headless = true;
                    if (arg.Equals("--headed", StringComparison.OrdinalIgnoreCase)) headless = false;
                }

                Console.WriteLine("==========================================================");
                Console.WriteLine("           TASK 2: AI-DRIVEN PLAYWRIGHT TEST RUNNER       ");
                Console.WriteLine("==========================================================");
                Console.WriteLine($"Target URL : {url}");
                Console.WriteLine($"Test Case  : \"{testCase}\"");
                Console.WriteLine($"Mode       : {(headless ? "Headless" : "Headed")}");
                Console.WriteLine("----------------------------------------------------------");

                // ----------------------------------------------------
                // STAGE 1 & 2: AI Natural Language -> Structured JSON
                // ----------------------------------------------------
                Console.WriteLine("\n[STAGE 1 & 2] Translating Plain English -> Structured JSON Plan via AI...");
                var aiGenerator = new AiPlanGenerator(apiKey, endpoint, model);
                string jsonPlan = await aiGenerator.GenerateJsonPlanAsync(testCase, url);

                Console.WriteLine("\nGenerated Structured JSON Plan:");
                Console.WriteLine("----------------------------------------------------------");
                Console.WriteLine(jsonPlan);
                Console.WriteLine("----------------------------------------------------------");

                // ----------------------------------------------------
                // STAGE 3: Validate Structured JSON
                // ----------------------------------------------------
                Console.WriteLine("\n[STAGE 3] Validating JSON against Schema...");
                TestPlan? plan = null;
                try
                {
                    plan = JsonSerializer.Deserialize<TestPlan>(jsonPlan, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Schema Validation Error] Invalid JSON formatting: {ex.Message}");
                    return 1;
                }

                var validationResult = TestPlanValidator.Validate(plan);
                if (!validationResult.IsValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[VALIDATION FAILED] The generated plan failed schema validation:");
                    foreach (var error in validationResult.Errors)
                    {
                        Console.WriteLine($" - {error}");
                    }
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("[Validation SUCCESS] Plan adheres to schema specifications.");

                // ----------------------------------------------------
                // STAGE 4: Execute via Playwright
                // ----------------------------------------------------
                Console.WriteLine("\n[STAGE 4] Executing Steps with Playwright Browser Engine...");
                var runner = new PlaywrightTestRunner();
                var report = await runner.ExecutePlanAsync(plan!, headless);

                // ----------------------------------------------------
                // STAGE 5: Final Result Reporting
                // ----------------------------------------------------
                Console.WriteLine("\n==========================================================");
                if (report.OverallStatus == "PASS")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"                   TEST SUITE PASSED                      ");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"                   TEST SUITE FAILED                      ");
                }
                Console.ResetColor();
                Console.WriteLine("==========================================================");
                Console.WriteLine($"Total Steps    : {report.TotalSteps}");
                Console.WriteLine($"Passed Steps   : {report.PassedSteps}");
                Console.WriteLine($"Failed Steps   : {report.FailedSteps}");
                Console.WriteLine($"Execution Time : {report.TotalDurationMs} ms");
                Console.WriteLine($"Report File    : test-report.json");
                Console.WriteLine("==========================================================\n");

                return report.OverallStatus == "PASS" ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[FATAL ERROR] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }
    }
}
