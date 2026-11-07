
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class TestResult
{
    public string Name { get; }
    public bool Passed { get; }
    public string Message { get; }

    public TestResult(
        string name,
        bool passed,
        string message)
    {
        Name = name;
        Passed = passed;
        Message = message;
    }
}

public static class Report
{
    public static void Print(
        IEnumerable<TestResult> results)
    {
        var tests = results.ToList();

        int passed = tests.Count(t => t.Passed);
        int failed = tests.Count - passed;

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("       ROBUST FORM AUTOMATION");
        Console.WriteLine("========================================");

        foreach (var test in tests)
        {
            Console.WriteLine();

            Console.WriteLine(
                $"[{(test.Passed ? "PASS" : "FAIL")}] {test.Name}"
            );

            Console.WriteLine($"       {test.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("----------------------------------------");
        Console.WriteLine($"Total Tests : {tests.Count}");
        Console.WriteLine($"Passed      : {passed}");
        Console.WriteLine($"Failed      : {failed}");

        Console.WriteLine("----------------------------------------");

        Console.WriteLine(
            $"FINAL STATUS: {(failed == 0 ? "SUCCESS" : "FAILED")}"
        );

        Console.WriteLine("========================================");
    }

    public static int GetExitCode(
        IEnumerable<TestResult> results)
    {
        return results.All(t => t.Passed) ? 0 : 1;
    }
}
