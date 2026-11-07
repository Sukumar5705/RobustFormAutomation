
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

public sealed class FormTests
{
    private readonly IWebDriver _driver;
    private readonly FieldResolver _resolver;
    private readonly WebDriverWait _wait;

    public FormTests(IWebDriver driver)
    {
        _driver = driver;
        _resolver = new FieldResolver(driver);

        _wait = new WebDriverWait(
            driver,
            TimeSpan.FromSeconds(10)
        );
    }

    // --------------------------------------------
    // RUN ALL TESTS
    // --------------------------------------------

    public List<TestResult> RunAll(
        string firstName,
        string gender,
        string? state)
    {
        var results = new List<TestResult>();

        results.Add(TestFirstName(firstName));
        results.Add(TestGender(gender));
        results.Add(TestState(state));

        return results;
    }

    // --------------------------------------------
    // RESOLVE WITH RETRY
    // --------------------------------------------

    private FieldResolution ResolveWithRetry(
        Func<FieldResolution> resolve,
        int seconds = 10)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);

        FieldResolution last;

        do
        {
            _resolver.Refresh();

            last = resolve();

            if (last.Success)
                return last;

            Thread.Sleep(500);

        } while (DateTime.UtcNow < end);

        return last;
    }

    // --------------------------------------------
    // EXPLAIN RESOLUTION
    // --------------------------------------------

    private static string Explain(FieldResolution resolution)
    {
        return resolution.Message +
            string.Concat(
                resolution.TopCandidates.Select(candidate =>
                    $"\n         - score {candidate.Score}: " +
                    $"{candidate.Evidence} | " +
                    $"{candidate.Candidate.Description}"
                )
            );
    }

    private static string SuccessEvidence(
        FieldResolution resolution)
    {
        if (resolution.TopCandidates.Count == 0)
            return "";

        var candidate = resolution.TopCandidates[0];

        return
            $" [via {candidate.Evidence}, " +
            $"score {resolution.Score}]";
    }

    // --------------------------------------------
    // TEST 1: FIRST NAME
    // --------------------------------------------

    private TestResult TestFirstName(string expected)
    {
        try
        {
            var resolution = ResolveWithRetry(
                () => _resolver.ResolveTextInput("First Name")
            );

            if (!resolution.Success ||
                resolution.Element == null)
            {
                return Failure(
                    "First Name",
                    Explain(resolution)
                );
            }

            IWebElement input = resolution.Element;

            _wait.Until(_ =>
                input.Displayed && input.Enabled
            );

            ScrollIntoView(input);

            input.Clear();
            input.SendKeys(expected);

            // Verify the actual browser value.
            _wait.Until(_ =>
                string.Equals(
                    input.GetDomProperty("value"),
                    expected,
                    StringComparison.Ordinal
                )
            );

            string actual =
                input.GetDomProperty("value") ?? "";

            return new TestResult(
                "First Name",
                true,
                $"Entered and verified: {actual}" +
                SuccessEvidence(resolution)
            );
        }
        catch (Exception ex)
        {
            return Failure("First Name", ex.Message);
        }
    }

    // --------------------------------------------
    // TEST 2: GENDER RADIO BUTTON
    // --------------------------------------------

    private TestResult TestGender(string expected)
    {
        try
        {
            var resolution = ResolveWithRetry(
                () => _resolver.ResolveRadioOption(
                    "Gender",
                    expected
                )
            );

            if (!resolution.Success ||
                resolution.Element == null)
            {
                return Failure(
                    "Gender",
                    Explain(resolution)
                );
            }

            IWebElement radio = resolution.Element;

            if (!radio.Enabled)
            {
                return Failure(
                    "Gender",
                    "Radio button is disabled."
                );
            }

            ScrollIntoView(radio);

            if (!radio.Selected)
            {
                ActivateRadio(radio);
            }

            // Verify selected state.
            _wait.Until(_ => radio.Selected);

            if (!radio.Selected)
            {
                return Failure(
                    "Gender",
                    "Radio button was not selected."
                );
            }

            return new TestResult(
                "Gender",
                true,
                $"Selected and verified: {expected}" +
                SuccessEvidence(resolution)
            );
        }
        catch (Exception ex)
        {
            return Failure("Gender", ex.Message);
        }
    }

    // --------------------------------------------
    // ACTIVATE RADIO
    // --------------------------------------------

    private void ActivateRadio(IWebElement radio)
    {
        // First attempt direct interaction.
        try
        {
            if (radio.Displayed && radio.Enabled)
            {
                radio.Click();
                return;
            }
        }
        catch (ElementNotInteractableException)
        {
            // Also handles ElementClickInterceptedException.
            // Fall back to the associated visible label.
        }

        const string script = @"
        const input = arguments[0];

        const labels = Array.from(input.labels || []);

        const wrappingLabel = input.closest('label');

        if (wrappingLabel) {
            labels.push(wrappingLabel);
        }

        return labels.find(label => {
            const style = getComputedStyle(label);

            return style.display !== 'none' &&
                   style.visibility !== 'hidden' &&
                   Number(style.opacity) !== 0 &&
                   label.getClientRects().length > 0;
        }) || null;
    ";

        var label = ((IJavaScriptExecutor)_driver)
            .ExecuteScript(script, radio) as IWebElement;

        if (label == null)
        {
            throw new InvalidOperationException(
                "Radio input is not directly clickable and " +
                "has no visible associated label."
            );
        }

        ScrollIntoView(label);
        label.Click();
    }

    // --------------------------------------------
    // TEST 3: STATE DROPDOWN
    // --------------------------------------------

    private TestResult TestState(string? expected)
    {
        try
        {
            var resolution = ResolveWithRetry(
                () => _resolver.ResolveDropdown("State")
            );

            if (!resolution.Success ||
                resolution.Element == null)
            {
                return Failure(
                    "State",
                    Explain(resolution)
                );
            }

            IWebElement dropdown = resolution.Element;

            if (!dropdown.Enabled)
            {
                return Failure(
                    "State",
                    "Dropdown is disabled."
                );
            }

            ScrollIntoView(dropdown);

            if (!string.Equals(
                dropdown.TagName,
                "select",
                StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    "State",
                    "A custom dropdown was detected. " +
                    "Its interaction adapter is not implemented."
                );
            }

            var select = new SelectElement(dropdown);

            // If State was provided, match it.
            // Otherwise choose the first valid option.
            IWebElement? option =
                string.IsNullOrWhiteSpace(expected)
                    ? select.Options.FirstOrDefault(
                        o => o.Enabled && !IsPlaceholder(o)
                    )
                    : select.Options.FirstOrDefault(
                        o => Normalize(o.Text) ==
                             Normalize(expected)
                    );

            if (option == null)
            {
                string available = string.Join(
                    ", ",
                    select.Options.Select(
                        o => o.Text.Trim()
                    )
                );

                return Failure(
                    "State",
                    $"Option '{expected}' not found. " +
                    $"Available options: {available}"
                );
            }

            string wanted = option.Text.Trim();

            select.SelectByText(wanted);

            // Verify the selected option.
            _wait.Until(_ =>
                Normalize(select.SelectedOption.Text) ==
                Normalize(wanted)
            );

            string actual =
                select.SelectedOption.Text.Trim();

            return new TestResult(
                "State",
                true,
                $"Selected and verified: {actual}" +
                SuccessEvidence(resolution)
            );
        }
        catch (Exception ex)
        {
            return Failure("State", ex.Message);
        }
    }

    // --------------------------------------------
    // STATE PLACEHOLDER DETECTION
    // --------------------------------------------

    private static bool IsPlaceholder(IWebElement option)
    {
        string text = Normalize(option.Text);

        string value =
            option.GetDomProperty("value") ?? "";

        return value == "" ||
               text.StartsWith("select") ||
               text.StartsWith("choose") ||
               text.StartsWith("-");
    }

    // --------------------------------------------
    // SHARED HELPERS
    // --------------------------------------------

    private void ScrollIntoView(IWebElement element)
    {
        ((IJavaScriptExecutor)_driver).ExecuteScript(
            "arguments[0].scrollIntoView({" +
            "behavior:'instant', block:'center'" +
            "});",
            element
        );
    }

    private static string Normalize(string value)
    {
        return string.Join(
            " ",
            value.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries
            )
        ).Trim().ToLowerInvariant();
    }

    private static TestResult Failure(
        string name,
        string message)
    {
        return new TestResult(
            name,
            false,
            message
        );
    }
}
