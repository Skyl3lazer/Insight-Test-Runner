using LudeonTK;
using Verse;

namespace TestRunner
{
    public static class TestRunnerDebugActions
    {
        [DebugAction("Testing", "Open test runner", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenRunner() => Find.WindowStack.Add(new Window_TestRunner());
    }
}
