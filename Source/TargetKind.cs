namespace TestRunner
{
    // What a suite runs against. Drives how the runner acquires the target and what the context holds.
    public enum TargetKind
    {
        None,      // no subject; map-level (the script spawns/inspects whatever it needs)
        Pawn,      // a clicked pawn
        Building,  // a clicked building
        Item,      // a clicked item
        Cell,      // a clicked map cell
    }
}
