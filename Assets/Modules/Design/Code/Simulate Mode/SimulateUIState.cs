using FieldDay.SharedState;
using FieldDay.Systems;
using FieldDay;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;
using SpaceFab.Design.Visuals;
using BeauRoutine;
using FieldDay.UI.Widgets;
using System;
using BeauUtil.Debugger;

namespace SpaceFab.Design
{
    /// <summary>
    /// View-side state for the evaluation table UI in Simulate mode. The phase machine in
    /// SimulateModeSystem drives TableBuilt / ResultsPanelVisible / HighlightedRowIndex /
    /// UnstableBannerVisible; SimulateUIUtility performs the actual row/cell writes when the
    /// view references are populated.
    /// </summary>
    public class SimulateUIState : SharedStateComponent, IRegistrationCallbacks
    {
        #region Inspector

        public SimTableLayout TableLayout;

        #endregion // Inspector

        [NonSerialized] public bool TableBuilt;
        [NonSerialized] public bool ResultsPanelVisible;
        [NonSerialized] public int HighlightedRowIndex;
        [NonSerialized] public bool UnstableBannerVisible;

        // Set true whenever the SuiteRunRowButton icons might need to change (Phase change,
        // CurrentRow change, table built, button clicked). Consumed and cleared by
        // SuiteRunRowButtonRefreshSystem. Any future site that mutates SimulateRunState.Phase
        // or CurrentRow must also raise this flag — use SimulateUIUtility.MarkAllRunButtonsDirty
        // so the suite-level dirty flag stays in sync.
        [NonSerialized] public bool RunButtonsNeedRefreshing;

        // Set true whenever the suite-level run / restart / cancel buttons need a repaint
        // (icon swap on SuiteRunButton, interactable toggle on Restart/Cancel). Consumed by
        // SuiteRunButtonRefreshSystem (icon) and SuiteSecondaryButtonRefreshSystem (interactable),
        // cleared by the latter. Always raised together with RunButtonsNeedRefreshing via
        // SimulateUIUtility.MarkAllRunButtonsDirty.
        [NonSerialized] public bool SuiteButtonsNeedRefreshing;

        // Per-row references to instantiated view handles. SuiteRow.Cols / SuiteRow.Verdicts
        // are sized at BuildTable time, parallel to the test suite's bundle structure.
        [NonSerialized] public SuiteRowV2[] Rows;

        // Per-row × per-col verdict display state. CellVerdicts[row][col] holds the desired UI
        // state for the VerdictVisualizer at Rows[row].Verdicts[col]. Sized in CreateRowsAndCols,
        // parallel to Rows[*].Cols. VerdictsNeedRefreshing flags the array dirty for the
        // VerdictVisualizerRefreshSystem to consume.
        [NonSerialized] public RowVerdictSet[] CellVerdicts;
        [NonSerialized] public bool VerdictsNeedRefreshing;

        public void OnRegister()
        {
            TableBuilt = false;
            ResultsPanelVisible = false;
            HighlightedRowIndex = -1;
            UnstableBannerVisible = false;
            RunButtonsNeedRefreshing = false;
            SuiteButtonsNeedRefreshing = false;
            VerdictsNeedRefreshing = false;
        }

        public void OnDeregister()
        {
        }
    }

    /// <summary>
    /// View-side helpers for the evaluation table. Called by SimulateModeSystem phase handlers
    /// at row-start, row-resolve, suite-complete, dismiss, and cancel points. Bodies to be ported
    /// from the prototype's EvaluationMgr UI Construction region.
    /// </summary>
    public static class SimulateUIUtility
    {
        // Constructs the header row + one content row per test. Called once on Simulate entry.
        // Ported from EvaluationMgr.ConstructSuiteTable. designState gates the classic vs toggle-input
        // chrome: toggle mode hides every per-row Run button and the suite Run/Restart/Cancel trio,
        // surfacing only the single SuiteTestButton.
        public static void BuildTable(SimulateUIState uiState, TestSuiteData suite, SimulateRunState runState, DesignMinigameState designState, SuiteVisualConfig suiteDB)
        {
            // Per-row CellVerdicts state arrays mirror the per-row Cols/Verdicts arrays created
            // in CreateRowsAndCols. Size the outer array here so CreateRowsAndCols can fill in
            // each row's slot inline.
            uiState.CellVerdicts = new RowVerdictSet[suite.Rows.Length];
            
            uiState.Rows = new SuiteRowV2[suite.Rows.Length];
            for(int i = 0; i < suite.Rows.Length; i++) {
                uiState.Rows[i] = uiState.TableLayout.Rows[i];
            }

            // instantiate headers and size table
            SimTableUtility.ConstructTable(uiState.TableLayout, suite, suiteDB);

            // instantiate rows and cols

            // hook into run state for play, pause, rewind, etc.
            AssignSuiteListeners(uiState, runState);

            uiState.TableBuilt = true;

            // Trigger the initial icon + verdict paint on every row, plus the suite-level controls.
            MarkAllRunButtonsDirty(uiState);
            uiState.VerdictsNeedRefreshing = true;
        }

        // Raises both the per-row and suite-level run-button dirty flags. Use this anywhere
        // SimulateRunState.Phase or CurrentRow changes — the row buttons and the suite buttons
        // both derive their state from those, and missing one drifts silently.
        public static void MarkAllRunButtonsDirty(SimulateUIState uiState)
        {
            uiState.RunButtonsNeedRefreshing = true;
            uiState.SuiteButtonsNeedRefreshing = true;
        }

        // Disables every eval image and flow image on the table. Ported from EvaluationMgr.ClearSuiteEvals.
        public static void ClearAllEvalMarks(SimulateUIState uiState)
        {
            // TODO: for each SuiteCellEval in the table, disable Img.
            // TODO: for each SuiteContents in the table, disable FlowImg.
        }

        // Writes the input values for a row into the contents cells (skipping output columns).
        // Called at the start of each test during PreparingTest.
        public static void WriteRowInputs(SimulateUIState uiState, int rowIndex, TestData test)
        {
            // TODO: for each non-output column in row rowIndex, look up the value in test.Bundle
            //       by its InputOutputNodeTypeFlags and set the contents cell's FlowImg sprite.
            var suiteDB = Find.GlobalAsset<SuiteVisualConfig>();

            SuiteRowV2 row = uiState.Rows[rowIndex];
            row.LeftProgress.enabled = true;
            row.RightProgress.enabled = true;
            row.LeftProgress.color = suiteDB.RowPendingLineLeftColor;
            row.RightProgress.color = suiteDB.RowPendingLineRightColor;
        }

        // Records per-output verdict outcomes for a row into uiState.CellVerdicts and flags the
        // verdict visuals dirty. Called from ProcessResolvingTest after scoring. actualPerCol is
        // sized to currTest.Bundle.Length, indexed by bundle column; non-output columns are
        // skipped. State only — VerdictVisualizerRefreshSystem applies the sprites.
        public static void WriteRowVerdict(SimulateUIState uiState, int rowIndex, TestData currTest, TestData actualPerCol)
        {
            Assert.True(rowIndex >= 0 && rowIndex < uiState.CellVerdicts.Length);

            ref RowVerdictSet verdicts = ref uiState.CellVerdicts[rowIndex];
            verdicts.OutputX = actualPerCol.OutputX == currTest.OutputX ? CellVerdict.Correct : CellVerdict.Incorrect;
            verdicts.OutputY = actualPerCol.OutputY == currTest.OutputY ? CellVerdict.Correct : CellVerdict.Incorrect;

            uiState.VerdictsNeedRefreshing = true;
        }

        // Marks every output column across every row as Correct in uiState.CellVerdicts and
        // flags the verdict visuals dirty. Called from SimTableLoadSystem on Design entry when
        // the player has previously found a valid solution (DesignMinigameState.FoundValidSolution)
        // — the contract is already solved, so the suite reads as passing on entry instead of
        // requiring the player to re-run.
        public static void MarkAllRowsCorrect(SimulateUIState uiState, TestSuiteData suite)
        {
            for (int row = 0; row < suite.Rows.Length; row++)
            {
                ref RowVerdictSet verdicts = ref uiState.CellVerdicts[row];
                verdicts.OutputX = verdicts.OutputY = CellVerdict.Correct;
            }

            uiState.VerdictsNeedRefreshing = true;
        }

        // Marks every output column in the given row as Hidden in uiState.CellVerdicts and
        // flags the verdict visuals dirty. Called from ProcessPreparingTest so a re-run doesn't
        // keep the previous run's verdict visible while the new propagation plays out.
        public static void HideRowVerdicts(SimulateUIState uiState, int rowIndex)
        {
            ref RowVerdictSet verdicts = ref uiState.CellVerdicts[rowIndex];
            verdicts.OutputX = verdicts.OutputY = CellVerdict.Hidden;
            uiState.VerdictsNeedRefreshing = true;
        }

        // Marks every cell across every row as Hidden. Called when starting a single-test run so
        // previously-resolved verdicts don't linger on inactive rows. Full-suite runs intentionally
        // don't call this — verdicts preserve between tests within the suite, accumulating as
        // each row resolves.
        public static void HideAllRowVerdicts(SimulateUIState uiState)
        {
            for (int row = 0; row < uiState.CellVerdicts.Length; row++)
            {
                ref RowVerdictSet verdicts = ref uiState.CellVerdicts[row];
                verdicts.OutputX = verdicts.OutputY = CellVerdict.Hidden;
            }

            uiState.VerdictsNeedRefreshing = true;
        }

        // Shows the overall results panel. Called when entering SuiteComplete from
        // SimulateModeSystem.ProcessResolvingTest (single-test or full-suite final row).
        //
        // HOOK: when the results panel work lands, wire it here.
        //   - add an inspector-assigned panel GameObject ref on SimulateUIState
        //     (e.g. ResultsPanelRoot).
        //   - SetActive(true), set header text via allCorrect.
        //   - uiState.ResultsPanelVisible = true.
        // Body intentionally empty until that work lands so the future implementer has a
        // single, named attachment point.
        public static void ShowResultsPanel(SimulateUIState uiState, bool allCorrect)
        {
            uiState.ResultsPanelVisible = true;
            ResultStateUtility.PopulateResultsTable();
            ResultStateUtility.ShowResults(Find.State<DesignResultPanel>(), allCorrect);
        }

        // Hides the results panel. Called on Dismiss or on a fresh Play from SuiteComplete.
        public static void HideResultsPanel(SimulateUIState uiState)
        {
            uiState.ResultsPanelVisible = false;
            ResultStateUtility.HideResults(Find.State<DesignResultPanel>());
        }

        #region Helpers

        // Wires the suite-level run / restart / cancel buttons (classic mode) and the single
        // Test button (toggle mode) to their click handlers. Refs may be null until the prefab
        // layout for the suite-level toolbar is finalized; skip wiring any null slot rather than failing.
        private static void AssignSuiteListeners(SimulateUIState uiState, SimulateRunState runState)
        {
            uiState.TableLayout.TestButton.OnClick.Register(() => HandleSuiteTestButtonClick(runState, uiState));
        }

        // Toggle-input mode Test click. Reads the matched test-row index that
        // SuiteTestButtonRefreshSystem last computed; the request is dropped silently when no
        // row matches the current toggle combo (the button should also be greyed in that case).
        private static void HandleSuiteTestButtonClick(SimulateRunState runState, SimulateUIState uiState)
        {
            InputToggleState toggleState = Find.State<InputToggleState>();
            int matched = toggleState != null ? toggleState.LastMatchedRowIndex : -1;
            SimulateControlUtility.RequestPlayCurrentToggleCombo(runState, matched);
            MarkAllRunButtonsDirty(uiState);
        }

        #endregion // Helpers
    }
}
