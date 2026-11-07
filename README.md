# Robust Form Automation Using C# and Selenium

A configurable and resilient web form automation framework built using **C#, .NET, and Selenium WebDriver**. The project identifies form elements using DOM metadata, accessible labels, nearby text, and confidence-based scoring instead of depending entirely on fixed XPath or CSS selectors.

The framework is designed to handle changes in HTML structure, element order, attributes, and label associations while avoiding unsafe interactions with ambiguous elements.

---

## 1. Project Overview

Traditional Selenium automation scripts often depend on fixed locators such as XPath, CSS selectors, element IDs, and class names.

These locators can become unreliable when a website changes its HTML structure, renames attributes, moves form elements, or modifies label associations.

This project addresses that problem by implementing a **DOM-based intelligent field resolution mechanism**.

The automation discovers candidate form elements, analyzes their attributes and surrounding text, assigns confidence scores, and selects an element only when sufficient evidence is available.

### Target Form

[CloudQA Automation Practice Form](https://app.cloudqa.io/home/AutomationPracticeForm)

### Fields Automated

| Field | Interaction |
|---|---|
| First Name | Text input |
| Gender | Radio button selection |
| State | Dropdown selection |

---

## 2. Objectives

- Automate form interactions using C# and Selenium WebDriver.
- Identify form fields without relying exclusively on fixed element selectors.
- Handle changes in HTML element order and attributes.
- Detect labels that are not explicitly associated with inputs using `for` attributes.
- Identify radio button groups and their corresponding options.
- Use confidence-based scoring to evaluate candidate elements.
- Detect ambiguous matches and avoid selecting the wrong element.
- Verify the actual values selected or entered in the browser.
- Support configurable URLs and test inputs.
- Validate the implementation using multiple modified HTML test pages.

---

## 3. Technology Stack

| Technology | Purpose |
|---|---|
| C# | Primary programming language |
| .NET | Application framework |
| Selenium WebDriver | Browser automation |
| ChromeDriver | Chrome browser interaction |
| JavaScript | DOM discovery and metadata extraction |
| HTML5 | Test form pages |
| PowerShell | Running and testing the application |
| Git and GitHub | Version control and project hosting |

---

## 4. Project Structure

```text
RobustFormAutomation/
│
├── Program.cs
│
├── FieldResolver.cs
│
├── FormTests.cs
│
├── Report.cs
│
├── RobustFormAutomation.csproj
│
├── TestPages/
│   ├── reordered.html
│   ├── renamed-attributes.html
│   └── decoy-fields.html
│
├── TestPages_Extra/
│   ├── aria-labels.html
│   ├── wrapped-labels.html
│   ├── plain-adjacent-labels.html
│   ├── nested-layout.html
│   ├── text-node-labels.html
│   ├── explicit-label-after-control.html
│   ├── noise-hidden-decoys.html
│   ├── state-default-option.html
│   ├── ambiguous-duplicate-fields.html
│   └── custom-dropdown-known-limit.html
│
├── README.md
│
└── .gitignore
```

### Main Components

**Program.cs**

Handles application configuration, command-line arguments, browser initialization, URL validation, execution, and final reporting.

**FieldResolver.cs**

Implements DOM discovery, metadata extraction, label matching, candidate scoring, confidence evaluation, and ambiguity detection.

**FormTests.cs**

Performs form interactions, retries field resolution, enters values, selects options, and verifies the results.

**Report.cs**

Displays test execution results, including passed and failed tests and the final execution status.

**TestPages/**

Contains the original HTML fixtures used to validate structural changes.

**TestPages_Extra/**

Contains additional HTML fixtures covering different labeling strategies, nested layouts, hidden elements, duplicate fields, and unsupported dropdown structures.

---

## 5. System Architecture

The application follows a modular execution pipeline.

```text
                 START
                   |
                   v
          Load Configuration
                   |
                   v
             Launch Chrome
                   |
                   v
             Open Target URL
                   |
                   v
          Discover DOM Elements
                   |
                   v
        Extract Element Metadata
                   |
                   v
          Generate Candidates
                   |
                   v
          Calculate Confidence
                   |
                   v
          Check Ambiguity
                   |
             +-----+-----+
             |           |
         Sufficient   Insufficient
          Evidence     Evidence
             |           |
             v           v
        Select Field   Report Failure
             |
             v
       Perform Interaction
             |
             v
       Verify Browser Value
             |
             v
       Generate Test Report
             |
             v
              END
```

### Field Resolution Pipeline

**Step 1: DOM Discovery**

JavaScript executes inside the browser to discover relevant form controls, including:

- Text inputs
- Radio buttons
- Select elements
- Textareas
- Elements with supported accessibility roles

**Step 2: Metadata Extraction**

The resolver extracts available information such as:

- Associated labels
- Wrapping labels
- ARIA labels
- ARIA labelled-by references
- Placeholders
- Title attributes
- Nearby text
- Radio group labels
- Radio option labels
- Element values
- HTML attributes
- Dropdown options
- Visibility and enabled state

**Step 3: Candidate Filtering**

Candidates are filtered according to the requested field type and their visibility, enabled state, or actionability.

**Step 4: Confidence Scoring**

The resolver compares the requested field name against available metadata and ranks matching candidates.

**Step 5: Ambiguity Detection**

The resolver checks whether the best candidate has sufficient confidence and is sufficiently distinct from other candidates.

**Step 6: Interaction and Verification**

Selenium interacts with the selected element and verifies the resulting browser value or selected state.

---

## 6. Confidence-Based Field Identification

The project uses a confidence-scoring mechanism to avoid relying on a single HTML attribute.

Different evidence sources are assigned different weights.

### Field Matching Evidence

| Evidence | Base Score |
|---|---:|
| Associated label | 100 |
| Wrapping label | 100 |
| ARIA labelled-by | 95 |
| ARIA label | 90 |
| Placeholder | 75 |
| Title | 70 |
| Nearby text | Distance-based |
| HTML ID, name, and class | Weak supporting evidence |

Nearby text receives a score based on its distance from the element.

The current implementation uses:

```csharp
private static int NearbyScore(int distance)
{
    return Math.Max(56, 90 - distance);
}
```

Exact matching is prioritized over partial matching.

Partial matches receive a score penalty of 25 points.

### Confidence Threshold

```csharp
private const int ConfidenceThreshold = 70;
```

The candidate must meet the minimum confidence threshold to be selected.

### Ambiguity Detection

When two leading candidates have scores that differ by less than 10 points, the resolver reports an ambiguous match instead of selecting an arbitrary element.

This helps prevent incorrect form interactions.

---

## 7. Handling HTML Changes

The framework is designed to handle several types of HTML modifications.

### Example 1: Associated Label

```html
<label for="firstName">First Name</label>

<input
    type="text"
    id="firstName"
/>
```

The resolver can identify the input through its associated label.

### Example 2: Wrapping Label

```html
<label>
    First Name
    <input type="text">
</label>
```

The resolver can use the wrapping label.

### Example 3: Plain Adjacent Label

```html
<label>First Name</label>
<input type="text">
```

Even without the `for` attribute, the resolver can use nearby text as evidence.

### Example 4: Modified Attributes

```html
<label>First Name</label>

<input
    type="text"
    name="customer_information"
    class="input-control"
    placeholder="Enter your name"
/>
```

The resolver does not depend exclusively on the original element ID or class.

### Example 5: Radio Group

```html
<label>Gender</label>

<div>
    <input type="radio" name="gender" value="Male">
    <span>Male</span>

    <input type="radio" name="gender" value="Female">
    <span>Female</span>
</div>
```

The resolver identifies the group label and requested radio option using available DOM evidence.

---

## 8. Installation and Setup

### Prerequisites

Install the following:

1. .NET SDK compatible with the project target framework.
2. Google Chrome browser.
3. Git (optional, for repository management).

### Clone the Repository

```bash
git clone YOUR_GITHUB_REPOSITORY_URL
```

Navigate to the project directory:

```bash
cd RobustFormAutomation
```

### Restore Dependencies

```bash
dotnet restore
```

### Build the Project

```bash
dotnet build
```

For a Release build:

```bash
dotnet build -c Release
```

The project dependencies are defined in `RobustFormAutomation.csproj`.

---

## 9. Running the Automation

### Run Against the Default Website

```powershell
dotnet run
```

### Run in Headless Mode

```powershell
dotnet run -- --headless
```

### Provide a Custom URL

```powershell
dotnet run -- --url "https://app.cloudqa.io/home/AutomationPracticeForm"
```

### Configure Form Values

```powershell
dotnet run -- --first-name "Sukumar" --gender "Male" --state "India"
```

### Configure All Inputs and Run Headless

```powershell
dotnet run -- --url "https://app.cloudqa.io/home/AutomationPracticeForm" --first-name "Sukumar" --gender "Male" --state "India" --headless
```

### Run Without Specifying State

```powershell
dotnet run -- --first-name "Sukumar" --gender "Male" --headless
```

When State is omitted, the current implementation selects the first enabled, non-placeholder option from a supported native select element.

### Run Against a Local HTML File

The project also supports positional URLs and local HTML file paths.

```powershell
dotnet run -- "TestPages\reordered.html" --headless
```

Alternatively, use a local HTTP server.

Start the server:

```powershell
py -m http.server 8000
```

Then execute:

```powershell
dotnet run -- --url "http://localhost:8000/TestPages/reordered.html" --headless
```

### Available Configuration Options

| Argument | Description |
|---|---|
| `--url` | Target website or local file URL |
| `--first-name` | First Name value |
| `--gender` | Gender option to select |
| `--state` | State option to select |
| `--headless` | Run Chrome without a visible browser window |
| `--keep-open` | Keep the browser open after execution, if supported by the application |

---

## 10. Test Scenarios

The project includes original and additional HTML fixtures to evaluate the robustness of field identification.

### Original Test Pages

| Test Page | Purpose |
|---|---|
| `reordered.html` | Tests fields moved to different positions |
| `renamed-attributes.html` | Tests modified IDs, names, and attributes |
| `decoy-fields.html` | Tests unrelated or misleading candidate elements |

### Additional Test Pages

| Test Page | Purpose |
|---|---|
| `aria-labels.html` | Tests ARIA-based field identification |
| `wrapped-labels.html` | Tests labels wrapping form controls |
| `plain-adjacent-labels.html` | Tests labels without explicit associations |
| `nested-layout.html` | Tests nested HTML structures |
| `text-node-labels.html` | Tests plain text nodes near form fields |
| `explicit-label-after-control.html` | Tests labels positioned after controls |
| `noise-hidden-decoys.html` | Tests hidden and disabled decoy elements |
| `state-default-option.html` | Tests automatic State option selection |
| `ambiguous-duplicate-fields.html` | Tests duplicate field detection |
| `custom-dropdown-known-limit.html` | Tests unsupported custom dropdown interaction |

### Running the Original Test Pages

Start the local HTTP server:

```powershell
py -m http.server 8000
```

Run each test in another terminal:

```powershell
dotnet run -- --url "http://localhost:8000/TestPages/reordered.html" --headless

dotnet run -- --url "http://localhost:8000/TestPages/renamed-attributes.html" --headless

dotnet run -- --url "http://localhost:8000/TestPages/decoy-fields.html" --headless
```

### Running Additional Test Pages

```powershell
Get-ChildItem .\TestPages_Extra\*.html | ForEach-Object {
    Write-Host "`nTesting $($_.Name)" -ForegroundColor Cyan

    dotnet run -- --url "http://localhost:8000/TestPages_Extra/$($_.Name)" --headless
}
```

### Expected Behavior

- Valid and sufficiently identifiable fields should be resolved.
- Hidden and disabled controls should not be selected.
- Duplicate or ambiguous matches should be rejected.
- Unsupported custom dropdown interactions should be reported.
- Selected and entered values should be verified.

A successful execution of every test page is not assumed. Deliberately ambiguous and unsupported fixtures are expected to demonstrate safe failure handling.

---

## 11. Test Results

Record the actual results after running the test suite.

| Test Page | First Name | Gender | State | Overall |
|---|---|---|---|---|
| reordered.html | Pending | Pending | Pending | Pending |
| renamed-attributes.html | Pending | Pending | Pending | Pending |
| decoy-fields.html | Pending | Pending | Pending | Pending |
| aria-labels.html | Pending | Pending | Pending | Pending |
| wrapped-labels.html | Pending | Pending | Pending | Pending |
| plain-adjacent-labels.html | Pending | Pending | Pending | Pending |
| nested-layout.html | Pending | Pending | Pending | Pending |
| text-node-labels.html | Pending | Pending | Pending | Pending |
| explicit-label-after-control.html | Pending | Pending | Pending | Pending |
| noise-hidden-decoys.html | Pending | Pending | Pending | Pending |
| state-default-option.html | Pending | Pending | Pending | Pending |
| ambiguous-duplicate-fields.html | Expected safe failure | Expected safe failure | Expected safe failure | Review |
| custom-dropdown-known-limit.html | Depends on page | Depends on page | Expected unsupported interaction | Review |

**Note:** Replace Pending with your actual execution results. Do not report unexecuted tests as passed.

---

## 12. Limitations

The current implementation focuses on conventional HTML form controls.

The following scenarios are not fully supported:

- Custom dropdowns built using `div` elements.
- Gender represented as a dropdown instead of radio buttons.
- Iframes containing form controls.
- Dynamic forms requiring specialized interaction.
- Complex layouts with multiple unrelated text elements near a control.
- Some hidden or heavily styled radio inputs without usable associated labels.
- Automatic recovery from stale WebElement references.

Nearby-text detection is heuristic. It provides useful evidence but cannot guarantee correct identification in every arbitrary HTML layout.

The confidence threshold and ambiguity checks are designed to favor safe failure over incorrect selection.

---

## 13. Future Improvements

Potential enhancements include:

- Custom dropdown interaction support.
- Iframe discovery and switching.
- Improved handling of hidden and visually styled form controls.
- Stale element recovery.
- Support for additional input types and accessible widgets.
- Configurable confidence thresholds.
- Structured JSON execution reports.
- Expanded automated regression testing.
- Parallel execution for independent test pages.

---

## 14. Version Control

The repository can be managed using Git.

```bash
git add .
git commit -m "Complete robust Selenium form automation"
git push origin main
```

Generated build folders and environment-specific files should be excluded using `.gitignore`.

---

## 15. Conclusion

Robust Form Automation demonstrates a DOM-based approach to Selenium form automation using C#.

Instead of depending entirely on fixed selectors, the system discovers candidate form elements, extracts semantic and structural information, evaluates evidence through confidence scoring, and verifies browser interactions.

The project emphasizes adaptability to HTML changes, reliable element identification, ambiguity detection, and safe automation behavior.

**Developed using C#, .NET, Selenium WebDriver, and JavaScript.**
