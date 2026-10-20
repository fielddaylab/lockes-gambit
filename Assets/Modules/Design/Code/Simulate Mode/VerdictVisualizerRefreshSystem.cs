using FieldDay;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;

namespace SpaceFab.Design
{
    /// <summary>
    /// Repaints VerdictVisualizer icons (Correct / Incorrect / Hidden) when SimulateUIState
    /// flags the verdict visuals dirty. Reads uiState.CellVerdicts[row][col] and applies the
    /// matching sprite (or disables the icon for Hidden) to uiState.Rows[row].Verdicts[col].
    /// Runs under DesignMask so verdicts stay correct across Tool↔Simulate transitions and the
    /// initial Hidden paint after BuildTable lands while still in Tool mode.
    /// </summary>
    public class VerdictVisualizerRefreshSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 1, UpdateMasks.DesignMask),
                new SysPermissions()
                    .ReadWriteShared<SimulateUIState>()
            );
        }

        // Walks every row × col when the dirty flag is raised and applies the verdict sprite
        // (or hides the icon) per CellVerdicts. Clears the flag after repainting.
        // update: also increments a fill bar and replaces outputs once complete
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out ContractState contractState
                );
            DesignMinigameState designState = Find.State<DesignMinigameState>();
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();

            Find.State(out SimulateUIState uiState, out DesignResultPanel resultState, out SimulateRunState simState);

            if (!uiState.VerdictsNeedRefreshing) { return; }
            if (!uiState.TableBuilt) { return; }

            var suiteDB = Find.GlobalAsset<SuiteVisualConfig>();

            for(int row = 0; row < uiState.Rows.Length; row++) {
                RowVerdictSet verdicts = uiState.CellVerdicts[row];
                TestData currentResult = simState.RowValues[row];
                TestRowVerdict sumVerdict = simState.RowVerdicts[row];
                SuiteRowV2 rowLayout = uiState.TableLayout.Rows[row];

                SimTableUtility.UpdateRowOutputs(uiState.TableLayout, rowLayout, suiteDB, verdicts, currentResult);
                SimTableUtility.SetRowAppearance(uiState.TableLayout, rowLayout, suiteDB, sumVerdict);
            }

            uiState.VerdictsNeedRefreshing = false;
        }
    }
}