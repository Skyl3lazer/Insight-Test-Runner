using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TestRunner
{
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

    public class TestStep
    {
        public readonly string Instruction;
        public readonly Action<TestContext> Run;
        public readonly Func<TestContext, CheckResult>? Check;
        public readonly bool HumanJudged;
        public StubPlan? StubPlan;

        public TestStep(string instruction, Action<TestContext> run, Func<TestContext, CheckResult>? check = null, bool humanJudged = false)
        {
            Instruction = instruction;
            Run = run;
            Check = check;
            HumanJudged = humanJudged;
        }

        public TestStep Stub(Action<StubBuilder> build)
        {
            StubPlan plan = StubPlan ?? new StubPlan();
            build(new StubBuilder(plan));
            StubPlan = plan;
            return this;
        }
    }

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

    public class TestSuite
    {
        public readonly string Name;
        public readonly TargetKind TargetKind;
        public readonly List<TestScript> Scripts;
        public readonly Func<TestContext, string>? Describe;

        public TestSuite(string name, TargetKind targetKind, List<TestScript> scripts, Func<TestContext, string>? describe = null)
        {
            Name = name;
            TargetKind = targetKind;
            Scripts = scripts;
            Describe = describe;
        }
    }

    public static class Test
    {
        public static TestStep Do(string instruction, Action<TestContext> op) => new TestStep(instruction, op);

        public static TestStep Do(string instruction, Action<TestContext> op, Func<TestContext, CheckResult> check) => new TestStep(instruction, op, check);

        public static TestStep Check(string instruction, Func<TestContext, CheckResult> check) => new TestStep(instruction, _ => { }, check);

        public static TestStep Note(string instruction) => new TestStep(instruction, _ => { }, null, humanJudged: true);

        public static TestScript Script(string name, params TestStep[] steps) => new TestScript(name, steps.ToList());
    }

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
