using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Design.Visuals;
using SpaceFab.Save;
using SpaceFab.UI;
using TMPro;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Holds data relevant to displaying simulation results.
    /// </summary>
    public class DesignResultPanel : SharedStateComponent, IScenePreload
    {
        public ResultsPanelLayout PanelLayout;
        public SimTableLayout Table;

        // One-frame intent flag: the player clicked "Continue" on a passing results panel. Consumed
        // by DesignContinueSystem, which decides whether to advance to the next level (reload the
        // Design scene) or return to overarching (last level). Mirrors the PlayFullSuiteRequested
        // hand-off OnRetryClicked uses, keeping scene-loading out of this UI click handler.
        [NonSerialized] public bool ContinueRequested;

        public IEnumerator<WorkSlicer.Result?> Preload() {
            PanelLayout.Contract.ContextBackButton.OnClick.Register(OnRetryClicked);
            PanelLayout.Contract.ContextContinueButton.OnClick.Register(OnNextClicked);

            PanelLayout.Contract.ShowDesignCompleted(Find.State<MinigameSaveStates>().Design.FoundValidSolution);
            return null;
        }

        private void OnNextClicked()
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
        }

        private void OnRetryClicked()
        {
            // Hide panel immediately, then queue a full-suite rerun.
            // SimulateModeSystem.ProcessSuiteComplete picks up PlayFullSuiteRequested
            // on its next tick and transitions to PreparingTest.
            Find.State(out SimulateRunState runState);

            SimulateUIUtility.HideResultsPanel(Find.State<SimulateUIState>());
        }
    }

    public static class ResultStateUtility
    {
        public static void BuildResultsTable(DesignResultPanel resultState, TestSuiteData suite, SuiteVisualConfig suiteDB) {
            SimTableUtility.ConstructTable(resultState.Table, suite, suiteDB);
        }

        public static void PopulateResultsTable() {
            Find.State(
                out ContractState contractState
                );
            DesignMinigameState designState = Find.State<DesignMinigameState>();
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();

            Find.State(out SimulateUIState uiState, out DesignResultPanel resultState, out SimulateRunState simState);

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

        public static void ShowResults(DesignResultPanel resultState, bool allCorrect)
        {
            resultState.PanelLayout.HeaderText.SetText(allCorrect ? "Design Complete" : "Design Incomplete");
            resultState.PanelLayout.HeaderBackground.color = allCorrect ? resultState.PanelLayout.CompleteHeaderColor : resultState.PanelLayout.IncompleteHeaderColor;

            resultState.PanelLayout.Contract.ContextHeader.SetText(allCorrect ? "Chip Outputs Verified" : "Chip Results Unverified");

            resultState.PanelLayout.Prepare();
            resultState.PanelLayout.AnimationRoutine.Replace(resultState, DisplayRoutine(resultState, allCorrect));
        }

        static private IEnumerator DisplayRoutine(DesignResultPanel resultPanel, bool allCorrect) {
            yield return resultPanel.PanelLayout.FadeIn();
            yield return 0.1f;
            yield return resultPanel.PanelLayout.FadeInContextPanel();

            if (allCorrect) {
                Find.State(out DesignMinigameState designState, out MinigameSaveStates saveState);
                int activeIdx = designState.ActiveLevelIndex;
                int levelCount = saveState.Design.LevelCount;
                if (!resultPanel.PanelLayout.Contract.ChipDiagramFilled.enabled && activeIdx == levelCount - 1) {
                    resultPanel.PanelLayout.Contract.ChipDiagramMask.gameObject.SetActive(true);
                    resultPanel.PanelLayout.Contract.ChipDiagramMask.SetAlpha(0);
                    resultPanel.PanelLayout.Contract.ChipDiagramMask.transform.localScale = new Vector3(2, 2, 2);
                    resultPanel.PanelLayout.Contract.ChipDiagramFilled.enabled = true;
                    yield return Routine.Combine(
                        Tween.ZeroToOne(resultPanel.PanelLayout.Contract.ChipDiagramMask.SetAlpha, 0.2f),
                        resultPanel.PanelLayout.Contract.ChipDiagramMask.transform.ScaleTo(1, 0.3f, Axis.XYZ).Ease(Curve.CubeOut)
                        );
                    resultPanel.PanelLayout.Contract.ChipDiagramEmpty.enabled = false;
                }
            }

            resultPanel.PanelLayout.Finish();
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

        static public void HideResults(DesignResultPanel panel) {
            panel.PanelLayout.AnimationRoutine.Replace(panel, panel.PanelLayout.FadeOut());
        }
    }
}