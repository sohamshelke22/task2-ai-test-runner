# AI-Driven Playwright Test Runner - C# (.NET)

## Overview
This project is an autonomous, AI-driven test automation runner written in C# and .NET using Microsoft Playwright. It converts human-authored, plain-English test case instructions into a structured, validated JSON execution plan and runs it against real browsers with robust error handling, reporting, and screenshot capture.

```text
┌─────────────────────────┐
│ Plain English Test Case │
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│     AI Generation       │  (Live LLM / Built-in Semantic NLP)
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│     Structured JSON     │  (Standardized Action Schema)
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│    Validate JSON        │  (Schema & Argument Validator)
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│       Playwright        │  (Headless / Headed Chromium & Chrome)
└────────────┬────────────┘
             │
             ▼
┌─────────────────────────┐
│   PASS / FAIL Report    │  (Console + test-report.json + Screenshots)
└─────────────────────────┘
```

---

## Key Features

1. **Natural Language to Test Actions**:
   Translates arbitrary test prompts into concrete, machine-executable browser steps.
2. **Generic & Website-Agnostic**:
   Not hardcoded for one specific website. Works with PHPTravels, CloudQA, or any public web application.
3. **Dual-Engine AI Synthesizer**:
   - **Live LLM Integration**: Connects to OpenAI, Azure OpenAI, Gemini, or any OpenAI-compatible API endpoint when an API key is provided.
   - **Built-in Deterministic NLP Synthesizer**: Zero-dependency offline NLP fallback that extracts intents (navigate, assert, click, fill, check) so the test runner works out of the box without requiring external API keys.
4. **Strict JSON Schema Validation**:
   Validates URLs, action types, required targets, and value arguments before browser initialization to fail fast on invalid plans.
5. **Resilient Playwright Locator Strategy**:
   Uses visibility filtering (`visible=true`), preventing hidden DOM elements (e.g. collapsed accordion menus or mobile navigation nodes) from causing false timeouts.
6. **Automatic Failure Diagnostics**:
   Captures full-page screenshots on any step failure into `screenshots/failure_step_<n>.png` and outputs execution timings into `test-report.json`.

---

## Supported Actions

The JSON schema supports a rich set of browser automation actions:

| Action | Target Required? | Value Required? | Description |
|---|---|---|---|
| `navigate` | Yes (or uses `targetUrl`) | No | Navigates page to URL |
| `click` | Yes | No | Clicks visible element (text, role, CSS) |
| `fill` | Yes | Yes | Fills input field with text value |
| `select` | Yes | Yes | Selects dropdown option |
| `check` / `uncheck` | Yes | No | Toggles checkbox or radio button |
| `assertVisible` | Yes | No | Verifies element is present and visible |
| `assertText` | Optional | Yes | Verifies text exists in target or page |
| `assertTitle` | No | Yes | Verifies page title contains text |
| `assertUrl` | No | Yes | Verifies browser URL contains path/query |
| `waitForTimeout` | No | Yes/Optional | Waits for specified milliseconds |
| `screenshot` | No | Optional (filename) | Captures page screenshot |

---

## Project Structure

```text
Task2-AiTestRunner/
│
├── Models/
│   ├── TestStep.cs              # Action, target, value, timeout schema
│   ├── TestPlan.cs              # Complete test plan model
│   └── ExecutionReport.cs       # Step timings, results, and summary model
│
├── Validation/
│   └── TestPlanValidator.cs     # Schema, URL, and argument validator
│
├── AI/
│   ├── IAiPlanGenerator.cs      # Plan generation abstraction
│   └── AiPlanGenerator.cs       # Dual LLM + deterministic NLP generator
│
├── Runner/
│   └── PlaywrightTestRunner.cs  # Playwright execution engine with diagnostics
│
├── Program.cs                   # Pipeline orchestrator & CLI entry point
├── appsettings.json             # Default URL, test case, & AI configuration
├── Task2-AiTestRunner.csproj
├── .gitignore
└── README.md
```

---

## Setup & Prerequisites

### 1. Requirements
- .NET SDK (8.0+ or 10.0+)
- Windows, macOS, or Linux

### 2. Restore Dependencies
```bash
dotnet restore
```

### 3. Install Playwright Browsers
```bash
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```
*(On Linux/macOS or if pwsh is unavailable, run `dotnet tool install --global Microsoft.Playwright.CLI` followed by `playwright install chromium`)*

---

## Usage

### Run Default Test (PHPTravels Demo)
```bash
dotnet run
```
Default configuration from `appsettings.json`:
- **URL**: `https://phptravels.com/demo`
- **Test Case**: `Open the demo website and verify that the demo page is displayed.`

### Run with Custom URL & Plain English Test Case
```bash
dotnet run -- "https://example.com" "Open the website and verify that title contains 'Example Domain'"
```

### Run in Headed Mode (Visible Browser Window)
```bash
dotnet run -- "https://phptravels.com/demo" "Open the demo website and verify that the demo page is displayed." --headed
```

---

## Configuring an External LLM (Optional)

In `appsettings.json`:
```json
{
  "Url": "https://phptravels.com/demo",
  "TestCase": "Open the demo website and verify that the demo page is displayed.",
  "Headless": true,
  "AiConfig": {
    "Provider": "Auto",
    "Endpoint": "https://api.openai.com/v1/chat/completions",
    "Model": "gpt-4o-mini",
    "ApiKey": "YOUR_OPENAI_API_KEY"
  }
}
```
Or set the environment variable:
```powershell
$env:OPENAI_API_KEY="YOUR_KEY"
```
If no key is configured, the runner automatically uses the built-in deterministic semantic NLP synthesizer.

---

## Example Execution Output

```text
==========================================================
           TASK 2: AI-DRIVEN PLAYWRIGHT TEST RUNNER       
==========================================================
Target URL : https://phptravels.com/demo
Test Case  : "Open the demo website and verify that the demo page is displayed."
Mode       : Headless
----------------------------------------------------------

[STAGE 1 & 2] Translating Plain English -> Structured JSON Plan via AI...
[AI Provider] No external LLM API key detected. Using built-in deterministic NLP semantic plan generator.

Generated Structured JSON Plan:
----------------------------------------------------------
{
  "testCase": "Open the demo website and verify that the demo page is displayed.",
  "targetUrl": "https://phptravels.com/demo",
  "steps": [
    {
      "stepNumber": 1,
      "action": "navigate",
      "target": "https://phptravels.com/demo",
      "description": "Navigate to https://phptravels.com/demo"
    },
    {
      "stepNumber": 2,
      "action": "assertVisible",
      "target": "text=demo",
      "description": "Verify that 'demo' element/page indicator is visible"
    }
  ]
}
----------------------------------------------------------

[STAGE 3] Validating JSON against Schema...
[Validation SUCCESS] Plan adheres to schema specifications.

[STAGE 4] Executing Steps with Playwright Browser Engine...

[Step 1/2] NAVIGATE: Navigate to https://phptravels.com/demo
 -> PASS (2083 ms)

[Step 2/2] ASSERTVISIBLE: Verify that 'demo' element/page indicator is visible
 -> PASS (185 ms)

[Report] Saved execution report to 'test-report.json'

==========================================================
                   TEST SUITE PASSED                      
==========================================================
Total Steps    : 2
Passed Steps   : 2
Failed Steps   : 0
Execution Time : 3868 ms
Report File    : test-report.json
==========================================================
```
