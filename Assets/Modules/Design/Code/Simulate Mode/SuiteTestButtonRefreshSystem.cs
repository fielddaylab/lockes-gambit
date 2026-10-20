using FieldDay;
using FieldDay.Systems;

namespace SpaceFab.Design
{
    /// <summary>
    /// Drives the toggle-input mode's single Test button:
    ///   - Hides the button when DesignMinigameState.UseToggleInputMode is false (the classic
    ///     per-row + suite-Run buttons take over).
    ///   - Whenever InputToggleState.MatchDirty is raised (toggle clicked or seed re-ran),
    ///     recomputes which TestData row matches the current toggle combo and caches the index
    ///     on InputToggleState.LastMatchedRowIndex.
    ///   - Sets the button interactable iff a matching row exists AND SimulateRunState is in a
    ///     phase that can accept a Play (CanAcceptPlay).
    /// Runs on LateUpdate at order 4 (after the classic suite-button refresh trio) under DesignMask
    /// so it stays correct across Tool↔Simulate transitions.
    /// </summary>
    public class SuiteTestButtonRefreshSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 4, UpdateMasks.DesignMask),
                new SysPermissions()
                    .ReadShared<DesignMinigameState>()
                    .ReadWriteShared<InputToggleState>()
                    .ReadShared<SimulateRunState>()
                    .ReadShared<SimulateUIState>()
                    .ReadShared<PlayerProgressState>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out SimulateRunState runState,
                out SimulateUIState uiState,
                out PlayerProgressState progressState,
                out ContractState contractState
                );
            DesignMinigameState designState = Find.State<DesignMinigameState>();
            InputToggleState toggleState = Find.State<InputToggleState>();

            if (!uiState.TableBuilt) { return; }
            
            // Refresh the matched test-row index on demand.
            if (toggleState.MatchDirty)
            {
                TestSuiteData suite = ResolveSuite(contractState, designState);
                toggleState.LastMatchedRowIndex = InputToggleUtility.FindMatchingTestRow(toggleState, suite);
                toggleState.MatchDirty = false;
                SimTableUtility.SetRowHighlighted(uiState.TableLayout, toggleState);
            }

            // Interactable iff we have a row to run AND the phase machine will accept a Play.
            bool matched = toggleState != null && toggleState.LastMatchedRowIndex >= 0;
            bool canPlay = SimulateControlUtility.CanAcceptPlay(runState);
            uiState.TableLayout.TestButton.Interactable = matched && canPlay;
        }

        // Pulls the TestSuiteData off the active Design level; returns null on a missing chain so
        // FindMatchingTestRow can short-circuit to -1.
        static private TestSuiteData ResolveSuite(ContractState contractState, DesignMinigameState designState)
        {
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            if (levelData == null) { return null; }
            return levelData.GetTestSuite();
        }
    }
}
