using System;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Design.Visuals;
using SpaceFab.UI;
using TMPro;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Holds data relevant to displaying simulation results.
    /// </summary>
    public class ResultState : SharedStateComponent, IRegistrationCallbacks
    {
        public CanvasGroup ResultsGroup;
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI SummaryText;
        public GuiButton DismissButton;
        public GuiButton RetryButton;
        public SimTableLayout Table;
        public CanvasInputLayer Input;

        // One-frame intent flag: the player clicked "Continue" on a passing results panel. Consumed
        // by DesignContinueSystem, which decides whether to advance to the next level (reload the
        // Design scene) or return to overarching (last level). Mirrors the PlayFullSuiteRequested
        // hand-off OnRetryClicked uses, keeping scene-loading out of this UI click handler.
        [NonSerialized] public bool ContinueRequested;

        public void OnRegister()
        {

            if (ResultsGroup != null)
            {
                ResultStateUtility.SetEnabledResultsGroup(this, false);
            }

            if (DismissButton != null)
            {
                DismissButton.gameObject.SetActive(true);
                DismissButton.OnClick.AddListener(OnDismissClicked);
            }

            if (RetryButton != null)
            {
                RetryButton.gameObject.SetActive(true);
                RetryButton.OnClick.AddListener(OnRetryClicked);
            }
        }

        public void OnDeregister()
        {
            if (DismissButton != null)
            {
                DismissButton.OnClick.RemoveListener(OnDismissClicked);
            }

            if (RetryButton != null)
            {
                RetryButton.OnClick.RemoveListener(OnRetryClicked);
            }
        }

        private void OnDismissClicked()
        {
            // "Continue" on a passing panel: hand off to DesignContinueSystem, which advances to the
            // next level (Design-scene reload) or returns to overarching on the last level. A failing
            // panel just dismisses.
            Find.State(out SimulateRunState runState);
            runState.PlayFullSuiteRequested = false; // in case we were on a single-test pass
            if (ResultStateUtility.IsAllCorrect(runState))
            {
                ContinueRequested = true;
            }
            ResultStateUtility.SetEnabledResultsGroup(this, false);
        }

        private void OnRetryClicked()
        {
            // Hide panel immediately, then queue a full-suite rerun.
            // SimulateModeSystem.ProcessSuiteComplete picks up PlayFullSuiteRequested
            // on its next tick and transitions to PreparingTest.
            Find.State(out SimulateRunState runState);
            ResultStateUtility.SetEnabledResultsGroup(this, false);
            runState.PlayFullSuiteRequested = true;
        }
    }

    public static class ResultStateUtility
    {
        public static void BuildResultsTable(ResultState resultState, TestSuiteData suite, SuiteVisualConfig suiteDB) {
            SimTableUtility.ConstructTable(resultState.Table, suite, suiteDB);
        }

        public static void PopulateResultsTable() {
            Find.State(
                out ContractState contractState
                );
            DesignMinigameState designState = Find.State<DesignMinigameState>();
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();

            Find.State(out SimulateUIState uiState, out ResultState resultState, out SimulateRunState simState);

            var suiteDB = Find.GlobalAsset<SuiteVisualConfig>();

            for (int row = 0; row < uiState.Rows.Length; row++) {
                RowVerdictSet verdicts = uiState.CellVerdicts[row];
                TestData currentResult = simState.RowValues[row];
                TestRowVerdict sumVerdict = simState.RowVerdicts[row];

                SuiteRowV2 rowLayout = resultState.Table.Rows[row];

                SimTableUtility.UpdateRowOutputs(resultState.Table, rowLayout, suiteDB, verdicts, currentResult);
                SimTableUtility.SetRowAppearance(resultState.Table, rowLayout, suiteDB, sumVerdict);
            }
        }

        public static void SetEnabledResultsGroup(ResultState resultState, bool isEnabled)
        {
            if (resultState.ResultsGroup == null)
            {
                Debug.LogWarning("ResultState.ResultsGroup is null!");
                return;
            }

            resultState.ResultsGroup.alpha = isEnabled ? 1f : 0f;
            resultState.ResultsGroup.blocksRaycasts = isEnabled;
            resultState.ResultsGroup.interactable = isEnabled;
            if (isEnabled) {
                resultState.Input.TryPushPriority();
            } else {
                resultState.Input.TryPopPriority();
            }
        }

        public static void ShowResults(ResultState resultState, bool allCorrect)
        {
            if (resultState.TitleText != null)
            {
                resultState.TitleText.SetText(allCorrect ? "Design complete" : "Failure");
                resultState.TitleText.color = allCorrect ? Color.white : Color.red;
            }

            if (resultState.SummaryText != null)
            {
                resultState.SummaryText.SetText(
                    allCorrect
                        ? "All outputs matched the expected values."
                        : "Some outputs were incorrect or unstable.");
            }

            SetEnabledResultsGroup(resultState, true);
        }

        static public bool IsAllCorrect(SimulateRunState runState)
        {
            TestRowVerdict[] verdicts = runState.RowVerdicts;
            for (int i = 0; i < verdicts.Length; i++)
            {
                if (verdicts[i] != TestRowVerdict.Correct) { return false; }
            }
            return true;
        }
    }
}