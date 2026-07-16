using HarmonyLib;
using Verse;

namespace TestRunner
{
    [StaticConstructorOnStartup]
    public static class TestRunnerStartup
    {
        static TestRunnerStartup() => new Harmony("Skyl3lazer.TestRunner").PatchAll();
    }
}
