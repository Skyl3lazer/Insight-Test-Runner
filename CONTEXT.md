# Insight Test Runner

A dev-only, interactive test harness for RimWorld mods. Consuming mods ship a separate "test kit"
mod that registers suites of steps to run live in-game against a selected pawn/building/item/cell,
with results shown in a debug-menu window and persisted to a report.

## Language

**Captured Error**:
A `Log.Error` call or exception seen by Insight's session-wide log hook, deduplicated by message
and stack trace within a single test's run and shown with an occurrence count. Excludes anything
Insight itself logs with its own `[Insight] ` prefix.
_Avoid_: Log entry, error message

**Loading Errors**:
The bucket of Captured Errors present in `Verse.Log.Messages` at the moment `TestRegistry`'s static
constructor runs - the earliest point Insight code exists, capturing game/mod/def load, independent
of when the runner window is later opened.
_Avoid_: Startup errors, boot errors

**Test-Attributed Errors**:
The bucket of Captured Errors that fire while a specific step is actively executing; attributed down
to that step regardless of the step's own kind (checked, action-only, or human-judged).
_Avoid_: Step errors

**Other/Idle Errors**:
The bucket of Captured Errors that are neither a Loading Error nor attributed to an active step -
anything else during the session, such as idle play between runs or unrelated mods.
_Avoid_: Miscellaneous errors, uncategorized errors

**PASSED_WITH_ERRORS**:
The result status for a step whose own check passed but which had at least one Test-Attributed Error
during its run. Distinct from plain `PASS` in suite summary counts. A failing step never gets an
equivalent `FAILED_WITH_ERRORS` - it stays `FAILED`, with Captured Errors attached only as detail.
_Avoid_: Warning status, flagged pass

**Suppression Pattern**:
A regex a test kit supplies (via an optional `ITestProvider` member) to silence known-noise Captured
Errors by matching their message. Applies across all three buckets, not just that kit's own tests.
_Avoid_: Whitelist, ignore rule
