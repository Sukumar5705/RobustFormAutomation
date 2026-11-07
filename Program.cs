
using System;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

internal class Program
{
    private const string DefaultUrl =
        "https://app.cloudqa.io/home/AutomationPracticeForm";

    private static int Main(string[] args)
    {

        string targetUrl =
            ReadOption(args, "--url") ??
            ReadPositional(args) ??
            Environment.GetEnvironmentVariable("FORM_URL") ??
            DefaultUrl;

        // Convert a local file path into a file:// URL.
        if (File.Exists(targetUrl))
        {
            targetUrl = new Uri(
                Path.GetFullPath(targetUrl)
            ).AbsoluteUri;
        }


        string firstName =
            ReadOption(args, "--first-name") ?? "Sukumar";

        string gender =
            ReadOption(args, "--gender") ?? "Male";
        string? state = ReadOption(args, "--state");

        bool headless = args.Contains(
            "--headless",
            StringComparer.OrdinalIgnoreCase
        );

        bool keepOpen = args.Contains(
            "--keep-open",
            StringComparer.OrdinalIgnoreCase
        );


        if (!Uri.TryCreate(
                targetUrl,
                UriKind.Absolute,
                out Uri? uri) ||
            (
                uri.Scheme != Uri.UriSchemeHttp &&
                uri.Scheme != Uri.UriSchemeHttps &&
                uri.Scheme != Uri.UriSchemeFile
            ))
        {
            Console.WriteLine("Invalid website URL.");
            return 1;
        }


        IWebDriver? driver = null;

        try
        {
            var options = new ChromeOptions();

            if (headless)
            {
                options.AddArgument("--headless=new");
                options.AddArgument("--window-size=1920,1080");
            }
            else
            {
                options.AddArgument("--start-maximized");
            }

            Console.WriteLine("Launching Chrome...");

            driver = new ChromeDriver(options);

            driver.Manage().Timeouts().PageLoad =
                TimeSpan.FromSeconds(30);

            driver.Navigate().GoToUrl(uri);

            var wait = new WebDriverWait(
                driver,
                TimeSpan.FromSeconds(20)
            );

            wait.Until(d =>
                ((IJavaScriptExecutor)d)
                    .ExecuteScript("return document.readyState")
                    ?.ToString() == "complete"
            );

            Console.WriteLine("\nWebsite loaded successfully.");
            Console.WriteLine($"URL: {driver.Url}");

            Console.WriteLine("\nTest Configuration:");
            Console.WriteLine($"First Name: {firstName}");
            Console.WriteLine($"Gender: {gender}");
            Console.WriteLine($"State: {state ?? "(first available option)"}");

            var formTests = new FormTests(driver);

            var results = formTests.RunAll(
                firstName,
                gender,
                state
            );

            Report.Print(results);

            int exitCode = Report.GetExitCode(results);

            if (keepOpen)
            {
                Console.WriteLine(
                    "\nPress ENTER to close the browser."
                );

                Console.ReadLine();
            }

            return exitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"\nAutomation error: {ex.Message}"
            );

            return 1;
        }
        finally
        {
            driver?.Quit();
            driver?.Dispose();
        }
    }

    private static string? ReadOption(
        string[] args,
        string option)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(
                args[i],
                option,
                StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static readonly string[] ValueOptions =
    {
    "--url",
    "--first-name",
    "--gender",
    "--state"
};

    private static string? ReadPositional(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                if (ValueOptions.Contains(
                        args[i],
                        StringComparer.OrdinalIgnoreCase))
                {
                    i++;
                }

                continue;
            }

            return args[i];
        }

        return null;
    }

}
