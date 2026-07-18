# Red-error capture as an independent, automatic framework feature

A test's `Run`/`Check` delegates can pass their own assertions while the mod under test (or some
other loaded mod) still fires a `Log.Error` or throws an exception that RimWorld swallows and
red-prints - today that's invisible to Insight, which only reacts to exceptions its own step
delegates throw. We're adding a capture mechanism that hooks `Application.logMessageReceivedThreaded`
for the whole session, buckets what it sees into **Loading Errors** (a snapshot of
`Verse.Log.Messages` taken in `TestRegistry`'s static constructor - the earliest point Insight code
runs, well before the tester ever opens the window), **Test-Attributed Errors** (whatever step is
active when the log line fires), and **Other/Idle Errors** (everything else). It runs automatically
for every suite with no opt-in, the same way RNG-stubbing already does, and it never changes a
step's PASS/FAIL outcome - a passing step with an error becomes `PASSED_WITH_ERRORS`, a failing step
stays `FAILED` with the error as attached detail.

## Considered Options

- **Shared library with SmashDevTools' `LogWatcher`** (rejected): SmashDevTools already solves
  almost the same problem the same way (subscribing to `Application.logMessageReceivedThreaded`,
  scoped per-test via `IDisposable`). We chose not to extract a common assembly - the two frameworks'
  execution models diverge enough (interactive/manual vs. unattended/automated, and Insight never
  fails a test over a captured error where SmashDevTools does) that a shared abstraction would mostly
  be indirection, and it would commit both mods to versioning and referencing a new shared dependency
  for a fairly small chunk of logic. Conceptual duplication between the two frameworks is the accepted
  cost.
- **Snapshot loading errors at first window open** (rejected): simpler to reason about since it's
  tied to a lifecycle event Insight already controls (the window's own construction), but the runner
  window opens whenever a dev happens to pick it from the debug menu - possibly long after game/mod
  load finished. Snapshotting at `TestRegistry`'s static constructor instead captures genuine load-time
  errors regardless of how long the dev waits before opening the runner.

## Consequences

- `ITestProvider` gets a new optional member (default-implemented empty) for regex suppression
  patterns; a kit only overrides it if it has known noise to silence, and suppression applies across
  all three buckets, not just that kit's own tests.
- Any log line Insight itself produces with its existing `[Insight] ` prefix is always excluded from
  capture, so the runner's own narration (a step-threw message, an internal framework failure) is
  never double-counted alongside the FAILED status it already produces.
- Rerunning a step/script/suite clears and replaces its previously captured errors - errors reflect
  only the most recent run, matching how results and the report file already behave.
- Loading and Other/Idle errors persist to one shared `_diagnostics.json` per session; Test-Attributed
  errors persist inside that test's own suite `.json`/`.log` alongside its step result. If this
  feature is ever ported to SmashDevTools, expect to re-derive rather than reuse this ADR's reasoning,
  since SmashDevTools already made different calls (fail-on-error, per-fixture `IDisposable` scoping).
