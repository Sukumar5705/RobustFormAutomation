
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenQA.Selenium;

public sealed class FieldCandidate
{
    public IWebElement Element { get; init; } = null!;

    public string Tag { get; init; } = "";
    public string Type { get; init; } = "";
    public string Role { get; init; } = "";

    public string LabelFor { get; init; } = "";
    public string WrappingLabel { get; init; } = "";
    public string AriaLabelledBy { get; init; } = "";
    public string AriaLabel { get; init; } = "";

    public string Placeholder { get; init; } = "";
    public string Title { get; init; } = "";
    public string NearbyText { get; init; } = "";
    public int NearbyDistance { get; init; } = 99;

    public string GroupLabel { get; init; } = "";
    public string OptionLabel { get; init; } = "";

    public string Value { get; init; } = "";
    public string WeakAttributes { get; init; } = "";

    public List<string> Options { get; init; } = new();

    public bool IsVisible { get; init; }
    public bool IsEnabled { get; init; }
    public bool IsActionable { get; init; }

    public string Description =>
        $"<{Tag}> type={Type} " +
        $"label={LabelFor} " +
        $"group={GroupLabel} " +
        $"option={OptionLabel} " +
        $"nearby={NearbyText} " +
        $"placeholder={Placeholder}";
}

public sealed class ScoredCandidate
{
    public FieldCandidate Candidate { get; init; } = null!;
    public int Score { get; init; }
    public string Evidence { get; init; } = "";
}

public sealed class FieldResolution
{
    public bool Success { get; init; }
    public IWebElement? Element { get; init; }
    public int Score { get; init; }
    public string Message { get; init; } = "";
    public List<ScoredCandidate> TopCandidates { get; init; } = new();
}

public sealed class FieldResolver
{
    private readonly IWebDriver _driver;
    private readonly List<FieldCandidate> _candidates = new();

    private const int ConfidenceThreshold = 70;

    public FieldResolver(IWebDriver driver)
    {
        _driver = driver;
    }

    // --------------------------------------------------
    // JavaScript DOM discovery
    // --------------------------------------------------

    private const string DiscoveryScript = """
    const controls = Array.from(
        document.querySelectorAll(
            'input:not([type="hidden"]), select, textarea, ' +
            '[role="textbox"], [role="combobox"], ' +
            '[role="radio"], [role="listbox"], ' +
            '[aria-haspopup], [contenteditable="true"]'
        )
    );

    function clean(value) {
        return (value || "").replace(/\s+/g, " ").trim();
    }

    function text(element) {
        return element
            ? clean(element.innerText || element.textContent)
            : "";
    }

    function visible(element) {
        if (!element) return false;

        const style = getComputedStyle(element);

        return style.display !== "none" &&
               style.visibility !== "hidden" &&
               Number(style.opacity) !== 0 &&
               element.getClientRects().length > 0;
    }

    function getLabelFor(element) {
        const labels = element.labels
            ? Array.from(element.labels)
            : [];

        return labels.map(text).filter(Boolean).join(" | ");
    }

    function getWrappingLabel(element) {
        return text(element.closest("label"));
    }

    function getAriaLabelledBy(element) {
        const ids = (
            element.getAttribute("aria-labelledby") || ""
        ).split(/\s+/).filter(Boolean);

        return ids.map(id => {
            return text(document.getElementById(id));
        }).filter(Boolean).join(" | ");
    }

    // Finds short text preceding the element.
    // Supports labels that are not associated using for=.
    function getNearby(element) {
        let current = element;

        for (let depth = 0; depth < 3; depth++) {
            if (!current || !current.parentElement) break;

            let node = current.previousSibling;
            let hops = 0;

            while (node) {
                if (node.nodeType === 3) {
                    const t = clean(node.textContent);

                    if (t && t.length <= 60) {
                        return {
                            text: t,
                            distance: depth * 10 + hops
                        };
                    }
                }

                if (
                    node.nodeType === 1 &&
                    !node.matches(
                        "input, select, textarea, button, script, style"
                    ) &&
                    !node.querySelector(
                        "input, select, textarea"
                    ) &&
                    visible(node)
                ) {
                    const t = text(node);

                    if (t && t.length <= 60) {
                        return {
                            text: t,
                            distance: depth * 10 + hops
                        };
                    }
                }

                node = node.previousSibling;
                hops++;
            }

            current = current.parentElement;
        }

        return { text: "", distance: 99 };
    }

    function getRadioGroup(element) {
        if (
            element.type !== "radio" &&
            element.getAttribute("role") !== "radio"
        ) {
            return "";
        }

        let ancestor = element.parentElement;

        while (ancestor && ancestor !== document.body) {
            const radios = ancestor.querySelectorAll(
                'input[type="radio"], [role="radio"]'
            );

            if (radios.length > 1) {
                const groupLabels = Array.from(
                    ancestor.children
                ).filter(child => {
                    return child.tagName === "LEGEND" ||
                        (
                            child.tagName === "LABEL" &&
                            !child.htmlFor
                        );
                }).map(text).filter(Boolean);

                const ariaLabel =
                    ancestor.getAttribute("aria-label") || "";

                const labelledBy =
                    getAriaLabelledBy(ancestor);

                const directLabel = [
                    ...groupLabels,
                    ariaLabel,
                    labelledBy
                ].filter(Boolean).join(" | ");

                if (directLabel) {
                    return directLabel;
                }

                // Fallback for standalone group text:
                // <label>Gender</label><div><input radio...>
                const near = getNearby(ancestor);

                if (near.text && near.distance <= 10) {
                    return near.text;
                }
            }

            ancestor = ancestor.parentElement;
        }

        return "";
    }

    function getOptionLabel(element) {
        const labels = element.labels
            ? Array.from(element.labels).map(text)
            : [];

        const wrapping = getWrappingLabel(element);

        const next = element.nextElementSibling;

        const adjacent =
            next &&
            ["LABEL", "SPAN"].includes(next.tagName) &&
            visible(next)
                ? text(next)
                : "";

        return [
            ...labels,
            wrapping,
            adjacent
        ].filter(Boolean).join(" | ");
    }

    function getOptions(element) {
        if (!element.options) return [];

        return Array.from(element.options).map(option => {
            return text(option);
        }).filter(Boolean);
    }

    return controls.map(element => {
        const nearby = getNearby(element);
        const associatedLabel = getLabelFor(element);
        const wrappingLabel = getWrappingLabel(element);
        const next = element.nextElementSibling;

        const labelVisible =
            (
                element.labels &&
                Array.from(element.labels).some(visible)
            ) ||
            (
                element.closest("label") &&
                visible(element.closest("label"))
            ) ||
            (
                next &&
                ["LABEL", "SPAN"].includes(next.tagName) &&
                visible(next)
            );

        const isVisible = visible(element);

        return {
            element: element,

            tag: element.tagName.toLowerCase(),

            type: element.getAttribute("type") ||
                (element.tagName === "INPUT" ? "text" : ""),

            role: element.getAttribute("role") || "",

            labelFor: associatedLabel,
            wrappingLabel: wrappingLabel,

            ariaLabelledBy: getAriaLabelledBy(element),
            ariaLabel: element.getAttribute("aria-label") || "",

            placeholder:
                element.getAttribute("placeholder") || "",

            title: element.getAttribute("title") || "",

            nearbyText: nearby.text,
            nearbyDistance: nearby.distance,

            groupLabel: getRadioGroup(element),
            optionLabel: getOptionLabel(element),

            value: element.value || "",

            weakAttributes: [
                element.id || "",
                element.getAttribute("name") || "",
                typeof element.className === "string"
                    ? element.className
                    : ""
            ].filter(Boolean).join(" "),

            options: getOptions(element),

            isVisible: isVisible,

            isEnabled:
                !element.disabled &&
                element.getAttribute("aria-disabled") !== "true",

            isActionable: isVisible || !!labelVisible
        };
    });
    """;

    // --------------------------------------------------
    // Refresh candidates
    // --------------------------------------------------

    public void Refresh()
    {
        _candidates.Clear();

        var executor = (IJavaScriptExecutor)_driver;

        object? result =
            executor.ExecuteScript(DiscoveryScript);

        if (result is not IEnumerable rows)
        {
            throw new InvalidOperationException(
                "Unable to collect page elements."
            );
        }

        foreach (var item in rows)
        {
            if (item is not IDictionary<string, object> row)
                continue;

            if (!row.TryGetValue("element", out var elementObject) ||
                elementObject is not IWebElement element)
            {
                continue;
            }

            _candidates.Add(new FieldCandidate
            {
                Element = element,

                Tag = GetString(row, "tag"),
                Type = GetString(row, "type"),
                Role = GetString(row, "role"),

                LabelFor = GetString(row, "labelFor"),
                WrappingLabel = GetString(row, "wrappingLabel"),

                AriaLabelledBy =
                    GetString(row, "ariaLabelledBy"),

                AriaLabel = GetString(row, "ariaLabel"),

                Placeholder = GetString(row, "placeholder"),
                Title = GetString(row, "title"),

                NearbyText = GetString(row, "nearbyText"),
                NearbyDistance = GetInt(row, "nearbyDistance"),

                GroupLabel = GetString(row, "groupLabel"),
                OptionLabel = GetString(row, "optionLabel"),

                Value = GetString(row, "value"),

                WeakAttributes =
                    GetString(row, "weakAttributes"),

                Options = GetStringList(row, "options"),

                IsVisible = GetBool(row, "isVisible"),
                IsEnabled = GetBool(row, "isEnabled"),
                IsActionable = GetBool(row, "isActionable")
            });
        }
    }

    // --------------------------------------------------
    // Resolve First Name
    // --------------------------------------------------

    public FieldResolution ResolveTextInput(string label)
    {
        var candidates = _candidates.Where(c =>
            c.Tag == "input" &&
            (c.Type == "" || c.Type == "text") &&
            c.IsVisible &&
            c.IsEnabled
        );

        return ResolveByLabel(label, candidates);
    }

    // --------------------------------------------------
    // Resolve State dropdown
    // --------------------------------------------------

    public FieldResolution ResolveDropdown(string label)
    {
        var candidates = _candidates.Where(c =>
            c.IsActionable &&
            c.IsEnabled &&
            (
                c.Tag == "select" ||
                c.Role == "combobox" ||
                c.Role == "listbox"
            )
        );

        return ResolveByLabel(label, candidates);
    }

    // --------------------------------------------------
    // Resolve Gender radio option
    // --------------------------------------------------

    public FieldResolution ResolveRadioOption(
        string groupLabel,
        string optionLabel)
    {
        var candidates = _candidates.Where(c =>
            c.IsEnabled &&
            (
                (c.Tag == "input" && c.Type == "radio") ||
                c.Role == "radio"
            ) &&
            c.IsActionable
        );

        var ranked = new List<ScoredCandidate>();

        foreach (var candidate in candidates)
        {
            var groupScore = GetBestSignal(
                groupLabel,
                new[]
                {
                    ("Group label", candidate.GroupLabel, 100),
                    ("Accessible name", candidate.AriaLabelledBy, 95),
                    ("ARIA label", candidate.AriaLabel, 90),
                    (
                        "Nearby text",
                        candidate.NearbyText,
                        NearbyScore(candidate.NearbyDistance)
                    )
                }
            );

            bool optionMatches =
                ExactAny(candidate.OptionLabel, optionLabel) ||
                ExactAny(candidate.Value, optionLabel) ||
                ExactAny(candidate.AriaLabel, optionLabel) ||
                ExactAny(candidate.LabelFor, optionLabel) ||
                ExactAny(candidate.WrappingLabel, optionLabel);

            if (groupScore.Score == 0 || !optionMatches)
                continue;

            ranked.Add(new ScoredCandidate
            {
                Candidate = candidate,
                Score = groupScore.Score + 20,
                Evidence =
                    groupScore.Evidence +
                    " + exact radio option"
            });
        }

        return SelectBest(ranked);
    }

    // --------------------------------------------------
    // General label resolution
    // --------------------------------------------------

    private FieldResolution ResolveByLabel(
        string requestedLabel,
        IEnumerable<FieldCandidate> candidates)
    {
        var ranked = candidates.Select(candidate =>
        {
            var result = GetBestSignal(
                requestedLabel,
                new[]
                {
                    ("Associated label", candidate.LabelFor, 100),
                    ("Wrapping label", candidate.WrappingLabel, 100),
                    ("ARIA labelledby", candidate.AriaLabelledBy, 95),
                    ("ARIA label", candidate.AriaLabel, 90),
                    ("Placeholder", candidate.Placeholder, 75),
                    ("Title", candidate.Title, 70),
                    (
                        "Nearby text",
                        candidate.NearbyText,
                        NearbyScore(candidate.NearbyDistance)
                    )
                }
            );

            int score = result.Score;
            string evidence = result.Evidence;

            if (
                score == 0 &&
                WeakMatch(candidate.WeakAttributes, requestedLabel)
            )
            {
                score = 15;
                evidence = "Weak HTML attribute hint";
            }

            return new ScoredCandidate
            {
                Candidate = candidate,
                Score = score,
                Evidence = evidence
            };
        })
        .Where(x => x.Score > 0)
        .OrderByDescending(x => x.Score)
        .ToList();

        return SelectBest(ranked);
    }

    // --------------------------------------------------
    // Matching helpers
    // --------------------------------------------------

    private static bool ExactAny(
        string actual,
        string expected)
    {
        return actual
            .Split('|')
            .Any(part => Exact(part, expected));
    }

    private static (
        int Score,
        string Evidence
    ) GetBestSignal(
        string requested,
        IEnumerable<(
            string Name,
            string Value,
            int Score
        )> signals)
    {
        var list = signals.ToList();

        foreach (var signal in list)
        {
            if (ExactAny(signal.Value, requested))
            {
                return (signal.Score, signal.Name);
            }
        }

        foreach (var signal in list)
        {
            if (signal.Value
                .Split('|')
                .Any(part => WeakMatch(part, requested)))
            {
                return (
                    signal.Score - 25,
                    signal.Name + " (partial)"
                );
            }
        }

        return (0, "");
    }

    private static int NearbyScore(int distance)
    {
        return Math.Max(56, 90 - distance);
    }

    private static string Normalize(string value)
    {
        string normalized = Regex.Replace(
            value.ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " "
        ).Trim();

        normalized = Regex.Replace(
            normalized,
            @"\b(required|optional)\b",
            ""
        );

        return Regex.Replace(
            normalized,
            @"\s+",
            " "
        ).Trim();
    }

    private static bool Exact(
        string actual,
        string expected)
    {
        return Normalize(actual) == Normalize(expected);
    }

    private static bool WeakMatch(
        string actual,
        string expected)
    {
        string value = " " + Normalize(actual) + " ";
        string target = " " + Normalize(expected) + " ";

        return target.Trim().Length > 0 &&
               value.Contains(target, StringComparison.Ordinal);
    }

    // --------------------------------------------------
    // Confidence and ambiguity
    // --------------------------------------------------

    private static FieldResolution SelectBest(
        List<ScoredCandidate> ranked)
    {
        var top = ranked.Take(3).ToList();

        if (top.Count == 0)
        {
            return new FieldResolution
            {
                Success = false,
                Message = "No matching candidates found.",
                TopCandidates = top
            };
        }

        if (top[0].Score < ConfidenceThreshold)
        {
            return new FieldResolution
            {
                Success = false,
                Score = top[0].Score,
                Message =
                    $"Insufficient confidence. Top score: {top[0].Score}.",
                TopCandidates = top
            };
        }

        if (
            top.Count > 1 &&
            top[0].Score - top[1].Score < 10
        )
        {
            return new FieldResolution
            {
                Success = false,
                Score = top[0].Score,
                Message =
                    "Ambiguous match. Multiple candidates have similar scores.",
                TopCandidates = top
            };
        }

        return new FieldResolution
        {
            Success = true,
            Element = top[0].Candidate.Element,
            Score = top[0].Score,
            Message = "High-confidence match.",
            TopCandidates = top
        };
    }

    // --------------------------------------------------
    // JavaScript result helpers
    // --------------------------------------------------

    private static string GetString(
        IDictionary<string, object> row,
        string key)
    {
        return row.TryGetValue(key, out var value)
            ? Convert.ToString(value) ?? ""
            : "";
    }

    private static int GetInt(
        IDictionary<string, object> row,
        string key)
    {
        return row.TryGetValue(key, out var value) &&
               value != null
            ? Convert.ToInt32(value)
            : 99;
    }

    private static bool GetBool(
        IDictionary<string, object> row,
        string key)
    {
        return row.TryGetValue(key, out var value) &&
               value != null &&
               Convert.ToBoolean(value);
    }

    private static List<string> GetStringList(
        IDictionary<string, object> row,
        string key)
    {
        var result = new List<string>();

        if (
            !row.TryGetValue(key, out var value) ||
            value is not IEnumerable values ||
            value is string
        )
        {
            return result;
        }

        foreach (var item in values)
        {
            if (item != null)
                result.Add(item.ToString() ?? "");
        }

        return result;
    }
}
