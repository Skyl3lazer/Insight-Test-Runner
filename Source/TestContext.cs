using Verse;

namespace TestRunner
{
    // The per-run bundle handed to every step: the map (always) plus the selected target, according
    // to the suite's TargetKind. A step reads the field for its kind (Pawn / Thing / Cell); the others
    // are null/invalid.
    public class TestContext
    {
        public Map Map = null!;
        public TargetKind Kind;
        public Pawn? Pawn;
        public Thing? Thing;   // set for Building and Item kinds
        public IntVec3 Cell = IntVec3.Invalid;
    }
}
