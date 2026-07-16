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
        private string? suiteResult;
        private Vector2 stepScroll;
        private Vector2 pickerScroll;
        private float stepsHeight;
        private float pickerHeight;
        private int scrolledCursor = -1;

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

        public override Vector2 InitialSize => new Vector2(580f, 680f);

        public override void DoWindowContents(Rect inRect)
        {
            if (script == null || suite == null)
            {
                DrawPickerArea(inRect);
                return;
            }

            Listing_Standard header = new Listing_Standard();
            header.Begin(inRect);
            header.Label("Map: " + (map?.Parent?.LabelCap ?? "<none>"));
            header.GapLine();
            header.Label("Suite: " + suite.Name + "   Script: " + script.Name);
            header.Label("Target [" + suite.TargetKind + "]: " + TargetLabel());
            if (suite.TargetKind != TargetKind.None && header.ButtonText("Select target"))
            {
                BeginSelectTarget();
            }

            if (header.RadioButton("Real RNG (sample variance)", mode == RngMode.Real, tooltip: "Live RNG - random outcomes vary run to run."))
            {
                mode = RngMode.Real;
            }

            if (header.RadioButton("Seeded RNG (repeatable rolls)", mode == RngMode.Seeded, tooltip: "Each step rolls with a fixed per-step seed, so random outcomes repeat run to run."))
            {
                mode = RngMode.Seeded;
            }

            if (header.RadioButton("Stubbed RNG (force declared rolls)", mode == RngMode.Stubbed, tooltip: "Steps with a .Stub() plan get their declared Rand results forced; unplanned steps and calls run seeded. Unused or extra rolls fail the step."))
            {
                mode = RngMode.Stubbed;
            }

            header.Gap(4f);
            float headerBottom = inRect.y + header.CurHeight;
            header.End();

            bool hasTarget = HasTarget();
            float footerH = FooterHeight(hasTarget);
            Rect footerRect = new Rect(inRect.x, inRect.yMax - footerH, inRect.width, footerH);

            Rect outRect = new Rect(inRect.x, headerBottom, inRect.width, Mathf.Max(0f, footerRect.y - headerBottom));
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(stepsHeight, outRect.height));
            stepScroll = GUI.BeginScrollView(outRect, stepScroll, viewRect, false, true);
            Listing_Standard stepsList = new Listing_Standard();
            stepsList.Begin(viewRect);
            float cursorTop = -1f;
            float cursorBottom = -1f;
            for (int i = 0; i < script.Steps.Count; i++)
            {
                if (i == cursor)
                {
                    cursorTop = stepsList.CurHeight;
                }

                DrawStepRow(stepsList, i);

                if (i == cursor)
                {
                    cursorBottom = stepsList.CurHeight;
                }
            }

            stepsHeight = stepsList.CurHeight;
            stepsList.End();
            GUI.EndScrollView();

            if (cursor != scrolledCursor)
            {
                if (cursorTop >= 0f)
                {
                    if (cursorTop < stepScroll.y)
                    {
                        stepScroll.y = cursorTop;
                    }
                    else if (cursorBottom > stepScroll.y + outRect.height)
                    {
                        stepScroll.y = cursorBottom - outRect.height;
                    }

                    stepScroll.y = Mathf.Clamp(stepScroll.y, 0f, Mathf.Max(0f, stepsHeight - outRect.height));
                }

                scrolledCursor = cursor;
            }

            DrawFooter(footerRect, hasTarget);
        }

        private void DrawPickerArea(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(pickerHeight, inRect.height));
            pickerScroll = GUI.BeginScrollView(inRect, pickerScroll, viewRect, false, true);
            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);
            list.Label("Map: " + (map?.Parent?.LabelCap ?? "<none>"));
            list.GapLine();
            DrawPicker(list);
            pickerHeight = list.CurHeight;
            list.End();
            GUI.EndScrollView();
        }

        private float FooterHeight(bool hasTarget)
        {
            float h = 12f;
            h += Text.LineHeight;
            if (suiteResult != null)
            {
                h += Text.LineHeight;
            }

            if (!hasTarget)
            {
                h += Text.LineHeight;
            }

            return h + 4f + 32f + 4f + 32f + 6f;
        }

        private void DrawFooter(Rect rect, bool hasTarget)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);

            GUI.color = Color.white;
            list.GapLine();
            list.Label(Summary());

            if (suiteResult != null)
            {
                GUI.color = new Color(0.8f, 0.85f, 1f);
                list.Label(suiteResult);
                GUI.color = Color.white;
            }

            if (!hasTarget)
            {
                GUI.color = new Color(0.9f, 0.6f, 0.4f);
                list.Label("Select a target to run.");
                GUI.color = Color.white;
            }

            Rect row1 = list.GetRect(32f);
            float w1 = (row1.width - 12f) / 4f;
            bool atEnd = cursor >= script!.Steps.Count;

            if (Widgets.ButtonText(Col(row1, w1, 0), "Prev") && cursor > 0)
            {
                cursor--;
            }

            if (Widgets.ButtonText(Col(row1, w1, 1), atEnd ? "Done" : "Run & Next") && !atEnd && hasTarget)
            {
                RunStep(cursor);
                cursor++;
            }

            if (Widgets.ButtonText(Col(row1, w1, 2), "Run script") && hasTarget)
            {
                RunAll();
            }

            if (Widgets.ButtonText(Col(row1, w1, 3), "Run suite") && hasTarget)
            {
                RunSuite();
            }

            list.Gap(4f);
            Rect row2 = list.GetRect(32f);
            float w2 = (row2.width - 8f) / 3f;

            if (Widgets.ButtonText(Col(row2, w2, 0), "Save report"))
            {
                WriteReport();
            }

            if (Widgets.ButtonText(Col(row2, w2, 1), "Restart"))
            {
                cursor = 0;
                results.Clear();
                dumps.Clear();
                suiteResult = null;
                stepScroll = Vector2.zero;
            }

            if (Widgets.ButtonText(Col(row2, w2, 2), "Suites"))
            {
                suite = null;
                script = null;
                cursor = 0;
                results.Clear();
                dumps.Clear();
                suiteResult = null;
                stepScroll = Vector2.zero;
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
                        suiteResult = null;
                        stepScroll = Vector2.zero;
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
            Color color;
            if (hasResult)
            {
                color = r.Pass ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
                marker = r.Pass ? "PASS" : "FAIL";
            }
            else if (step.Check == null)
            {
                color = i == cursor ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
                marker = "-";
            }
            else
            {
                color = i == cursor ? Color.yellow : new Color(0.7f, 0.7f, 0.7f);
                marker = "?";
            }

            string prefix = (i == cursor ? "> " : "   ") + (i + 1) + ". [" + marker + "] ";
            string detail = hasResult && !r.Detail.NullOrEmpty() ? "   (" + r.Detail + ")" : "";
            string text = prefix + step.Instruction + detail;

            bool showManualButtons = step.Check == null && i == cursor;
            float btnW = Text.LineHeight;
            const float gap = 2f;
            float labelWidth = showManualButtons ? list.ColumnWidth - ((btnW * 2f) + (gap * 2f)) : list.ColumnWidth;
            float h = Mathf.Max(Text.LineHeight, Text.CalcHeight(text, labelWidth));
            Rect rowRect = list.GetRect(h);

            GUI.color = color;
            Widgets.Label(new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height), text);
            GUI.color = Color.white;

            if (!showManualButtons)
            {
                return;
            }

            Rect passRect = new Rect(rowRect.xMax - (btnW * 2f) - gap, rowRect.y, btnW, btnW);
            Rect failRect = new Rect(rowRect.xMax - btnW, rowRect.y, btnW, btnW);

            if (Widgets.ButtonImage(passRect, Widgets.CheckboxOnTex, new Color(0.4f, 0.85f, 0.4f), new Color(0.6f, 1f, 0.6f), true, "Mark this manual step passed and advance"))
            {
                MarkManual(i, true);
            }

            if (Widgets.ButtonImage(failRect, TexButton.CloseXSmall, new Color(0.9f, 0.4f, 0.4f), new Color(1f, 0.6f, 0.6f), true, "Mark this manual step failed and advance"))
            {
                MarkManual(i, false);
            }
        }

        private void MarkManual(int i, bool pass)
        {
            results[i] = pass ? CheckResult.Ok("manual pass") : CheckResult.Fail("manual fail");
            dumps[i] = suite?.Describe?.Invoke(BuildContext()) ?? "";
            WriteLog(script!.Name + " - step " + (i + 1) + " [" + (pass ? "PASS" : "FAIL") + "] (manual) " + script.Steps[i].Instruction);
            cursor = Mathf.Min(i + 1, script.Steps.Count);
        }

        private string Summary()
        {
            Tally(out int pass, out int fail, out int pending, out int manual);
            return string.Format("Checks - PASS {0}  FAIL {1}  pending {2}  |  manual {3}   (rng: {4})",
                pass, fail, pending, manual, mode);
        }

        private void Tally(out int pass, out int fail, out int pending, out int manual)
        {
            TallyOf(script!, results, out pass, out fail, out pending, out manual);
        }

        private static void TallyOf(TestScript sc, Dictionary<int, CheckResult> res, out int pass, out int fail, out int pending, out int manual)
        {
            pass = fail = pending = manual = 0;
            for (int i = 0; i < sc.Steps.Count; i++)
            {
                if (res.TryGetValue(i, out CheckResult r))
                {
                    if (r.Pass) pass++; else fail++;
                }
                else if (sc.Steps[i].Check == null)
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
            suiteResult = null;
            results.Clear();
            dumps.Clear();
            for (int i = 0; i < script!.Steps.Count; i++)
            {
                RunStep(i);
            }

            cursor = script.Steps.Count;
            WriteLog(script.Name + " - Run script: " + Summary());
            WriteReport();
        }

        private void RunSuite()
        {
            if (suite == null || !HasTarget())
            {
                return;
            }

            List<ScriptRun> runs = new List<ScriptRun>();
            int aPass = 0, aFail = 0, aPending = 0, aManual = 0;

            foreach (TestScript sc in suite.Scripts)
            {
                Dictionary<int, CheckResult> res = new Dictionary<int, CheckResult>();
                Dictionary<int, string> dmp = new Dictionary<int, string>();
                for (int i = 0; i < sc.Steps.Count; i++)
                {
                    RunStepCore(sc, i, res, dmp);
                }

                TallyOf(sc, res, out int p, out int f, out int pend, out int man);
                aPass += p; aFail += f; aPending += pend; aManual += man;
                WriteLog(sc.Name + " - PASS " + p + " FAIL " + f + " pending " + pend + " manual " + man);
                runs.Add(new ScriptRun(sc, res, dmp));

                if (sc == script)
                {
                    results.Clear();
                    dumps.Clear();
                    foreach (KeyValuePair<int, CheckResult> kv in res) results[kv.Key] = kv.Value;
                    foreach (KeyValuePair<int, string> kv in dmp) dumps[kv.Key] = kv.Value;
                    cursor = sc.Steps.Count;
                }
            }

            suiteResult = string.Format("Run suite - {0} scripts: PASS {1} FAIL {2} pending {3} manual {4} (rng: {5})",
                runs.Count, aPass, aFail, aPending, aManual, mode);
            WriteLog(suiteResult);
            WriteReportFile(runs);
        }

        private void RunStep(int i) => RunStepCore(script!, i, results, dumps);

        private void RunStepCore(TestScript sc, int i, Dictionary<int, CheckResult> res, Dictionary<int, string> dmp)
        {
            if (suite == null || !HasTarget() || i < 0 || i >= sc.Steps.Count)
            {
                return;
            }

            TestStep step = sc.Steps[i];
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
            dmp[i] = dump;

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

                res[i] = r;
                WriteLog(sc.Name + " - step " + (i + 1) + " [" + (r.Pass ? "PASS" : "FAIL") + "] " + step.Instruction + " -> " + r.Detail);
            }
            else
            {
                WriteLog(sc.Name + " - step " + (i + 1) + " (" + step.Instruction + ") -> " + dump);
            }
        }

        private void WriteReport()
        {
            if (script == null)
            {
                return;
            }

            WriteReportFile(new List<ScriptRun> { new ScriptRun(script, results, dumps) });
        }

        private void WriteReportFile(List<ScriptRun> runs)
        {
            if (suite == null)
            {
                return;
            }

            int aPass = 0, aFail = 0, aPending = 0, aManual = 0;
            foreach (ScriptRun run in runs)
            {
                TallyOf(run.Script, run.Results, out int p, out int f, out int pend, out int man);
                aPass += p; aFail += f; aPending += pend; aManual += man;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"suite\": ").Append(J(suite.Name)).Append(",\n");
            sb.Append("  \"kind\": ").Append(J(suite.TargetKind.ToString())).Append(",\n");
            sb.Append("  \"target\": ").Append(J(TargetLabel())).Append(",\n");
            sb.Append("  \"rngMode\": ").Append(J(mode.ToString())).Append(",\n");
            sb.Append("  \"summary\": { \"pass\": ").Append(aPass).Append(", \"fail\": ").Append(aFail)
              .Append(", \"pending\": ").Append(aPending).Append(", \"manual\": ").Append(aManual).Append(" },\n");
            sb.Append("  \"scripts\": [\n");

            for (int s = 0; s < runs.Count; s++)
            {
                AppendScript(sb, runs[s]);
                sb.Append(s < runs.Count - 1 ? ",\n" : "\n");
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

        private static void AppendScript(StringBuilder sb, ScriptRun run)
        {
            TestScript sc = run.Script;
            TallyOf(sc, run.Results, out int pass, out int fail, out int pending, out int manual);

            sb.Append("    {\n");
            sb.Append("      \"script\": ").Append(J(sc.Name)).Append(",\n");
            sb.Append("      \"summary\": { \"pass\": ").Append(pass).Append(", \"fail\": ").Append(fail)
              .Append(", \"pending\": ").Append(pending).Append(", \"manual\": ").Append(manual).Append(" },\n");
            sb.Append("      \"steps\": [\n");

            for (int i = 0; i < sc.Steps.Count; i++)
            {
                TestStep step = sc.Steps[i];
                string status;
                string detail = "";
                if (run.Results.TryGetValue(i, out CheckResult r))
                {
                    status = r.Pass ? "PASS" : "FAIL";
                    detail = r.Detail;
                }
                else if (step.Check == null)
                {
                    status = "MANUAL";
                }
                else
                {
                    status = "PENDING";
                }

                sb.Append("        { \"n\": ").Append(i + 1)
                  .Append(", \"status\": ").Append(J(status))
                  .Append(", \"instruction\": ").Append(J(step.Instruction))
                  .Append(", \"detail\": ").Append(J(detail))
                  .Append(", \"dump\": ").Append(J(run.Dumps.TryGetValue(i, out string d) ? d : ""))
                  .Append(" }");
                sb.Append(i < sc.Steps.Count - 1 ? ",\n" : "\n");
            }

            sb.Append("      ]\n");
            sb.Append("    }");
        }

        private sealed class ScriptRun
        {
            public readonly TestScript Script;
            public readonly Dictionary<int, CheckResult> Results;
            public readonly Dictionary<int, string> Dumps;

            public ScriptRun(TestScript script, Dictionary<int, CheckResult> results, Dictionary<int, string> dumps)
            {
                Script = script;
                Results = results;
                Dumps = dumps;
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
