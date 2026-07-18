using System.Collections.Generic;

namespace TestRunner
{
    // Auto-discovered at startup; needs a public parameterless ctor. Or use TestRegistry.Register.
    public interface ITestProvider
    {
        IEnumerable<TestSuite> GetSuites();

        // Regex patterns matched against captured red-error messages to silence known noise.
        // Applies to loading, test-attributed, and other/idle errors alike, not just this kit's own tests.
        IEnumerable<string> SuppressedLogPatterns { get; }
    }
}
