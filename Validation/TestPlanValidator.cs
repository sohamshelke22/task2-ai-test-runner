using System;
using System.Collections.Generic;
using Task2_AiTestRunner.Models;

namespace Task2_AiTestRunner.Validation
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }

    public static class TestPlanValidator
    {
        private static readonly HashSet<string> SupportedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "navigate",
            "click",
            "fill",
            "select",
            "check",
            "uncheck",
            "assertVisible",
            "assertText",
            "assertTitle",
            "assertUrl",
            "waitForTimeout",
            "screenshot"
        };

        public static ValidationResult Validate(TestPlan? plan)
        {
            var result = new ValidationResult();

            if (plan == null)
            {
                result.AddError("Test plan cannot be null or empty.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(plan.TestCase))
            {
                result.AddError("Test plan 'testCase' description is required.");
            }

            if (string.IsNullOrWhiteSpace(plan.TargetUrl))
            {
                result.AddError("Test plan 'targetUrl' is required.");
            }
            else if (!Uri.TryCreate(plan.TargetUrl, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                result.AddError($"'targetUrl' '{plan.TargetUrl}' is not a valid absolute HTTP/HTTPS URL.");
            }

            if (plan.Steps == null || plan.Steps.Count == 0)
            {
                result.AddError("Test plan must contain at least one step in 'steps'.");
                return result;
            }

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var step = plan.Steps[i];
                int index = i + 1;

                if (string.IsNullOrWhiteSpace(step.Action))
                {
                    result.AddError($"Step #{index}: 'action' is required.");
                    continue;
                }

                if (!SupportedActions.Contains(step.Action))
                {
                    result.AddError($"Step #{index}: Unsupported action '{step.Action}'. Supported: {string.Join(", ", SupportedActions)}");
                    continue;
                }

                string action = step.Action.ToLowerInvariant();

                switch (action)
                {
                    case "navigate":
                        if (string.IsNullOrWhiteSpace(step.Target) && string.IsNullOrWhiteSpace(plan.TargetUrl))
                        {
                            result.AddError($"Step #{index} (navigate): 'target' URL is required.");
                        }
                        break;

                    case "click":
                    case "check":
                    case "uncheck":
                    case "assertVisible":
                        if (string.IsNullOrWhiteSpace(step.Target))
                        {
                            result.AddError($"Step #{index} ({step.Action}): 'target' locator/selector is required.");
                        }
                        break;

                    case "fill":
                    case "select":
                        if (string.IsNullOrWhiteSpace(step.Target))
                        {
                            result.AddError($"Step #{index} ({step.Action}): 'target' locator/selector is required.");
                        }
                        if (step.Value == null)
                        {
                            result.AddError($"Step #{index} ({step.Action}): 'value' is required.");
                        }
                        break;

                    case "assertTitle":
                    case "assertUrl":
                        if (string.IsNullOrWhiteSpace(step.Value))
                        {
                            result.AddError($"Step #{index} ({step.Action}): 'value' expected text is required.");
                        }
                        break;

                    case "assertText":
                        if (string.IsNullOrWhiteSpace(step.Value) && string.IsNullOrWhiteSpace(step.Target))
                        {
                            result.AddError($"Step #{index} (assertText): at least one of 'value' or 'target' is required.");
                        }
                        break;
                }
            }

            return result;
        }
    }
}
