# Creating a test kit

A complete, self-contained recipe for building an Insight test kit for a RimWorld mod. It assumes
**no prior context** beyond: you know the target mod, you can build a C# RimWorld assembly, and
`TestRunner.dll` (this framework) is present. Follow it top to bottom.

---

## 1. The model (what you are building)

- A **test kit** is a *separate, dev-only mod* that depends on (a) the mod under test and (b)
  Insight - Test Runner. It ships nothing into the target mod, so the target mod stays release-clean.
- The kit registers one or more **suites**. A `TestSuite` = a name + a list of scripts + an optional
  `Describe(pawn)` delegate (a one-line state dump used in logs/reports).
- A **script** (`TestScript`) = a name + an ordered list of **steps**.
- A **step** (`TestStep`) = an instruction string + an `Action<Pawn>` to run + an optional
  `Func<Pawn, CheckResult>` self-check. No check => the step is human-judged (UI/feel, or a manual
  prerequisite note).
- Insight discovers your suite automatically (you implement `ITestProvider`), shows it in the runner,
  and drives the steps against a clicked pawn, writing `Insight\<suite>.json` + `.log`.

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
      <HintPath>..\..\Test Runner\1.6\Assemblies\TestRunner.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

(The Insight mod's folder is `Test Runner` even though it displays as "Insight - Test Runner"; the
DLL is `TestRunner.dll`.)

---

## 3. (Optional) an ops class

Not required, but strongly recommended once you have more than a couple of scripts: a static class of
small, parameterized verbs that drive the target mod's public API and read its state. Scripts call
these; it keeps scripts short and DRY, and it is the seam a future automated suite reuses.

```csharp
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

        // Describe: the one-line dump shown in logs/report for each step.
        public static string Describe(Pawn p) =>
            p.LabelShort + ": someCount=" + SomeCount(p);
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
            yield return new TestSuite("<Target>", Scripts, <Target>Ops.Describe);
        }

        private static readonly List<TestScript> Scripts = new List<TestScript>
        {
            Script("01 First scenario",
                Do("Set up: do the thing (amount 5)", p => <Target>Ops.DoTheThing(p, 5),
                    p => Assert.Eq(<Target>Ops.SomeCount(p), 5, "someCount")),
                Note("Look at the pawn's tab: confirm the widget shows 5 (human-judged)."),
                Do("Reset / clean up", p => <Target>Ops.DoTheThing(p, 0))),
        };
    }
}
```

---

## 5. The step + assertion API (reference)

All in namespace `TestRunner`.

Builders (`Test` static class):
- `Do(string instruction, Action<Pawn> op)` - run an action; logs a dump.
- `Do(string instruction, Action<Pawn> op, Func<Pawn,CheckResult> check)` - action + self-check.
- `Check(string instruction, Func<Pawn,CheckResult> check)` - assertion only, no state change.
- `Note(string instruction)` - human-judged only (UI/feel or a manual setup prerequisite).
- `Script(string name, params TestStep[] steps)` - assemble a script.

Combine multiple ops in one step with a lambda: `p => { OpA(p); OpB(p); }`.

Assertions (`Assert` static class) - each returns a `CheckResult`:
- `True(bool cond, string label)`
- `Eq(int actual, int expected, string label)`
- `InRange(int actual, int lo, int hi, string label)`
- `Approx(float actual, float expected, float tol, string label)`
- `Less(float actual, float bound, string label)`
- `AtMost(float actual, float bound, string label)`
- `All(params CheckResult[] results)` - passes only if all pass; details concatenated.

For custom logic a check returns `CheckResult.Ok("detail")` / `CheckResult.Fail("detail")` directly.

`TestSuite(string name, List<TestScript> scripts, Func<Pawn,string>? describe = null)` - `describe`
is the per-step state dump; pass `null` if you do not want dumps.

---

## 6. Build order and running

Build in dependency order (each writes its DLL into its own `1.6\Assemblies`):

1. the target mod, 2. `TestRunner` (already built), 3. your test kit.

`dotnet build "<Target> Test Kit\Source\<Target>TestKit.csproj"`

If the copy step fails with "user-mapped section open", RimWorld is running and holding the old DLL -
exit the game (or hot-reload) and rebuild.

To run: enable target + Insight + kit (kit after both), dev mode on, **Testing -> Open test runner**,
click a pawn, pick your suite's script, **Run all**. Read `Insight\<suite>.json` (and `.log`) in the
LocalLow RimWorld folder, and the pawn's tab for the human-judged steps.

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
  variant, assert a *range* (`InRange`), or rely on the runner's seeded mode; say so in the
  instruction and turn seed off occasionally to sample variance.
- **Human-judge what has no hook.** UI appearance, "feel", and anything you cannot read as a value
  stays a `Note`; do not fake a check for it.
- **One suite per kit** is the norm; split into multiple suites only when areas are genuinely
  separate. Output files are named by suite.
