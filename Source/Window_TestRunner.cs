using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace TestRunner
{
    public class Window_TestRunner : Window
    {
        private const int BaseSeed = 1234567;
        private const string OutputDir = "Insight";

        private readonly Map map;
        private TestSuite? suite;
        private TestScript? script;
        private int cursor;
        private RngMode mode = RngMode.Seeded;
        private readonly Dictionary<int, CheckResult> results = new Dictionary<int, CheckResult>();
        private readonly Dictionary<int, string> dumps = new Dictionary<int, string>();

        private Pawn? targetPawn;
        private Thing? targetThing;
        private IntVec3 targetCell = IntVec3.Invalid;

        // Static, so it resets each game launch: the log is truncated on the launch's first write, then appended.
        private static readonly HashSet<string> LogFreshThisLaunch = new HashSet<string>();

        public Window_TestRunner()
        {
            map = Find.CurrentMap;
            draggable = true;
            doCloseX = true;
            preventCameraMotion = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
        }

        public override Vector2 InitialSize => new Vector2(580f, 640f);

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("Map: " + (map?.Parent?.LabelCap ?? "<none>"));
            list.GapLine();

            if (script == null || suite == null)
            {
                DrawPicker(list);
                list.End();
                return;
            }

            list.Label("Suite: " + suite.Name + "   Script: " + script.Name);

            list.Label("Target [" + suite.TargetKind + "]: " + TargetLabel());
            if (suite.TargetKind != TargetKind.None && list.ButtonText("Select target"))
            {
                BeginSelectTarget();
            }

            if (list.RadioButton("Real RNG (sample variance)", mode == RngMode.Real, tooltip: "Live RNG - random outcomes vary run to run."))
            {
                mode = RngMode.Real;
            }

            if (list.RadioButton("Seeded RNG (repeatable rolls)", mode == RngMode.Seeded, tooltip: "Each step rolls with a fixed per-step seed, so random outcomes repeat run to run."))
            {
                mode = RngMode.Seeded;
            }

            if (list.RadioButton("Stubbed RNG (force declared rolls)", mode == RngMode.Stubbed, tooltip: "Steps with a .Stub() plan get their declared Rand results forced; unplanned steps and calls run seeded. Unused or extra rolls fail the step."))
            {
                mode = RngMode.Stubbed;
            }

            list.Gap(4f);

            for (int i = 0; i < script.Steps.Count; i++)
            {
                DrawStepRow(list, i);
            }

            GUI.color = Color.white;
            list.GapLine();
            list.Label(Summary());

            bool hasTarget = HasTarget();
            if (!hasTarget)
            {
                GUI.color = new Color(0.9f, 0.6f, 0.4f);
                list.Label("Select a target to run.");
                GUI.color = Color.white;
            }

            Rect row = list.GetRect(32f);
            float w = (row.width - 20f) / 6f;
            bool atEnd = cursor >= script.Steps.Count;

            if (Widgets.ButtonText(Col(row, w, 0), "Prev") && cursor > 0)
            {
                cursor--;
            }

            if (Widgets.ButtonText(Col(row, w, 1), atEnd ? "Done" : "Run & Next") && !atEnd && hasTarget)
            {
                RunStep(cursor);
                cursor++;
            }

            if (Widgets.ButtonText(Col(row, w, 2), "Run all") && hasTarget)
            {
                RunAll();
            }

            if (Widgets.ButtonText(Col(row, w, 3), "Save report"))
            {
                WriteReport();
            }

            if (Widgets.ButtonText(Col(row, w, 4), "Restart"))
            {
                cursor = 0;
                results.Clear();
                dumps.Clear();
            }

            if (Widgets.ButtonText(Col(row, w, 5), "Suites"))
            {
                suite = null;
                script = null;
                cursor = 0;
                results.Clear();
                dumps.Clear();
            }

            list.End();
        }

        private void DrawPicker(Listing_Standard list)
        {
            if (TestRegistry.AllSuites.Count == 0)
            {
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                list.Label("No test suites registered. A mod registers them via ITestProvider or TestRegistry.Register.");
                GUI.color = Color.white;
                return;
            }

            list.Label("Select a script:");
            foreach (TestSuite s in TestRegistry.AllSuites)
            {
                list.Gap(2f);
                GUI.color = new Color(0.8f, 0.85f, 1f);
                list.Label(s.Name + "  [" + s.TargetKind + "]");
                GUI.color = Color.white;
                foreach (TestScript sc in s.Scripts)
                {
                    if (list.ButtonText("    " + sc.Name))
                    {
                        if (suite != s)
                        {
                            targetPawn = null;
                            targetThing = null;
                            targetCell = IntVec3.Invalid;
                        }

                        suite = s;
                        script = sc;
                        cursor = 0;
                        results.Clear();
                        dumps.Clear();
                    }
                }
            }
        }

        private void BeginSelectTarget()
        {
            if (suite == null || suite.TargetKind == TargetKind.None)
            {
                return;
            }

            TargetKind kind = suite.TargetKind;
            Find.Targeter.BeginTargeting(ParamsFor(kind), delegate (LocalTargetInfo t)
            {
                switch (kind)
                {
                    case TargetKind.Pawn: targetPawn = t.Pawn; break;
                    case TargetKind.Building:
                    case TargetKind.Item: targetThing = t.Thing; break;
                    case TargetKind.Cell: targetCell = t.Cell; break;
                }
            });
        }

        private static TargetingParameters ParamsFor(TargetKind k)
        {
            switch (k)
            {
                case TargetKind.Pawn:
                    return new TargetingParameters { canTargetPawns = true, canTargetBuildings = false, canTargetItems = false, canTargetLocations = false };
                case TargetKind.Building:
                    return new TargetingParameters { canTargetPawns = false, canTargetBuildings = true, canTargetItems = false, canTargetLocations = false };
                case TargetKind.Item:
                    return new TargetingParameters { canTargetPawns = false, canTargetBuildings = false, canTargetItems = true, canTargetLocations = false };
                case TargetKind.Cell:
                    return new TargetingParameters { canTargetPawns = false, canTargetBuildings = false, canTargetItems = false, canTargetLocations = true };
                default:
                    return new TargetingParameters();
            }
        }

        private bool HasTarget()
        {
            switch (suite?.TargetKind ?? TargetKind.None)
            {
                case TargetKind.None: return true;
                case TargetKind.Pawn: return targetPawn != null;
                case TargetKind.Building:
                case TargetKind.Item: return targetThing != null;
                case TargetKind.Cell: return targetCell.IsValid;
                default: return true;
            }
        }

        private string TargetLabel()
        {
            switch (suite?.TargetKind ?? TargetKind.None)
            {
                case TargetKind.None: return "(none)";
                case TargetKind.Pawn: return targetPawn != null ? targetPawn.LabelShortCap : "<select a pawn>";
                case TargetKind.Building:
                case TargetKind.Item: return targetThing != null ? targetThing.LabelCap : "<select>";
                case TargetKind.Cell: return targetCell.IsValid ? targetCell.ToString() : "<select a cell>";
                default: return "";
            }
        }

        private TestContext BuildContext()
        {
            return new TestContext
            {
                Map = map,
                Kind = suite!.TargetKind,
                Pawn = targetPawn,
                Thing = targetThing,
                Cell = targetCell,
                Rng = mode,
            };
        }

        private void DrawStepRow(Listing_Standard list, int i)
        {
            TestStep step = script!.Steps[i];
            bool hasResult = results.TryGetValue(i, out CheckResult r);

            string marker;
            if (step.Check == null)
            {
                GUI.color = i == cursor ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
                marker = "-";
            }
            else if (!hasResult)
            {
                GUI.color = i == cursor ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
                marker = "?";
            }
            else
            {
                GUI.color = r.Pass ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
                marker = r.Pass ? "PASS" : "FAIL";
            }

            string prefix = (i == cursor ? "> " : "   ") + (i + 1) + ". [" + marker + "] ";
            string detail = hasResult && !r.Detail.NullOrEmpty() ? "   (" + r.Detail + ")" : "";
            list.Label(prefix + step.Instruction + detail);
        }

        private string Summary()
        {
            Tally(out int pass, out int fail, out int pending, out int manual);
            return string.Format("Checks - PASS {0}  FAIL {1}  pending {2}  |  manual {3}   (rng: {4})",
                pass, fail, pending, manual, mode);
        }

        private void Tally(out int pass, out int fail, out int pending, out int manual)
        {
            pass = fail = pending = manual = 0;
            for (int i = 0; i < script!.Steps.Count; i++)
            {
                if (results.TryGetValue(i, out CheckResult r))
                {
                    if (r.Pass) pass++; else fail++;
                }
                else if (script.Steps[i].Check == null)
                {
                    manual++;
                }
                else
                {
                    pending++;
                }
            }
        }

        private void RunAll()
        {
            results.Clear();
            dumps.Clear();
            for (int i = 0; i < script!.Steps.Count; i++)
            {
                RunStep(i);
            }

            cursor = script.Steps.Count;
            WriteLog(script.Name + " - Run all: " + Summary());
            WriteReport();
        }

        private void RunStep(int i)
        {
            if (script == null || suite == null || !HasTarget() || i < 0 || i >= script.Steps.Count)
            {
                return;
            }

            TestStep step = script.Steps[i];
            TestContext ctx = BuildContext();

            bool pushed = mode == RngMode.Seeded || mode == RngMode.Stubbed;
            bool armed = mode == RngMode.Stubbed && step.StubPlan != null;
            if (pushed)
            {
                Rand.PushState(BaseSeed + i);
            }

            if (armed)
            {
                RandStub.Arm(step.StubPlan!);
            }

            string? driftError = null;
            try
            {
                step.Run(ctx);
            }
            catch (Exception e)
            {
                Log.Error("[Insight] " + suite.Name + " step " + (i + 1) + " threw: " + e);
                WriteLog("step " + (i + 1) + " THREW: " + e.Message);
            }
            finally
            {
                if (armed && RandStub.Drift(out string de))
                {
                    driftError = de;
                }

                RandStub.Disarm();

                if (pushed)
                {
                    Rand.PopState();
                }
            }

            string dump = suite.Describe?.Invoke(ctx) ?? "";
            dumps[i] = dump;

            if (step.Check != null || driftError != null)
            {
                CheckResult r;
                try
                {
                    r = step.Check != null ? step.Check(ctx) : CheckResult.Ok();
                }
                catch (Exception e)
                {
                    r = CheckResult.Fail("check threw: " + e.Message);
                }

                if (driftError != null)
                {
                    r = CheckResult.Fail("stub drift - " + driftError);
                }

                results[i] = r;
                WriteLog(script.Name + " - step " + (i + 1) + " [" + (r.Pass ? "PASS" : "FAIL") + "] " + step.Instruction + " -> " + r.Detail);
            }
            else
            {
                WriteLog(script.Name + " - step " + (i + 1) + " (" + step.Instruction + ") -> " + dump);
            }
        }

        private void WriteReport()
        {
            if (script == null || suite == null)
            {
                return;
            }

            Tally(out int pass, out int fail, out int pending, out int manual);

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"suite\": ").Append(J(suite.Name)).Append(",\n");
            sb.Append("  \"script\": ").Append(J(script.Name)).Append(",\n");
            sb.Append("  \"kind\": ").Append(J(suite.TargetKind.ToString())).Append(",\n");
            sb.Append("  \"target\": ").Append(J(TargetLabel())).Append(",\n");
            sb.Append("  \"rngMode\": ").Append(J(mode.ToString())).Append(",\n");
            sb.Append("  \"summary\": { \"pass\": ").Append(pass).Append(", \"fail\": ").Append(fail)
              .Append(", \"pending\": ").Append(pending).Append(", \"manual\": ").Append(manual).Append(" },\n");
            sb.Append("  \"steps\": [\n");

            for (int i = 0; i < script.Steps.Count; i++)
            {
                TestStep step = script.Steps[i];
                string status;
                string detail = "";
                if (step.Check == null)
                {
                    status = "MANUAL";
                }
                else if (results.TryGetValue(i, out CheckResult r))
                {
                    status = r.Pass ? "PASS" : "FAIL";
                    detail = r.Detail;
                }
                else
                {
                    status = "PENDING";
                }

                sb.Append("    { \"n\": ").Append(i + 1)
                  .Append(", \"status\": ").Append(J(status))
                  .Append(", \"instruction\": ").Append(J(step.Instruction))
                  .Append(", \"detail\": ").Append(J(detail))
                  .Append(", \"dump\": ").Append(J(dumps.TryGetValue(i, out string d) ? d : ""))
                  .Append(" }");
                sb.Append(i < script.Steps.Count - 1 ? ",\n" : "\n");
            }

            sb.Append("  ]\n}\n");

            string path = Path.Combine(OutputDirPath(), SafeName(suite.Name) + ".json");
            try
            {
                File.WriteAllText(path, sb.ToString());
                WriteLog("wrote report: " + path);
            }
            catch (Exception e)
            {
                Log.Error("[Insight] report write failed: " + e);
            }
        }

        private void WriteLog(string line)
        {
            try
            {
                string key = SafeName(suite?.Name ?? "unknown");
                string path = Path.Combine(OutputDirPath(), key + ".log");
                string text = DateTime.Now.ToString("HH:mm:ss") + " " + line + "\n";

                if (LogFreshThisLaunch.Add(key))
                {
                    File.WriteAllText(path, text); // first write this launch: start a clean file
                }
                else
                {
                    File.AppendAllText(path, text);
                }
            }
            catch (Exception e)
            {
                Log.Error("[Insight] log write failed: " + e);
            }
        }

        private static string OutputDirPath()
        {
            string dir = Path.Combine(Application.persistentDataPath, OutputDir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string SafeName(string s)
        {
            if (s.NullOrEmpty())
            {
                return "unknown";
            }

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }

            return s;
        }

        private static string J(string s)
        {
            if (s == null)
            {
                return "\"\"";
            }

            StringBuilder b = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default: b.Append(c); break;
                }
            }

            b.Append("\"");
            return b.ToString();
        }

        private static Rect Col(Rect row, float w, int index) => new Rect(row.x + (w + 4f) * index, row.y, w, row.height);
    }
}
