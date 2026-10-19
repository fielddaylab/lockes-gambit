using FieldDay;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Facilitates transitioning between Design minigame modes (Tool vs. Simulate). On
    /// Tool→Simulate, rebuilds the evaluation graph from the current grid (the player may have
    /// edited it in Tool mode), sizes per-test scratch arrays, allocates RowVerdicts, and flips
    /// the active update mask. The transition is implicit — any play request from Tool mode (a
    /// suite-row click setting PlaySingleTestRequested, a future Play-All button setting
    /// PlayFullSuiteRequested, or a queued PendingPlayRowIndex) triggers entry.
    /// Simulate→Tool is not yet implemented.
    /// Runs on Update at order 0, no category mask (so it's reachable from Tool mode).
    /// </summary>
    public class ModeTransitionSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0),
                new SysPermissions()
                    .ReadWriteShared<ModeTransitionState>()
                    .ReadWriteShared<SimulateRunState>()
                    .ReadWriteShared<SimulateGraphState>()
                    .ReadWriteShared<SimulateGraphBuildScratch>()
                    .ReadWriteShared<SimulateRunScratch>()
                    .ReadShared<GridStackState>()
                    .ReadWriteShared<SimulateUIState>()
                    .ReadWriteShared<VisualGridStackState>()
                    .ReadWriteShared<PlayerProgressState>()
                    .ReadShared<DesignMinigameState>()
                    .Read<ToolbarButton>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out ModeTransitionState modeState,
                out SimulateRunState runState,
                out SimulateGraphState graphState,
                out SimulateGraphBuildScratch graphBuildScratch
                );
            Find.State(
                out SimulateRunScratch runScratch,
                out GridStackState gridStackState,
                out SimulateUIState uiState,
                out VisualGridStackState visualState
                );

            Find.State(
                out PlayerProgressState progressState,
                out DesignMinigameState designState,
                out ContractState contractState
                );

            // Tool → Simulate. Any pending play indicator triggers entry; we run before
            // SimulateModeSystem (Update order 1) so the freshly-built graph is in place when
            // ProcessIdle consumes the play flag the same frame.
            if (modeState.Mode == DesignMode.Tool)
            {
                bool playRequested = runState.PlayFullSuiteRequested
                    || runState.PlaySingleTestRequested
                    || runState.PlayCurrentToggleComboRequested
                    || runState.PendingPlayRowIndex >= 0;
                if (playRequested)
                {
                    VisualGridStackUtility.SetOutlineVisible(visualState, true);
                    EnterSimulateMode(modeState, runState, graphState, graphBuildScratch, runScratch, gridStackState, progressState, contractState, visualState, designState);
                }
            }
            // Simulate → Tool. Any toolbar-button click triggers exit. The click flag survives
            // until ToolbarSelectSystem (PreUpdate 0, ToolModeMask) sees it next frame, which
            // happens automatically once we flip masks here — so the click both transitions and
            // selects the tool with no manual click routing.
            else if (modeState.Mode == DesignMode.Simulate)
            {
                if (AnyToolbarClickThisFrame())
                {
                    ExitSimulateMode(modeState, runState, runScratch, graphState, uiState, visualState, designState);
                }
            }
        }

        // True if any available toolbar button has its one-frame click flag set this frame.
        // Used in Simulate mode to detect the player's intent to return to Tool mode.
        static private bool AnyToolbarClickThisFrame()
        {
            var buttons = Find.Components<ToolbarButton>();
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Available && buttons[i].ClickedThisFrame) { return true; }
            }
            return false;
        }

        // Builds the evaluation graph from the current grid, sizes scratch arrays, allocates
        // RowVerdicts, and flips the active update mask from Tool to Simulate. Idempotent within
        // a single tick — caller gates on modeState.Mode.
        static private void EnterSimulateMode(ModeTransitionState modeState, SimulateRunState runState, SimulateGraphState graphState, SimulateGraphBuildScratch graphBuildScratch, SimulateRunScratch runScratch, GridStackState gridStackState, PlayerProgressState progressState, ContractState contractState, VisualGridStackState visualState, DesignMinigameState designState)
        {
            // Rebuild graph from the current grid. The player may have edited the grid in Tool
            // mode; the prior graph (if any) is stale.
            SimulateGraphUtility.Build(graphState, graphBuildScratch, gridStackState);

            // Per-test scratch sized to the freshly-built graph + grid.
            Dimensions dims = gridStackState.GridStack.LayerDims;
            int cellCount = gridStackState.GridStack.GridLayers.Length * dims.X * dims.Y;
            SimulateRunScratchUtility.EnsureCapacity(runScratch, graphState.NodeCount, graphState.SegmentCount, cellCount);

            // Output buffer sized to the count of Output crucial nodes in graph order — matches
            // the iteration order ProcessResolvingTest uses to write into OutputFlowBuffer.
            int outputCount = 0;
            for (int i = 0; i < graphState.NodeCount; i++)
            {
                GridCell cell = GridStackUtility.GetCellDirect(gridStackState, graphState.CrucialNodes[i].Coord);
                if (cell.CellType == CellType.Output) { outputCount++; }
            }
            SimulateRunScratchUtility.SizeOutputBuffer(runScratch, outputCount);

            // Allocate RowVerdicts to match the suite length. Reuse the existing array when its
            // length already matches, otherwise allocate fresh (default TestRowVerdict.Untested).
            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suite = levelData.GetTestSuite();
            if (runState.RowVerdicts == null || runState.RowVerdicts.Length != suite.Rows.Length)
            {
                runState.RowVerdicts = new TestRowVerdict[suite.Rows.Length];
            }

            // Flip masks. Both calls are idempotent in GameLoop, so no harm if a future
            // path enters this in an already-Simulate state (currently gated by modeState.Mode).
            GameLoop.SuspendUpdates(UpdateMasks.ToolModeMask);
            GameLoop.ResumeUpdates(UpdateMasks.SimulateModeMask);

            VisualGridStackUtility.SetOutlineVisible(visualState, true);
            ToolbarUtility.DeselectTool(Find.State<ToolModeState>(), Find.State<ToolbarState>());
            modeState.Mode = DesignMode.Simulate;

        }

        // Wipes the active simulation back to Idle, clears any queued play, and flips the active
        // update mask from Simulate to Tool. The toolbar button's one-frame click flag is left
        // alone — it'll survive until ToolbarSelectSystem (now active under ToolModeMask) sees
        // it next frame, which selects the clicked tool with no extra click routing.
        static private void ExitSimulateMode(ModeTransitionState modeState, SimulateRunState runState, SimulateRunScratch runScratch, SimulateGraphState graphState, SimulateUIState uiState, VisualGridStackState visualState, DesignMinigameState designState)
        {
            Debug.Log("<color=red>Exit simulate mode</color>");
            // Shared sim-state wipe: bump flow stamp, clear node transients, mark visuals dirty,
            // park Phase at Idle, flag run-button repaint. In toggle-input mode, verdicts persist
            // through this path (only grid edits clear them) — designState gates that.
            SimulateControlUtility.WipeRunState(runState, runScratch, graphState, uiState, visualState, designState);

            // Discard any queued play — the player's intent is to edit, not to run another test.
            runState.PendingPlayRowIndex = -1;

            GameLoop.SuspendUpdates(UpdateMasks.SimulateModeMask);
            GameLoop.ResumeUpdates(UpdateMasks.ToolModeMask);

            VisualGridStackUtility.SetOutlineVisible(visualState, false);
            modeState.Mode = DesignMode.Tool;
        }
    }
}