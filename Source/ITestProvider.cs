using System.Collections.Generic;

namespace TestRunner
{
    // Auto-discovered at startup; needs a public parameterless ctor. Or use TestRegistry.Register.
    public interface ITestProvider
    {
        IEnumerable<TestSuite> GetSuites();
    }
}
