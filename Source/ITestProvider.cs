using System.Collections.Generic;

namespace TestRunner
{
    // Implement this (with a public parameterless constructor) in a consumer mod's assembly; the
    // Test Runner auto-discovers all implementors at startup and registers their suites.
    public interface ITestProvider
    {
        IEnumerable<TestSuite> GetSuites();
    }
}
