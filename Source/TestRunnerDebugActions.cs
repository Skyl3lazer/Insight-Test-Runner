using LudeonTK;
using Verse;

namespace TestRunner
{
    public static class TestRunnerDebugActions
    {
        // Global entry - no target needed to open; the runner acquires a suite's target inside.
        [DebugAction("Testing", "Open test runner", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenRunner() => Find.WindowStack.Add(new Window_TestRunner());
    }
}
