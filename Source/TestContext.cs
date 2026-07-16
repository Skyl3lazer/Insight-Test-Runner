using Verse;

namespace TestRunner
{
    public class TestContext
    {
        public Map Map = null!;
        public TargetKind Kind;
        public Pawn? Pawn;
        public Thing? Thing;   // set for Building and Item kinds
        public IntVec3 Cell = IntVec3.Invalid;
    }
}
