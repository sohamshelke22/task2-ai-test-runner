using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Task2_AiTestRunner.Models;

namespace Task2_AiTestRunner.Runner
{
    public class PlaywrightTestRunner
    {
        public async Task<ExecutionReport> ExecutePlanAsync(TestPlan plan, bool headless = true)
        {
            var report = new ExecutionReport
            {
                TestCase = plan.TestCase,
                TargetUrl = plan.TargetUrl,
                TotalSteps = plan.Steps.Count
            };

            var overallStopwatch = Stopwatch.StartNew();

            Directory.CreateDirectory("screenshots");

            using var playwright = await Playwright.CreateAsync();

            IBrowser? browser = null;
            try
            {
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = headless
                });
            }
            catch
            {
                // Fallback to installed Google Chrome channel
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = headless,
                    Channel = "chrome"
                });
            }

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
            });

            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(20000);

            bool executionFailed = false;

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                var stepResult = new StepExecutionResult
                {
                    StepNumber = step.StepNumber > 0 ? step.StepNumber : (i + 1),
                    Action = step.Action,
                    Description = step.Description ?? $"{step.Action} {step.Target} {step.Value}".Trim()
                };

                Console.WriteLine($"\n[Step {stepResult.StepNumber}/{plan.Steps.Count}] {stepResult.Action.ToUpperInvariant()}: {stepResult.Description}");

                var stepStopwatch = Stopwatch.StartNew();

                try
                {
                    await ExecuteStepAsync(page, step, plan);
                    stepStopwatch.Stop();
                    stepResult.Status = "PASS";
                    stepResult.DurationMs = stepStopwatch.ElapsedMilliseconds;
                    report.PassedSteps++;
                    Console.WriteLine($" -> PASS ({stepResult.DurationMs} ms)");
                }
                catch (Exception ex)
                {
                    stepStopwatch.Stop();
                    stepResult.Status = "FAIL";
                    stepResult.DurationMs = stepStopwatch.ElapsedMilliseconds;
                    stepResult.ErrorMessage = ex.Message;
                    report.FailedSteps++;
                    executionFailed = true;

                    Console.WriteLine($" -> FAIL ({stepResult.DurationMs} ms)");
                    Console.WriteLine($"    Error: {ex.Message}");

                    // Capture failure screenshot
                    try
                    {
                        var failureScreenshot = Path.Combine("screenshots", $"failure_step_{stepResult.StepNumber}.png");
                        await page.ScreenshotAsync(new PageScreenshotOptions { Path = failureScreenshot, FullPage = true });
                        stepResult.ScreenshotPath = failureScreenshot;
                        Console.WriteLine($"    Screenshot captured: {failureScreenshot}");
                    }
                    catch (Exception sEx)
                    {
                        Console.WriteLine($"    Could not save failure screenshot: {sEx.Message}");
                    }

                    report.Steps.Add(stepResult);
                    break; // Stop further steps on failure
                }

                report.Steps.Add(stepResult);
            }

            overallStopwatch.Stop();
            report.TotalDurationMs = overallStopwatch.ElapsedMilliseconds;
            report.OverallStatus = !executionFailed && report.PassedSteps == report.TotalSteps ? "PASS" : "FAIL";

            await browser.CloseAsync();

            // Save JSON execution report
            try
            {
                var reportOptions = new JsonSerializerOptions { WriteIndented = true };
                var reportJson = JsonSerializer.Serialize(report, reportOptions);
                await File.WriteAllTextAsync("test-report.json", reportJson);
                Console.WriteLine("\n[Report] Saved execution report to 'test-report.json'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[Report Warning] Failed to save test-report.json: {ex.Message}");
            }

            return report;
        }

        private async Task ExecuteStepAsync(IPage page, TestStep step, TestPlan plan)
        {
            int timeout = step.TimeoutMs ?? 20000;
            string action = step.Action.ToLowerInvariant();

            switch (action)
            {
                case "navigate":
                {
                    string target = !string.IsNullOrWhiteSpace(step.Target) ? step.Target : plan.TargetUrl;
                    await page.GotoAsync(target, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.DOMContentLoaded,
                        Timeout = timeout
                    });
                    break;
                }

                case "click":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeout
                    });
                    await locator.ClickAsync();
                    break;
                }

                case "fill":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeout
                    });
                    await locator.FillAsync(step.Value ?? string.Empty);
                    break;
                }

                case "select":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeout
                    });
                    await locator.SelectOptionAsync(step.Value ?? string.Empty);
                    break;
                }

                case "check":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.CheckAsync();
                    break;
                }

                case "uncheck":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.UncheckAsync();
                    break;
                }

                case "assertvisible":
                {
                    var locator = ResolveLocator(page, step.Target!);
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = timeout
                    });
                    bool visible = await locator.IsVisibleAsync();
                    if (!visible)
                    {
                        throw new Exception($"Assertion failed: Target '{step.Target}' is not visible.");
                    }
                    break;
                }

                case "asserttext":
                {
                    if (!string.IsNullOrWhiteSpace(step.Target))
                    {
                        var locator = ResolveLocator(page, step.Target);
                        await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeout });
                        string actualText = await locator.InnerTextAsync();
                        if (!actualText.Contains(step.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception($"Assertion failed: Expected '{step.Value}' but found '{actualText}' in '{step.Target}'.");
                        }
                    }
                    else
                    {
                        string content = await page.ContentAsync();
                        if (!content.Contains(step.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new Exception($"Assertion failed: Page content does not contain expected text '{step.Value}'.");
                        }
                    }
                    break;
                }

                case "asserttitle":
                {
                    string actualTitle = await page.TitleAsync();
                    if (!actualTitle.Contains(step.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Assertion failed: Expected page title to contain '{step.Value}', but actual title was '{actualTitle}'.");
                    }
                    break;
                }

                case "asserturl":
                {
                    string currentUrl = page.Url;
                    if (!currentUrl.Contains(step.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new Exception($"Assertion failed: Expected URL to contain '{step.Value}', but actual URL was '{currentUrl}'.");
                    }
                    break;
                }

                case "waitfortimeout":
                {
                    int delay = 1000;
                    if (int.TryParse(step.Value, out int parsed)) delay = parsed;
                    else if (step.TimeoutMs.HasValue) delay = step.TimeoutMs.Value;
                    await page.WaitForTimeoutAsync(delay);
                    break;
                }

                case "screenshot":
                {
                    string fileName = step.Value ?? $"screenshot_step_{step.StepNumber}.png";
                    string filePath = Path.Combine("screenshots", fileName);
                    await page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true });
                    Console.WriteLine($"    [Screenshot] Saved to {filePath}");
                    break;
                }

                default:
                    throw new NotSupportedException($"Action '{step.Action}' is not supported.");
            }
        }

        private ILocator ResolveLocator(IPage page, string target, bool visibleOnly = true)
        {
            ILocator locator;

            if (target.StartsWith("text=", StringComparison.OrdinalIgnoreCase))
            {
                string text = target.Substring(5).Trim();
                locator = page.GetByText(text, new PageGetByTextOptions { Exact = false });
            }
            else if (target.StartsWith("role=", StringComparison.OrdinalIgnoreCase))
            {
                locator = page.Locator(target);
            }
            else
            {
                locator = page.Locator(target);
            }

            if (visibleOnly)
            {
                // In Playwright, filter to visible elements so hidden DOM elements (e.g. collapsed menus) don't block visibility assertions
                locator = locator.Locator("visible=true");
            }

            return locator.First;
        }
    }
}
