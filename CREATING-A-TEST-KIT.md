# Creating a test kit

A complete, self-contained recipe for building an Insight test kit for a RimWorld mod. It assumes
**no prior context** beyond: you know the target mod, you can build a C# RimWorld assembly, and
`TestRunner.dll` (this framework) is present. Follow it top to bottom.

---

## 1. The model (what you are building)

- A **test kit** is a *separate, dev-only mod* that depends on (a) the mod under test and (b)
  Insight - Test Runner. It ships nothing into the target mod, so the target mod stays release-clean.
- The kit registers one or more **suites**. A `TestSuite` = a name + a **`TargetKind`** + a list of
  scripts + an optional `Describe(TestContext)` delegate (a one-line state dump for logs/reports).
- **`TargetKind`** = what the suite runs against: `None` (map-level; the script spawns/inspects what
  it needs), `Pawn`, `Building`, `Item`, or `Cell`. It decides what the runner asks the tester to
  click before running.
- A **script** (`TestScript`) = a name + an ordered list of **steps**.
- A **step** (`TestStep`) = an instruction + an `Action<TestContext>` to run + an optional
  `Func<TestContext, CheckResult>` self-check. No check => human-judged (UI/feel, or a manual note).
  A step may also carry an optional **stub plan** (`.Stub(...)`) that forces specific `Rand` results.
- A **`TestContext`** is handed to every step: `Map` (always), the selected target for the kind -
  `ctx.Pawn`, `ctx.Thing` (building/item), or `ctx.Cell` - and `ctx.Rng` (the active `RngMode`:
  `Real` / `Seeded` / `Stubbed`), so a check can adapt to the run mode.
- The runner offers three **RNG modes**: `Real` (live), `Seeded` (fixed per-step seed, repeatable),
  and `Stubbed` (a step's `.Stub(...)` values are forced; unplanned steps/calls fall back to seeded).
- Insight discovers your suite automatically (you implement `ITestProvider`), shows it in the runner,
  has the tester select the target, drives the steps, and writes `Insight\<suite>.json` + `.log`.

Golden rule: **drive the target mod through its public API only.** If a capability you need is not
public, add a thin public method to the target mod (or an ops class in the kit that calls existing
public members) - never reach into internals.

---

## 2. Mod folder layout

Create a new mod folder next to the target mod, e.g. `Mods\<Target> Test Kit\`:

```
<Target> Test Kit\
  About\About.xml
  1.6\Assemblies\            <- build output lands here (committed so it runs when cloned)
  Source\
    Directory.Build.props
    <Target>TestKit.csproj
    <Target>Ops.cs           <- optional: parameterized ops that drive the target mod
    <Target>TestProvider.cs  <- ITestProvider: your suite + scripts
```

### About\About.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <packageId>YourName.<Target>TestKit</packageId>
  <name><Target> Test Kit</name>
  <author>YourName</author>
  <description>Dev-only Insight test kit for <Target>. Not for release.</description>
  <supportedVersions><li>1.6</li></supportedVersions>
  <modDependencies>
    <li><packageId>Author.<Target></packageId><displayName><Target></displayName></li>
    <li><packageId>Skyl3lazer.TestRunner</packageId><displayName>Insight - Test Runner</displayName></li>
  </modDependencies>
  <loadAfter>
    <li>Author.<Target></li>
    <li>Skyl3lazer.TestRunner</li>
  </loadAfter>
</ModMetaData>
```

### Source\Directory.Build.props

Puts the built DLL in `1.6\Assemblies` and sets the assembly/namespace.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <PropertyGroup>
    <RootNamespace><Target>TestKit</RootNamespace>
    <AssemblyName><Target>TestKit</AssemblyName>
    <OutDir>..\1.6\Assemblies</OutDir>
    <BaseIntermediateOutputPath>obj\$(MSBuildProjectName)\</BaseIntermediateOutputPath>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <LangVersion>latestMajor</LangVersion>
    <Nullable>enable</Nullable>
    <TargetType>library</TargetType>
    <DebugType>none</DebugType>
  </PropertyGroup>
</Project>
```

### Source\<Target>TestKit.csproj

`Krafs.Rimworld.Ref` supplies the game assemblies; add references to the target mod's DLL and to
`TestRunner.dll`. `Private=false` means those DLLs are provided by their own mods at runtime, not
copied here.

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Krafs.Rimworld.Ref" Version="1.6.4488-beta" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="<Target>">
      <HintPath>..\..\<Target>\1.6\Assemblies\<Target>.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="TestRunner">
      <HintPath>..\..\Insight Test Runner\1.6\Assemblies\TestRunner.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

(The Insight mod's folder is `Insight Test Runner`; the assembly/DLL is `TestRunner.dll` and the
packageId is `Skyl3lazer.TestRunner`.)

---

## 3. (Optional) an ops class

Not required, but strongly recommended once you have more than a couple of scripts: a static class of
small, parameterized verbs that drive the target mod's public API and read its state. Scripts call
these; it keeps scripts short and DRY, and it is the seam a future automated suite reuses.

```csharp
using TestRunner;
using Verse;
using <Target>;                 // the target mod's namespace

namespace <Target>TestKit
{
    public static class <Target>Ops
    {
        // Verbs: mutate state via the target mod's PUBLIC API.
        public static void DoTheThing(Pawn p, int amount) { /* call public API */ }

        // Accessors: read state for checks.
        public static int SomeCount(Pawn p) => /* ... */ 0;

        // Describe: one-line dump for logs/report. Reads the TestContext field for the suite's
        // TargetKind (ctx.Pawn / ctx.Thing / ctx.Cell).
        public static string Describe(TestContext ctx) =>
            ctx.Pawn != null ? ctx.Pawn.LabelShort + ": someCount=" + SomeCount(ctx.Pawn) : "(none)";
    }
}
```

---

## 4. The provider: register your suite

Implement `ITestProvider` (public, parameterless ctor). Insight finds it automatically. Return one
`TestSuite` per logical area (usually one). Use `using static TestRunner.Test;` to write the builders
bare (`Do`, `Note`, `Check`, `Script`).

```csharp
using System.Collections.Generic;
using TestRunner;
using Verse;
using static TestRunner.Test;

namespace <Target>TestKit
{
    public class <Target>TestProvider : ITestProvider
    {
        public IEnumerable<TestSuite> GetSuites()
        {
            // Pick the TargetKind your scripts need: None / Pawn / Building / Item / Cell.
            yield return new TestSuite("<Target>", TargetKind.Pawn, Scripts, <Target>Ops.Describe);
        }

        private static readonly List<TestScript> Scripts = new List<TestScript>
        {
            Script("01 First scenario",
                Do("Set up: do the thing (amount 5)", ctx => <Target>Ops.DoTheThing(ctx.Pawn, 5),
                    ctx => Assert.Eq(<Target>Ops.SomeCount(ctx.Pawn), 5, "someCount")),
                Note("Look at the target's tab: confirm the widget shows 5 (human-judged)."),
                Do("Reset / clean up", ctx => <Target>Ops.DoTheThing(ctx.Pawn, 0))),
        };
    }
}
```

Steps receive the `TestContext`; read the field for your kind (`ctx.Pawn` / `ctx.Thing` / `ctx.Cell`).
For a single-kind suite you can keep step bodies terse with a tiny adapter - see the Psyche kit's
`DoP` / `CheckP`, which wrap `ctx.Pawn` so scripts read `p => ...`.

---

## 5. The step + assertion API (reference)

All in namespace `TestRunner`.

`TargetKind` (enum): `None`, `Pawn`, `Building`, `Item`, `Cell`.

`TestContext` (handed to every step): `Map Map`, `TargetKind Kind`, `Pawn? Pawn`, `Thing? Thing`
(building/item), `IntVec3 Cell`, `RngMode Rng` (Real / Seeded / Stubbed - the active run mode).

Builders (`Test` static class):
- `Do(string instruction, Action<TestContext> op)` - run an action; logs a dump.
- `Do(string instruction, Action<TestContext> op, Func<TestContext,CheckResult> check)` - + self-check.
- `Check(string instruction, Func<TestContext,CheckResult> check)` - assertion only, no state change.
- `Note(string instruction)` - human-judged only (UI/feel or a manual setup prerequisite).
- `Script(string name, params TestStep[] steps)` - assemble a script.

Combine multiple ops in one step with a lambda: `ctx => { OpA(ctx.Pawn); OpB(ctx.Pawn); }`.

Assertions (`Assert` static class) - each returns a `CheckResult`:
- `True(bool cond, string label)`
- `Eq(int actual, int expected, string label)`
- `InRange(int actual, int lo, int hi, string label)`
- `Approx(float actual, float expected, float tol, string label)`
- `Less(float actual, float bound, string label)`
- `AtMost(float actual, float bound, string label)`
- `All(params CheckResult[] results)` - passes only if all pass; details concatenated.

For custom logic a check returns `CheckResult.Ok("detail")` / `CheckResult.Fail("detail")` directly.

Forcing RNG (`RngMode.Stubbed` only) - attach a plan to a step and it is applied while that step's op
runs; other modes ignore it:
- `step.Stub(r => r.Chance(true, false).Value(0.1f).Range(5f).RangeInt(3))` - fluent per `Rand` entry
  point. Each method has its own ordered queue consumed in call order: `Chance` -> `Rand.Chance`,
  `Value` -> `Rand.Value`, `Range` -> `Rand.Range(float,float)`, `RangeInt` -> `Rand.Range(int,int)`.
- Declare **exactly** the calls the driven code path makes on that method. A leftover value (declared
  but not consumed) or an extra call past the declared count **fails the step** ("stub drift"), which
  catches a shift in the target mod's call order rather than silently exercising the wrong branch.
- Only stub calls that are actually reached; a roll behind a gate that the op skips will drift.
- Because the check runs in every mode, make it mode-aware: `ctx.Rng == RngMode.Stubbed ?
  Assert.Eq(...) : Assert.InRange(...)` so Run-all is green under Real/Seeded too.

`TestSuite(string name, TargetKind targetKind, List<TestScript> scripts, Func<TestContext,string>? describe = null)`
- `describe` is the per-step state dump; pass `null` for no dumps.

---

## 6. Build order and running

Build in dependency order (each writes its DLL into its own `1.6\Assemblies`):

1. the target mod, 2. `TestRunner` (already built), 3. your test kit.

`dotnet build "<Target> Test Kit\Source\<Target>TestKit.csproj"`

If the copy step fails with "user-mapped section open", RimWorld is running and holding the old DLL -
exit the game (or hot-reload) and rebuild.

To run: enable target + Insight + kit (kit after both), dev mode on, **Testing -> Open test runner**
(a global action - no click needed to open). Pick your suite's script; if its `TargetKind` is not
`None`, hit **Select target** and click the pawn/building/item/cell (the click is filtered to the
kind); then **Run script** (all steps of the current script) or **Run suite** (every script in the
suite, in order, on the same target - use this for a full unattended pass; it also runs any
destructive scripts, so pick a disposable target). Run suite writes the combined report, shows the
aggregate tally in the window, and logs it. Read `Insight\<suite>.json` (and `.log`) in the LocalLow
RimWorld folder, and the target's tab for the human-judged steps.

The report is suite-level: top-level `suite` / `kind` / `target` / `rngMode`, an aggregate `summary`,
and a `scripts` array - each entry has its own `script` name, `summary`, and `steps`. A single-script
run writes the same shape with one entry; Run suite writes them all. The file overwrites each run.

---

## 7. Conventions that keep tests trustworthy

- **Test shipped behavior, not the roadmap.** A check that encodes an unbuilt design fails forever
  and trains you to ignore red. Only assert what the code does now; note unbuilt areas in your kit's
  docs.
- **Assert invariants/relations, not tuning constants.** `x < baseline`, `count == 1`,
  `severity <= magnitude * scale` survive rebalancing; `x == 3.47` does not.
- **Isolate.** Start (and end) scripts with a reset step that returns the pawn to a clean state, and
  clear anything your ops leave behind - stale state leaking between scripts is the most common false
  failure.
- **Prefer deterministic ops for checked steps.** Where the target mod rolls RNG, either use a forced
  variant, assert a *range* (`InRange`), rely on Seeded mode, or force the exact branch with
  `.Stub(...)` in Stubbed mode; say so in the instruction and run Real occasionally to sample variance.
- **Human-judge what has no hook.** UI appearance, "feel", and anything you cannot read as a value
  stays a `Note`; do not fake a check for it.
- **One suite per kit** is the norm; split into multiple suites only when areas are genuinely
  separate. Output files are named by suite.
