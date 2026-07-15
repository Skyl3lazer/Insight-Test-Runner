using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TestRunner
{
    // Result of a step's optional self-check.
    public readonly struct CheckResult
    {
        public readonly bool Pass;
        public readonly string Detail;

        public CheckResult(bool pass, string detail)
        {
            Pass = pass;
            Detail = detail;
        }

        public static CheckResult Ok(string detail = "") => new CheckResult(true, detail);

        public static CheckResult Fail(string detail) => new CheckResult(false, detail);
    }

    // A single manual step: an action driven on the target pawn, what to observe, and an optional
    // self-check. Check == null means the step is human-judged only (UI/feel, or a manual note).
    public class TestStep
    {
        public readonly string Instruction;
        public readonly Action<Pawn> Run;
        public readonly Func<Pawn, CheckResult>? Check;

        public TestStep(string instruction, Action<Pawn> run, Func<Pawn, CheckResult>? check = null)
        {
            Instruction = instruction;
            Run = run;
            Check = check;
        }
    }

    // An ordered, named manual scenario.
    public class TestScript
    {
        public readonly string Name;
        public readonly List<TestStep> Steps;

        public TestScript(string name, List<TestStep> steps)
        {
            Name = name;
            Steps = steps;
        }
    }

    // A mod's group of scripts. Describe supplies the per-mod state dump used in logs and the report;
    // if null, dumps are blank (the runner still works).
    public class TestSuite
    {
        public readonly string Name;
        public readonly List<TestScript> Scripts;
        public readonly Func<Pawn, string>? Describe;

        public TestSuite(string name, List<TestScript> scripts, Func<Pawn, string>? describe = null)
        {
            Name = name;
            Scripts = scripts;
            Describe = describe;
        }
    }

    // Fluent builders. Consumers do `using static TestRunner.Test;` and write Do/Note/Check/Script.
    public static class Test
    {
        public static TestStep Do(string instruction, Action<Pawn> op) => new TestStep(instruction, op);

        public static TestStep Do(string instruction, Action<Pawn> op, Func<Pawn, CheckResult> check) => new TestStep(instruction, op, check);

        public static TestStep Check(string instruction, Func<Pawn, CheckResult> check) => new TestStep(instruction, _ => { }, check);

        public static TestStep Note(string instruction) => new TestStep(instruction, _ => { });

        public static TestScript Script(string name, params TestStep[] steps) => new TestScript(name, steps.ToList());
    }

    // Assertion helpers for step checks. Prefer invariant/relational asserts over exact tuning
    // constants so tests survive rebalancing.
    public static class Assert
    {
        public static CheckResult True(bool cond, string label) =>
            cond ? CheckResult.Ok(label) : CheckResult.Fail(label + " (expected true)");

        public static CheckResult Eq(int actual, int expected, string label) =>
            actual == expected ? CheckResult.Ok(label + "=" + actual) : CheckResult.Fail(label + "=" + actual + " expected " + expected);

        public static CheckResult InRange(int actual, int lo, int hi, string label) =>
            actual >= lo && actual <= hi ? CheckResult.Ok(label + "=" + actual) : CheckResult.Fail(label + "=" + actual + " not in [" + lo + "," + hi + "]");

        public static CheckResult Approx(float actual, float expected, float tol, string label) =>
            Mathf.Abs(actual - expected) <= tol
                ? CheckResult.Ok(string.Format("{0}={1:0.##}", label, actual))
                : CheckResult.Fail(string.Format("{0}={1:0.##} expected ~{2:0.##}", label, actual, expected));

        public static CheckResult Less(float actual, float bound, string label) =>
            actual < bound
                ? CheckResult.Ok(string.Format("{0}={1:0.##}<{2:0.##}", label, actual, bound))
                : CheckResult.Fail(string.Format("{0}={1:0.##} !< {2:0.##}", label, actual, bound));

        public static CheckResult AtMost(float actual, float bound, string label) =>
            actual <= bound + 0.0001f
                ? CheckResult.Ok(string.Format("{0}={1:0.##}<={2:0.##}", label, actual, bound))
                : CheckResult.Fail(string.Format("{0}={1:0.##} !<= {2:0.##}", label, actual, bound));

        public static CheckResult All(params CheckResult[] results)
        {
            List<CheckResult> failed = results.Where(r => !r.Pass).ToList();
            string detail = string.Join("; ", results.Select(r => r.Detail).Where(d => !d.NullOrEmpty()));
            return failed.Count == 0 ? CheckResult.Ok(detail) : CheckResult.Fail(string.Join("; ", failed.Select(r => r.Detail)));
        }
    }
}
