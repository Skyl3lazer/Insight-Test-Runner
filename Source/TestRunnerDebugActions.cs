using LudeonTK;
using Verse;

namespace TestRunner
{
    public static class TestRunnerDebugActions
    {
        [DebugAction("Testing", "Open test runner", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenRunner(Pawn p) => Find.WindowStack.Add(new Window_TestRunner(p));
    }
}
