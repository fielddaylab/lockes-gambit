using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;
using SpaceFab.Save;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Phase driver for Simulate mode. Owns the SimulatePhase state machine: reads one-frame request
    /// flags on SimulateRunState, decides whether this frame advances a depth step or transitions to a
    /// new phase, and sets PaintDepthThisFrame on the frames DepthStepSystem should do flow work.
    /// Runs on Update at order 1 under SimulateModeMask.
    /// </summary>
    public class SimulateModeSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 1, UpdateMasks.SimulateModeMask),
                new SysPermissions()
                    .ReadWriteShared<SimulateRunState>()
                    .ReadWriteShared<SimulateGraphState>()
                    .ReadWriteShared<SimulateRunScratch>()
                    .ReadShared<GridStackState>()
                    .ReadWriteShared<SimulateUIState>()
                    .ReadWriteShared<VisualGridStackState>()
                    .ReadWriteShared<PlayerProgressState>()
                    .ReadWriteShared<DesignMinigameState>()
                    .ReadWriteShared<MinigameSaveStates>()
            );
        }

        // Top-level phase dispatch. Cancel is checked first and wins over any other request.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out SimulateRunState runState,
                out SimulateGraphState graphState,
                out SimulateRunScratch runScratch,
                out GridStackState gridStackState
                );
            Find.State(
                out SimulateUIState uiState,
                out VisualGridStackState visualState,
                out PlayerProgressState progressState,
                out DesignMinigameState designState
                );
            Find.State(
                out ContractState contractState
            );

            // Universal high-priority request: Cancel beats everything except Cancelling itself.
            if (runState.Phase != SimulatePhase.Cancelling && runState.CancelRequested) {
                EnterCancelling(runState);
                return;
            }

            switch (runState.Phase) {
                case SimulatePhase.Idle:
                    ProcessIdle(runState, uiState, designState);
                    break;
                case SimulatePhase.PreparingTest:
                    ProcessPreparingTest(runState, runScratch, graphState, uiState, visualState, progressState, contractState, gridStackState, designState);
                    break;
                case SimulatePhase.Propagating:
                    ProcessPropagating(runState, graphState, uiState, designState, deltaTime);
                    break;
                case SimulatePhase.Paused:
                    ProcessPaused(runState, uiState, designState);
                    break;
                case SimulatePhase.ResolvingTest:
                    ProcessResolvingTest(runState, runScratch, graphState, uiState, progressState, contractState, gridStackState, designState);
                    break;
                case SimulatePhase.SuiteComplete:
                    ProcessSuiteComplete(runState, uiState, designState);
                    break;
                case SimulatePhase.Cancelling:
                    ProcessCancelling(runState, runScratch, graphState, uiState, visualState, designState);
                    break;
            }
        }

        // Idle: accept Play / PlaySingle. Graph is already built by ModeTransitionSystem on
        // Simulate-mode entry, so we can go straight to PreparingTest without a build phase.
        // Branch order: a queued cancel-then-play (PendingPlayRowIndex) wins over an explicit
        // PlayFullSuiteRequested if both somehow set the same frame, since the queue carries an
        // older intent. PlayFullSuiteRequested then wins over PlaySingleTestRequested.
        //
        // Verdict-clearing rule: any new run (single-test or full-suite) wipes all model + UI
        // verdicts up front. Each row's SetVerdict then writes its own slot as it resolves, so a
        // full-suite run accumulates verdicts row-by-row from a clean slate.
        static private void ProcessIdle(SimulateRunState runState, SimulateUIState uiState, DesignMinigameState designState)
        {
            // Toggle-input mode "Test" click: matched row was looked up by SuiteTestButtonRefreshSystem
            // and carried via RequestedRowIndex. No verdict wipe — toggle mode preserves prior verdicts
            // across runs (WipeVerdictsForNewRun is a no-op when UseToggleInputMode).
            if (runState.PlayCurrentToggleComboRequested)
            {
                runState.Scope = RunScope.SingleTest;
                runState.CurrentRow = runState.RequestedRowIndex;
                runState.PendingPlayRowIndex = -1;
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }

            // Cancel-then-play hand-off. SuiteRunRowButton's click handler sets PendingPlayRowIndex
            // when the player clicks an inactive row mid-run; ProcessCancelling preserved it across
            // the Cancelling -> Idle transition for us to consume here.
            if (runState.PendingPlayRowIndex >= 0)
            {
                runState.Scope = RunScope.SingleTest;
                runState.CurrentRow = runState.PendingPlayRowIndex;
                runState.PendingPlayRowIndex = -1;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }

            if (runState.PlayFullSuiteRequested)
            {
                runState.Scope = RunScope.FullSuite;
                runState.CurrentRow = 0;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }

            if (runState.PlaySingleTestRequested)
            {
                runState.Scope = RunScope.SingleTest;
                runState.CurrentRow = runState.RequestedRowIndex;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                runState.Phase = SimulatePhase.PreparingTest;
                return;
            }
        }

        // PreparingTest: reset per-row sim state, seed the input segments, advance to Propagating.
        // Runs exactly once per test-row start.
        //
        // Reset strategy:
        //   - Transient arrays (SegmentFlow / NodeTempTransform): Array.Clear over the live counts.
        //     Both are tens at most — effectively free.
        //   - Per-cell transient state (CellFlow / CellTempTransform): BumpFlowStamp. Single int
        //     increment invalidates every per-cell mark from the prior test.
        //   - Edge state: none to reset. CycleDetected is durable on CrucialEdge and computed at
        //     Build time, not per test.
        static private void ProcessPreparingTest(SimulateRunState runState, SimulateRunScratch runScratch, SimulateGraphState graphState, SimulateUIState uiState, VisualGridStackState visualState, PlayerProgressState progressState, ContractState contractState, GridStackState gridStackState, DesignMinigameState designState)
        {
            // Per-segment and per-node transient reset.
            SimulateRunScratchUtility.ClearRunTransients(runScratch, graphState.NodeCount, graphState.SegmentCount);

            // Per-cell transient reset. O(1) — stamp bump invalidates all prior flow + temp-
            // transform writes without touching the arrays themselves.
            SimulateRunScratchUtility.BumpFlowStamp(runScratch);

            // Seed each Input's segment with this row's value. An Input drives its whole conductor
            // the moment the row starts, so this belongs on the segment rather than being read
            // back per edge — which is also what removes the Input special case from ProcessEdge.
            // Two Inputs wired to one segment resolve Unstable here, before propagation begins.
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();
            TestData currTest = suite.Rows[runState.CurrentRow];
            List<GridCoord> inputs = new List<GridCoord>();
            List<GridCoord> outputs = new List<GridCoord>();

            for (int i = 0; i < graphState.NodeCount; i++)
            {
                CrucialNode node = graphState.CrucialNodes[i];
                GridCell cell = GridStackUtility.GetCellDirect(gridStackState, node.Coord);
                if (cell.CellType == CellType.Input)
                {
                    FlowState inputFlow = EvalUtility.GetTestValBySubType(cell.SubtypeLabel, suite.ColumnMask, currTest);
                    SimulateRunScratchUtility.AssignSegmentFlow(runScratch, graphState.CrucialSegment[i], inputFlow);

                    // Paint the Input's own cell too. DepthStepSystem's crawl carries the colour
                    // already on an edge's origin cell, so without this every depth-0 edge would
                    // start from an unpainted origin and nothing would ever advance.
                    SimulateRunScratchUtility.SetCellFlow(runScratch, node.CellIndex, inputFlow);
                    inputs.Add(node.Coord);
                }
                else if (cell.CellType == CellType.Output)
                {
                    outputs.Add(node.Coord);
                }
            }

            Debug.Log($"{inputs} \n {outputs}");

            SpacefabGame.Events.Dispatch(GameEvents.DesignSimRowStarted, EvtArgs.Box((inputs, outputs, runState.CurrentRow)));
            SimulateUIUtility.WriteRowInputs(uiState, runState.CurrentRow, currTest);
            SimulateControlUtility.SetVerdictInProgress(runState, runState.CurrentRow);

            // Wipe any leftover verdict marks from this row's previous run — the new propagation
            // hasn't produced a result yet, so the per-output visualizers should read as "no
            // verdict active" until ResolvingTest writes fresh ones. In toggle-input mode we
            // preserve verdicts across runs (they only clear on grid edits), so skip the hide.
            SimulateUIUtility.HideRowVerdicts(uiState, runState.CurrentRow);

            // Mark visuals dirty so GridVisualsUpdateSystem redraws the now-empty-flow grid.
            visualState.VisualsNeedRefreshing = true;

            runState.IsUnstable = false;
            runScratch.IsUnstable = false;
            uiState.HighlightedRowIndex = runState.CurrentRow;

            runState.CurrentDepth = 0;
            runState.PhaseTimer = 0f;
            runState.Phase = SimulatePhase.Propagating;

            // CurrentRow / Phase just changed; the active row's button needs to flip to Pause.
            SimulateUIUtility.MarkAllRunButtonsDirty(uiState);

            //SpacefabGame.Events.Dispatch(GameEvents.DesignSimRowStarted, runState.CurrentRow);
        }

        // Propagating: per-depth paint rhythm.
        //   First frame at a new depth (PhaseTimer == 0) → set PaintDepthThisFrame so DepthStepSystem
        //     paints this depth's edges this same frame (it runs at Update order 2, after us at 1).
        //   Every frame (including the paint frame) → accumulate deltaTime. The first frame's
        //     accumulation is what pulls PhaseTimer off zero so the next frame doesn't re-paint.
        //   At PhaseTimer >= InterDepthDelay → depth boundary; check interrupts, then advance or resolve.
        static private void ProcessPropagating(SimulateRunState runState, SimulateGraphState graphState, SimulateUIState uiState, DesignMinigameState designState, float deltaTime)
        {
            // First frame at this depth: signal DepthStepSystem to paint.
            if (runState.PhaseTimer == 0f)
            {
                runState.PaintDepthThisFrame = true;
            }

            // Depth boundary reached. Check interrupts before advancing. Restarts beat Pause:
            // a player who hits Restart while the run is mid-flight expects an immediate reset,
            // not a pause-then-restart. Pause comes last so it's the catch-all if no Restart fired.

            if (runState.RestartTestRequested)
            {
                // Same row, fresh sim state — ProcessPreparingTest re-primes inputs and resets
                // the depth pointer. Verdicts for other rows stay untouched.
                runState.Phase = SimulatePhase.PreparingTest;
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            if (runState.RestartSuiteRequested)
            {
                runState.Scope = RunScope.FullSuite;
                runState.CurrentRow = 0;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                runState.Phase = SimulatePhase.PreparingTest;
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            if (runState.PauseRequested)
            {
                runState.Phase = SimulatePhase.Paused;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPaused);
                // Active row's button should flip from Pause icon to Resume icon.
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            // Accumulate. Even on the paint frame: ensures next frame won't repaint and progress
            // is made toward the InterDepthDelay threshold.
            runState.PhaseTimer += deltaTime;
            if (runState.PhaseTimer < runState.InterDepthDelay) { return; }

            // No interrupt: advance depth or finish.
            runState.CurrentDepth++;
            runState.PhaseTimer = 0f;
            if (runState.CurrentDepth > graphState.MaxDepth)
            {
                runState.Phase = SimulatePhase.ResolvingTest;
                // Active row's button should flip back from Pause to Play.
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
            }
            // Otherwise PhaseTimer == 0 → next frame's first-frame branch paints the new depth.
        }

        // Paused: wait for Resume or Restart*. Cancel handled at top of ProcessWork. Restart
        // wins over Resume if both somehow fired the same frame — Restart is the more-decisive
        // intent (player wants the run to start over, not just to keep going).
        static private void ProcessPaused(SimulateRunState runState, SimulateUIState uiState, DesignMinigameState designState)
        {
            if (runState.RestartTestRequested)
            {
                runState.Phase = SimulatePhase.PreparingTest;
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            if (runState.RestartSuiteRequested)
            {
                runState.Scope = RunScope.FullSuite;
                runState.CurrentRow = 0;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                runState.Phase = SimulatePhase.PreparingTest;
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            if (runState.ResumeRequested)
            {
                runState.Phase = SimulatePhase.Propagating;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimResumed);
                // Active row's button should flip from Resume icon back to Pause icon.
                // PhaseTimer is left at its boundary value, so the next ProcessPropagating tick
                // falls straight through to depth-advance — the just-painted depth doesn't get
                // re-painted, the run continues at the next depth.
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }
        }

        // ResolvingTest: score outputs for CurrentRow, write verdict, advance to next row or finish.
        //
        // Reads each Output's flow from its SEGMENT — the same value the output tag shows via
        // GetCellFlow, so the two cannot disagree — fills runScratch.OutputFlowBuffer in
        // CrucialNodes-Output order, and compares each value to the expected one from the current
        // row's TestData. The verdict gets recorded on runState.RowVerdicts via SetVerdict and
        // pushed to the UI via WriteRowVerdict (currently a stub — UI not yet implemented).
        //
        // Advance rules:
        //   SingleTest scope                       → SuiteComplete (whole run is just this row).
        //   FullSuite, more rows remain            → next row via PreparingTest.
        //   FullSuite, last row resolved           → SuiteComplete with aggregate-correct flag.
        static private void ProcessResolvingTest(SimulateRunState runState, SimulateRunScratch runScratch, SimulateGraphState graphState, SimulateUIState uiState, PlayerProgressState progressState, ContractState contractState, GridStackState gridStackState, DesignMinigameState designState)
        {
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();
            TestData currTest = suite.Rows[runState.CurrentRow];

            MinigameSaveStates saveStates = Find.State<MinigameSaveStates>();

            // Score every Output crucial node against its expected value. OutputFlowBuffer is
            // sized by SimulateRunScratchUtility.SizeOutputBuffer at Simulate-mode entry, in the
            // same CrucialNodes ordering — so outputIdx walks both in lockstep. We also build
            // actualPerCol — actual flow indexed by bundle column — so the UI layer can display
            // per-output verdicts without re-walking the graph.
            TestData actualPerCol = default;
            bool allCorrect = true;
            bool anyOutputUnstable = false;
            int outputIdx = 0;
            for (int i = 0; i < graphState.NodeCount; i++)
            {
                CrucialNode node = graphState.CrucialNodes[i];
                GridCell cell = GridStackUtility.GetCellDirect(gridStackState, node.Coord);
                if (cell.CellType != CellType.Output) { continue; }

                FlowState actual = runScratch.SegmentFlow[graphState.CrucialSegment[i]];
                FlowState expected = EvalUtility.GetTestValBySubType(cell.SubtypeLabel, suite.ColumnMask, currTest);
                runScratch.OutputFlowBuffer[outputIdx++] = actual;
                if (actual != expected) { allCorrect = false; }
                if (actual == FlowState.Unstable) { anyOutputUnstable = true; }

                switch (cell.SubtypeLabel) {
                    case InputOutputNodeTypeFlags.OUTX: {
                        actualPerCol.OutputX = actual;
                        break;
                    }
                    case InputOutputNodeTypeFlags.OUTY: {
                        actualPerCol.OutputY = actual;
                        break;
                    }
                }
            }

            // Unstable beats Correct/Incorrect, but only when the instability actually reaches an
            // output. Scoping it to the output segments rather than a board-wide flag means a
            // conflicting pair of drivers off in a region that connects to nothing still paints
            // Unstable — it is unstable, and the player should see that — without deciding a row
            // it has no electrical bearing on.
            TestRowVerdict verdict = anyOutputUnstable
                ? TestRowVerdict.Unstable
                : (allCorrect ? TestRowVerdict.Correct : TestRowVerdict.Incorrect);
            SimulateControlUtility.SetVerdict(runState, runState.CurrentRow, verdict, actualPerCol);
            SimulateUIUtility.WriteRowVerdict(uiState, runState.CurrentRow, currTest, actualPerCol);
            
            using (var table = TempVarTable.Alloc()) {
                var resultStr = "failure";
                if (verdict == TestRowVerdict.Correct) { resultStr = "success"; }
                table.Set("result", resultStr);
                ScriptUtility.Trigger(DesignScriptTriggers.OnSingleTestComplete, table);
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimRowResolved, EvtArgs.Box((resultStr, runState.CurrentRow)));
            }

            // Toggle-input mode: every Test click resolves a single row. Show the results panel
            // only when every row in the suite has been resolved Correct (the "level complete"
            // moment). Partial passes / fails leave the panel hidden so the player can keep
            // toggling inputs and running tests without dismissal friction.
            if (true)
            {
                runState.Phase = SimulatePhase.SuiteComplete;
                bool suiteAllCorrect = IsAllCorrect(runState.RowVerdicts);
                if (suiteAllCorrect)
                {
                    ScriptUtility.Trigger(DesignScriptTriggers.OnAllTestsComplete);
                    SimulateUIUtility.ShowResultsPanel(uiState, true); // called too early relative to sim table
                    
                    DesignLevelUtility.MarkActiveLevelSolved(saveStates.Design, designState);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignSimSuiteSucceeded);
                }
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimSuiteComplete);
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            //// Advance based on Scope.
            //if (runState.Scope == RunScope.SingleTest)
            //{
            //    runState.Phase = SimulatePhase.SuiteComplete;
            //    SimulateUIUtility.ShowResultsPanel(uiState, verdict == TestRowVerdict.Correct);
            //    SpacefabGame.Events.Dispatch(GameEvents.DesignSimSuiteComplete);
            //}
            //else if (runState.CurrentRow + 1 < runState.RowVerdicts.Length)
            //{
            //    runState.CurrentRow++;
            //    runState.Phase = SimulatePhase.PreparingTest;
            //}
            //else
            //{
            //    runState.Phase = SimulatePhase.SuiteComplete;
            //    bool suiteAllCorrect = IsAllCorrect(runState.RowVerdicts);
            //    SimulateUIUtility.ShowResultsPanel(uiState, suiteAllCorrect);
            //    // A passing full-suite run is the one moment FoundValidSolution flips true.
            //    // It's reset to false on grid edits and on any subsequent test re-run via
            //    // DesignMinigameState's event listeners — set directly here rather than via
            //    // an event to keep the success signal coupled to the verdict array that
            //    // produced it.
            //    if (suiteAllCorrect)
            //    {
            //        DesignLevelUtility.MarkActiveLevelSolved(saveStates.Design, designState);
            //    }
            //    SpacefabGame.Events.Dispatch(GameEvents.DesignSimSuiteComplete);
            //}

            //// Phase or CurrentRow changed; the active row's button needs a repaint.
            //SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
        }

        // True iff every entry in verdicts is Correct. Used for the suite-level pass/fail flag
        // pushed to the results panel at the end of a FullSuite run.
        static private bool IsAllCorrect(TestRowVerdict[] verdicts)
        {
            for (int i = 0; i < verdicts.Length; i++)
            {
                if (verdicts[i] != TestRowVerdict.Correct) { return false; }
            }
            return true;
        }

        // SuiteComplete: wait on Dismiss or new Play request. Cancel handled at top.
        // Both Play branches wipe all row verdicts and hide the results panel before re-running.
        static private void ProcessSuiteComplete(SimulateRunState runState, SimulateUIState uiState, DesignMinigameState designState)
        {
            if (runState.DismissResultsRequested)
            {
                SimulateUIUtility.HideResultsPanel(uiState);
                runState.Phase = SimulatePhase.Idle;
                SimulateUIUtility.MarkAllRunButtonsDirty(uiState);
                return;
            }

            // Toggle-input mode: the player toggled inputs after the results panel was shown and
            // hit Test again. Same path as ProcessIdle's toggle branch — preserve verdicts, hide
            // the panel, and start the next single-test run.
            if (runState.PlayCurrentToggleComboRequested)
            {
                runState.Scope = RunScope.SingleTest;
                runState.CurrentRow = runState.RequestedRowIndex;
                runState.PendingPlayRowIndex = -1;
                SimulateUIUtility.HideResultsPanel(uiState);
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }

            if (runState.PlayFullSuiteRequested)
            {
                runState.Scope = RunScope.FullSuite;
                runState.CurrentRow = 0;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                SimulateUIUtility.HideResultsPanel(uiState);
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }

            if (runState.PlaySingleTestRequested)
            {
                runState.Scope = RunScope.SingleTest;
                runState.CurrentRow = runState.RequestedRowIndex;
                SimulateControlUtility.WipeVerdictsForNewRun(runState, uiState, designState);
                SimulateUIUtility.HideResultsPanel(uiState);
                runState.Phase = SimulatePhase.PreparingTest;
                SpacefabGame.Events.Dispatch(GameEvents.DesignSimPlayStarted);
                return;
            }
        }

        // Cancelling: wipe sim visuals and exit Simulate mode via ModeTransitionState.
        //
        // Flow-wipe is O(1): bump CurrentFlowStamp, which invalidates every per-cell flow and
        // temp-transform mark. GridVisualsUpdateSystem then redraws with empty flow.
        static private void ProcessCancelling(SimulateRunState runState, SimulateRunScratch runScratch, SimulateGraphState graphState, SimulateUIState uiState, VisualGridStackState visualState, DesignMinigameState designState)
        {
            // Shared sim-state wipe. Lands runState.Phase at Idle so a subsequent Simulate
            // entry starts clean. PendingPlayRowIndex is intentionally NOT touched here —
            // ProcessIdle consumes it on the next frame to fire the queued PlaySingleTest
            // (cancel-then-play hand-off from the suite-row click handler). In toggle-input
            // mode the verdict wipe inside WipeRunState is suppressed (designState gates it).
            SimulateControlUtility.WipeRunState(runState, runScratch, graphState, uiState, visualState, designState);

            // TODO: SimulateUIUtility.ClearAllEvalMarks(uiState); HideResultsPanel(uiState).
            // TODO: dispatch DesignSimCancelled.
            // TODO: request Simulate→Tool transition via ModeTransitionState (or directly:
            //         GameLoop.SuspendUpdates(UpdateMasks.SimulateModeMask);
            //         SimulateGraphUtility.Clear(graphState);
            //         GameLoop.ResumeUpdates(UpdateMasks.ToolModeMask)).
            //       Final transition wiring lives in ModeTransitionSystem — this handler just signals.
        }

        // Sets Phase = Cancelling and clears per-test progress. Does not itself perform the wipe —
        // that's ProcessCancelling's job on the next tick.
        static private void EnterCancelling(SimulateRunState runState)
        {
            runState.Phase = SimulatePhase.Cancelling;
            runState.PhaseTimer = 0f;
            runState.CurrentDepth = 0;
            runState.PaintDepthThisFrame = false;
        }
    }
}
