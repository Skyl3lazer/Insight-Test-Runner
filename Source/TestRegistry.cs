using System;
using System.Collections.Generic;
using Verse;

namespace TestRunner
{
    [StaticConstructorOnStartup]
    public static class TestRegistry
    {
        private static readonly List<TestSuite> Suites = new List<TestSuite>();

        static TestRegistry()
        {
            foreach (Type t in GenTypes.AllTypes)
            {
                if (t.IsAbstract || t.IsInterface || !typeof(ITestProvider).IsAssignableFrom(t))
                {
                    continue;
                }

                try
                {
                    ITestProvider provider = (ITestProvider)Activator.CreateInstance(t);
                    foreach (TestSuite suite in provider.GetSuites())
                    {
                        Register(suite);
                    }
                }
                catch (Exception e)
                {
                    Log.Error("[TestRunner] provider " + t + " failed: " + e);
                }
            }

            Log.Message("[TestRunner] registered " + Suites.Count + " test suite(s).");
        }

        public static IReadOnlyList<TestSuite> AllSuites => Suites;

        public static void Register(TestSuite suite)
        {
            if (suite != null && !Suites.Contains(suite))
            {
                Suites.Add(suite);
            }
        }
    }
}
