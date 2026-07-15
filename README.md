# Insight - Test Runner

A generic, dev-only **in-game test harness** for RimWorld mods. Any mod ships a small "test kit"
that registers **suites** of ordered **steps**; you open the runner on a pawn and step through them,
watching the real game react. Steps can **self-check** (PASS/FAIL) or be human-judged, and every run
writes a per-suite log + JSON report.

It holds no mod-specific content - it is the framework other mods plug into.

## Why

RimWorld has no built-in assertion framework and much of what a mod does (UI, feel, emergent state)
can only be judged against the running game. Insight gives you a repeatable, scriptable way to drive
a mod's behavior on a live pawn, assert the parts that are machine-checkable, and get a written
record of each run - so you (or a tool reading the report) can tell what passed and what broke.

## Install

Dev tool - not for the Workshop. Enable it in your mod list; consuming test kits load after it.

## Using it

1. Enable **Insight - Test Runner**, the mod under test, and that mod's test kit (kits load after both).
2. Dev mode on -> debug actions -> category **`Testing`** -> **`Open test runner`** -> click a pawn.
3. Pick a suite's script and step through:
   - **Run & Next** runs the current step and evaluates its check; **Run all** runs the whole script.
   - **Prev / Restart / Suites** navigate. **Save report** writes without a full run.
   - **Seeded RNG** makes random outcomes repeat run to run; turn it off to sample variance.
4. Output lands in `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Insight\`:
   - `<suite>.json` - latest run, structured (for tooling).
   - `<suite>.log` - timestamped step narration; overwrites on each game launch, appends within it.
   Genuine exceptions also surface in `Player.log`.

## Writing a test kit for your mod

See **[CREATING-A-TEST-KIT.md](CREATING-A-TEST-KIT.md)** - a complete, self-contained recipe: the
mod layout, project files, the `ITestProvider` + `TestSuite` pattern, the step/assert API, and a
worked example. It assumes no prior context.

## Public API (namespace `TestRunner`, assembly `TestRunner.dll`)

- `ITestProvider` - implement it (public parameterless ctor); auto-discovered at startup.
- `TestRegistry.Register(TestSuite)` - manual registration alternative.
- `TestSuite(name, List<TestScript> scripts, Func<Pawn,string>? describe)`
- `TestScript` / `TestStep` and the `Test.Do/Note/Check/Script` builders.
- `CheckResult` and the `Assert` helpers.
