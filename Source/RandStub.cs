using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace TestRunner
{
    public enum RngMode { Real, Seeded, Stubbed }

    public class StubPlan
    {
        internal readonly List<bool> Chances = new List<bool>();
        internal readonly List<float> Values = new List<float>();
        internal readonly List<float> RangeFloats = new List<float>();
        internal readonly List<int> RangeInts = new List<int>();

        public bool IsEmpty => Chances.Count + Values.Count + RangeFloats.Count + RangeInts.Count == 0;
    }

    public class StubBuilder
    {
        private readonly StubPlan plan;

        internal StubBuilder(StubPlan plan) => this.plan = plan;

        public StubBuilder Chance(params bool[] results) { plan.Chances.AddRange(results); return this; }

        public StubBuilder Value(params float[] values) { plan.Values.AddRange(values); return this; }

        public StubBuilder Range(params float[] values) { plan.RangeFloats.AddRange(values); return this; }

        public StubBuilder RangeInt(params int[] values) { plan.RangeInts.AddRange(values); return this; }
    }

    public static class RandStub
    {
        private sealed class Feed<T>
        {
            public readonly Queue<T> Queue;
            public readonly int Declared;
            public int Over;

            public Feed(List<T> spec)
            {
                Queue = new Queue<T>(spec);
                Declared = spec.Count;
            }

            public bool TryTake(out T value)
            {
                if (Declared == 0)
                {
                    value = default!;
                    return false;
                }

                if (Queue.Count > 0)
                {
                    value = Queue.Dequeue();
                    return true;
                }

                Over++;
                value = default!;
                return false;
            }

            public void AppendDrift(string name, List<string> parts)
            {
                if (Queue.Count > 0)
                {
                    parts.Add(name + ": " + Queue.Count + " of " + Declared + " unconsumed");
                }

                if (Over > 0)
                {
                    parts.Add(name + ": " + Over + " call(s) past the declared " + Declared);
                }
            }
        }

        private sealed class State
        {
            public readonly Feed<bool> Chance;
            public readonly Feed<float> Value;
            public readonly Feed<float> RangeFloat;
            public readonly Feed<int> RangeInt;

            public State(StubPlan p)
            {
                Chance = new Feed<bool>(p.Chances);
                Value = new Feed<float>(p.Values);
                RangeFloat = new Feed<float>(p.RangeFloats);
                RangeInt = new Feed<int>(p.RangeInts);
            }
        }

        private static State? active;

        public static void Arm(StubPlan plan) => active = new State(plan);

        public static void Disarm() => active = null;

        public static bool Drift(out string error)
        {
            error = "";
            State? s = active;
            if (s == null)
            {
                return false;
            }

            List<string> parts = new List<string>();
            s.Chance.AppendDrift("Chance", parts);
            s.Value.AppendDrift("Value", parts);
            s.RangeFloat.AppendDrift("Range(float)", parts);
            s.RangeInt.AppendDrift("Range(int)", parts);

            if (parts.Count == 0)
            {
                return false;
            }

            error = string.Join("; ", parts);
            return true;
        }

        internal static bool TryChance(out bool result) => Take(active?.Chance, out result);

        internal static bool TryValue(out float result) => Take(active?.Value, out result);

        internal static bool TryRangeFloat(out float result) => Take(active?.RangeFloat, out result);

        internal static bool TryRangeInt(out int result) => Take(active?.RangeInt, out result);

        private static bool Take<T>(Feed<T>? feed, out T result)
        {
            if (feed != null)
            {
                return feed.TryTake(out result);
            }

            result = default!;
            return false;
        }
    }

    [HarmonyPatch(typeof(Rand), "get_Value")]
    public static class Patch_Rand_Value
    {
        public static bool Prefix(ref float __result)
        {
            if (RandStub.TryValue(out float v))
            {
                __result = v;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Chance))]
    public static class Patch_Rand_Chance
    {
        public static bool Prefix(ref bool __result)
        {
            if (RandStub.TryChance(out bool b))
            {
                __result = b;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), new[] { typeof(float), typeof(float) })]
    public static class Patch_Rand_RangeFloat
    {
        public static bool Prefix(ref float __result)
        {
            if (RandStub.TryRangeFloat(out float v))
            {
                __result = v;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Rand), nameof(Rand.Range), new[] { typeof(int), typeof(int) })]
    public static class Patch_Rand_RangeInt
    {
        public static bool Prefix(ref int __result)
        {
            if (RandStub.TryRangeInt(out int v))
            {
                __result = v;
                return false;
            }

            return true;
        }
    }
}
