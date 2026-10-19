using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Design.Visuals;
using System;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// One per-input toggle record. CellIndex is the flat (layer, col, row) index produced by
    /// SimulateRunScratchUtility.CellIndex — encodes the grid coordinate in a single int so
    /// IndexOfCellIndex stays an int compare. Subtype is the InputOutputNodeTypeFlags label baked
    /// onto the cell; State is the player's current Lo/Hi choice (defaults that aren't binary get
    /// normalised to Lo at contract-confirm time).
    /// </summary>
    [Serializable]
    public struct InputToggleEntry
    {
        public int CellIndex;
        public InputOutputNodeTypeFlags Subtype;
        public FlowState State;
    }

    /// <summary>
    /// Holds the runtime Lo/Hi toggle state for every Input cell in the current grid stack
    /// (toggle-input mode). Drives the matching of the player's current toggle combo against
    /// TestSuiteData rows for the single-button "Test" flow, and is the source of truth for the
    /// per-input overlay sprites. Data-only — all mutation, lookup, and import/export go through
    /// InputToggleUtility.
    ///
    /// Lifecycle mirrors GridStackState: seeded into the save state once at contract confirm
    /// (InputToggleUtility.SeedDefaultsFromConfig), imported into runtime on Design entry
    /// (DesignStateUtility.ImportState), exported back on minigame save. No in-session reseeding —
    /// input cells are immutable per-level, so the entry set is stable.
    /// </summary>
    public class InputToggleState : SharedStateComponent, IRegistrationCallbacks
    {
        // Compact runtime entries; the first InputCount slots are valid. May be oversized.
        [NonSerialized] public InputToggleEntry[] Inputs;
        [NonSerialized] public int InputCount;

        // Index into the active TestSuiteData.Tests of the row that matches the current toggle
        // combo, or -1 if no row matches. Set by SuiteTestButtonRefreshSystem on every refresh.
        [NonSerialized] public int LastMatchedRowIndex;

        // Raised by any toggle mutation (HandleToggleClick) and by Import. Consumed by
        // SuiteTestButtonRefreshSystem to recompute LastMatchedRowIndex.
        [NonSerialized] public bool MatchDirty;

        public void OnRegister()
        {
            Inputs = null;
            InputCount = 0;
            LastMatchedRowIndex = -1;
            MatchDirty = false;
        }

        public void OnDeregister()
        {
        }
    }

    /// <summary>
    /// Serializable entry written into DesignSaveState's input-toggle chunk. Mirrors
    /// InputToggleEntry but typed separately so the on-disk format is decoupled from the runtime
    /// state shape. CellIndex follows the same encoding the rest of the grid uses
    /// (SimulateRunScratchUtility.CellIndex).
    /// </summary>
    [Serializable]
    public struct InputToggleSaveEntry
    {
        public int CellIndex;
        public InputOutputNodeTypeFlags Subtype;
        public FlowState State;
    }

    /// <summary>
    /// Container for the input-toggle save chunk. Count-prefixed list of entries; Entries may be
    /// oversized beyond Count to amortise allocations across repeated saves.
    /// </summary>
    [Serializable]
    public struct InputToggleSaveData
    {
        public InputToggleSaveEntry[] Entries;
        public int Count;
    }

    /// <summary>
    /// Mutation, lookup, save/load, and visual-stamp helpers for InputToggleState. Mirrors
    /// GridStackUtility's role: the save state owns persistence; this utility moves data between
    /// save / runtime and applies per-frame mutations.
    /// </summary>
    public static class InputToggleUtility
    {
        #region Lookup

        // Linear scan over the active entries; Inputs is tiny (one slot per Input cell in the grid).
        public static int IndexOfCellIndex(InputToggleState state, int cellIndex)
        {
            if (state == null || state.Inputs == null) { return -1; }
            for (int i = 0; i < state.InputCount; i++)
            {
                if (state.Inputs[i].CellIndex == cellIndex) { return i; }
            }
            return -1;
        }

        // Scans the suite for a TestData row whose Bundle matches the current toggle combo for
        // every constrained input subtype. Empty (unconstrained) subtypes are skipped so a test
        // that omits a subtype the grid happens to expose still matches. Returns -1 if none match.
        public static int FindMatchingTestRow(InputToggleState state, TestSuiteData suite)
        {
            if (state.InputCount == 0) { return -1; }

            for (int testIdx = 0; testIdx < suite.Rows.Length; testIdx++)
            {
                TestData td = suite.Rows[testIdx];
                bool match = true;
                for (int i = 0; i < state.InputCount; i++)
                {
                    InputToggleEntry entry = state.Inputs[i];
                    FlowState expected = EvalUtility.GetTestValBySubType(entry.Subtype, suite.ColumnMask, td);
                    if (expected == FlowState.Empty) { continue; }
                    if (entry.State != expected) { match = false; break; }
                }
                if (match) { return testIdx; }
            }
            return -1;
        }

        // Gates toggle clicks: Tool mode always allows; Simulate mode allows only between runs
        // (Idle / SuiteComplete). Mid-propagation clicks are dropped on the floor.
        public static bool CanAcceptToggle(ModeTransitionState modeState, SimulateRunState runState)
        {
            if (modeState.Mode == DesignMode.Tool) { return true; }
            return runState.Phase == SimulatePhase.Idle || runState.Phase == SimulatePhase.SuiteComplete;
        }

        #endregion // Lookup

        #region Mutation

        // Flips a single entry's state Lo↔Hi. Non-binary states (Empty / Unstable) snap to Hi.
        // Raises MatchDirty so the next refresh recomputes LastMatchedRowIndex.
        public static void ToggleEntry(InputToggleState state, int entryIndex)
        {
            Assert.False(state.Inputs == null, "No input cell");
            Assert.False(entryIndex < 0 || entryIndex >= state.InputCount, "Cell index out of range");

            // Cannot flip if default value is pre-assigned
            if (state.Inputs[entryIndex].Subtype == InputOutputNodeTypeFlags.VMINUS
                || state.Inputs[entryIndex].Subtype == InputOutputNodeTypeFlags.VPLUS) {
                return;
            }

            FlowState curr = state.Inputs[entryIndex].State;
            state.Inputs[entryIndex].State = (curr == FlowState.Hi) ? FlowState.Lo : FlowState.Hi;
            state.MatchDirty = true;

            ScriptUtility.Trigger(DesignScriptTriggers.OnInputToggled);
        }

        // Short identifier label for an input subtype ("IN", "A", "B", "C"). Used by
        // SpawnInputOverlays to write the per-overlay SubtypeText once on spawn. Returns the
        // empty string for non-input subtypes — those cells don't host overlays.
        // TODO: hook up with Loc system (mirrors SimulateUIUtility.GetLocTextForId).
        public static string GetInputSubtypeShortLabel(InputOutputNodeTypeFlags id)
        {
            if ((id & InputOutputNodeTypeFlags.IN) != 0) { return "IN"; }
            if ((id & InputOutputNodeTypeFlags.A) != 0) { return "A"; }
            if ((id & InputOutputNodeTypeFlags.B) != 0) { return "B"; }
            if ((id & InputOutputNodeTypeFlags.C) != 0) { return "C"; }
            if ((id & InputOutputNodeTypeFlags.VMINUS) != 0) { return "LO"; }
            if ((id & InputOutputNodeTypeFlags.VPLUS) != 0) { return "HI"; }
            return string.Empty;
        }

        // "LO" / "HI" label inside the toggle pill. Anything not Hi reads as "LO" so unstable /
        // empty edge cases stay visually consistent with the bool toggle the player sees.
        public static string GetStateShortLabel(FlowState state)
        {
            return state == FlowState.Hi ? "HI" : "LO";
        }

        // Click entry point. Called from InputToggleVisual.HandleClick — looks up state via
        // Find.State so the view component itself stays a pure data container.
        public static void HandleToggleClick(int cellIndex)
        {
            InputToggleState state = Find.State<InputToggleState>();
            ModeTransitionState modeState = Find.State<ModeTransitionState>();
            SimulateRunState runState = Find.State<SimulateRunState>();
            if (!CanAcceptToggle(modeState, runState)) { return; }

            int idx = IndexOfCellIndex(state, cellIndex);
            ToggleEntry(state, idx);

            // Any flow visuals on the grid from the prior test no longer correspond to the
            // freshly-changed toggle combo. Bump the flow stamp to invalidate every per-cell
            // mark and flag the visuals for repaint — verdicts in the suite table are unaffected.
            InvalidateFlowVisuals();
        }

        // Wipes the grid's per-cell flow paint without touching verdicts, phase, or any other
        // run state. Used after a toggle change so the just-shown propagation visuals don't
        // misrepresent the new input combination.
        private static void InvalidateFlowVisuals()
        {
            SimulateRunScratch runScratch = Find.State<SimulateRunScratch>();
            VisualGridStackState visualState = Find.State<VisualGridStackState>();
            if (runScratch != null) { SimulateRunScratchUtility.BumpFlowStamp(runScratch); }
            if (visualState != null) { visualState.VisualsNeedRefreshing = true; }
        }

        #endregion // Mutation

        #region Save / Load

        // Contract-confirm seed. Walks the level's GridStackConfig and writes one save entry per
        // Input cell, taking each cell's authored DefaultInputState (normalised to Lo if not binary).
        // Mirrors the parallel GridStackUtility.LoadConfig call that seeds the grid into the
        // save state at contract confirm. Encodes each (layer, col, row) into a single flat
        // CellIndex using the canonical SimulateRunScratchUtility formula.
        public static void SeedDefaultsFromConfig(ref InputToggleSaveData saveData, GridStackConfig config)
        {
            if (config == null || config.Cells == null)
            {
                saveData.Count = 0;
                return;
            }

            int inputCount = 0;
            for (int i = 0; i < config.Cells.Length; i++)
            {
                if (config.Cells[i].CellType == CellType.Input) { inputCount++; }
            }

            if (saveData.Entries == null || saveData.Entries.Length < inputCount)
            {
                saveData.Entries = new InputToggleSaveEntry[Math.Max(inputCount, 4)];
            }

            int numCols = DesignConsts.NUM_GRID_COLS;
            int cellsPerLayer = numCols * DesignConsts.NUM_GRID_ROWS;

            int idx = 0;
            for (int i = 0; i < config.Cells.Length; i++)
            {
                GridCellConfig cc = config.Cells[i];
                if (cc.CellType != CellType.Input) { continue; }

                FlowState defaultState = cc.DefaultInputState;
                if (defaultState != FlowState.Hi && defaultState != FlowState.Lo) { defaultState = FlowState.Lo; }

                saveData.Entries[idx].CellIndex = SimulateRunScratchUtility.CellIndex((int)cc.LayerIndex, cc.ColumnIndex, cc.RowIndex, numCols, cellsPerLayer);
                saveData.Entries[idx].Subtype = cc.SubtypeLabel;
                saveData.Entries[idx].State = defaultState;
                idx++;
            }
            saveData.Count = inputCount;
        }

        // Direct save → runtime copy. Called from DesignStateUtility.ImportState on Design entry.
        // The save state is the authoritative seed (contract confirm wrote defaults; player edits
        // are written back via Export on minigame save), so no merge logic needed.
        public static void ImportFromSaveData(InputToggleState state, InputToggleSaveData saveData)
        {
            int count = saveData.Count;
            if (count <= 0 || saveData.Entries == null)
            {
                state.InputCount = 0;
                state.MatchDirty = true;
                return;
            }

            if (state.Inputs == null || state.Inputs.Length < count)
            {
                state.Inputs = new InputToggleEntry[count];
            }
            for (int i = 0; i < count; i++)
            {
                state.Inputs[i].CellIndex = saveData.Entries[i].CellIndex;
                state.Inputs[i].Subtype = saveData.Entries[i].Subtype;
                state.Inputs[i].State = saveData.Entries[i].State;
            }
            state.InputCount = count;
            state.MatchDirty = true;
        }

        // Direct runtime → save copy. Called from DesignStateUtility.ExportState on minigame save.
        // Reuses the destination's Entries array when it's large enough so repeated saves don't
        // churn the heap.
        public static void ExportToSaveData(InputToggleState state, ref InputToggleSaveData saveData)
        {
            int needed = state != null ? state.InputCount : 0;
            if (saveData.Entries == null || saveData.Entries.Length < needed)
            {
                saveData.Entries = new InputToggleSaveEntry[Math.Max(needed, 4)];
            }
            for (int i = 0; i < needed; i++)
            {
                InputToggleEntry src = state.Inputs[i];
                saveData.Entries[i].CellIndex = src.CellIndex;
                saveData.Entries[i].Subtype = src.Subtype;
                saveData.Entries[i].State = src.State;
            }
            saveData.Count = needed;
        }

        #endregion // Save / Load

        #region Visuals

        // Returns every active input-toggle overlay to the pool. Called as the first step of
        // SpawnInputOverlays (clean slate before re-allocating for the freshly-loaded grid).
        public static void FreeAllInputOverlays(DesignPools pools)
        {
            Assert.False(pools.ActiveInputToggleOverlays == null, "No active input toggle overlays");
            int n = pools.ActiveInputToggleOverlays.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                InputToggleVisual overlay = pools.ActiveInputToggleOverlays[i];
                if (overlay != null)
                {
                    // Clear the onboarding tag id before pooling so the lookup doesn't carry
                    // stale entries pointing at parked overlays across level reloads.
                    if (overlay.Tag != null) { overlay.Tag.SetId(default(StringHash32)); }
                    Pool.TryFree(overlay);
                }
            }
            pools.ActiveInputToggleOverlays.Clear();
        }

        // Walks the loaded grid, allocs one overlay from the pool per Input cell, positions it
        // at the matching VisualGridCell's worldspace location, applies the shared sprites from
        // GridSpriteDB, writes the per-input subtype label, stamps its CellIndex, and registers
        // it in the pool's Active list. Frees any previously-active overlays first so a level
        // transition leaves a clean set. Called from GridStackLoadSystem after
        // VisualGridStackUtility.Init builds the visual cells.
        public static void SpawnInputOverlays(GridStackState gridStackState, VisualGridStackState visualState, DesignPools pools)
        {
            FreeAllInputOverlays(pools);

            Assert.False(gridStackState.GridStack == null, "Null GridStack");
            Assert.False(gridStackState.GridStack.GridLayers == null, "Null GridLayers in GridStack");
            Assert.False(visualState.VisualGridStack == null, "Null VisualGridStack");
            Assert.False(visualState.VisualGridStack.GridLayers == null, "Null GridLayers in VisualGridStack");

            GridStack stack = gridStackState.GridStack;
            int layerLimit = stack.GridLayers.Length;
            if (visualState.VisualGridStack.GridLayers.Length < layerLimit) { layerLimit = visualState.VisualGridStack.GridLayers.Length; }

            int numCols = DesignConsts.NUM_GRID_COLS;
            int cellsPerLayer = numCols * DesignConsts.NUM_GRID_ROWS;

            GridSpriteDB spriteDB = Find.GlobalAsset<GridSpriteDB>();

            for (int layer = 0; layer < layerLimit; layer++)
            {
                VisualGridLayer visualLayer = visualState.VisualGridStack.GridLayers[layer];
                if (visualLayer == null) { continue; }

                for (int col = 0; col < DesignConsts.NUM_GRID_COLS; col++)
                {
                    for (int row = 0; row < DesignConsts.NUM_GRID_ROWS; row++)
                    {
                        GridCell cell = GridStackUtility.GetCellDirect(gridStackState, layer, col, row);
                        if (cell == null || cell.CellType != CellType.Input) { continue; }

                        VisualGridCell visualCell = visualLayer.GetCell(col, row);
                        if (visualCell == null) { continue; }

                        InputToggleVisual overlay = pools.InputToggleOverlayPool.Alloc();
                        if (overlay == null) { continue; }

                        // Match the cell's worldspace position so the overlay sits in front of it.
                        // Sorting order is owned by the prefab — Input cells typically live on the
                        // top layer, but we don't override here to keep the asset side authoritative.
                        overlay.transform.position = visualCell.transform.position;
                        overlay.transform.Translate(0, VisualGridCellUtility.METAL_OFFSET, 0, Space.Self);

                        // Exclude constant hi/lo inputs visual renders as their visuals are updated in
                        // VisualGridCell with different visual designs.
                        if (cell.SubtypeLabel != InputOutputNodeTypeFlags.VPLUS && cell.SubtypeLabel != InputOutputNodeTypeFlags.VMINUS)
                        {
                            ApplyOverlayCommonVisuals(overlay, spriteDB);
                            ApplyOverlaySubtypeLabel(overlay, cell.SubtypeLabel);


                            overlay.CellIndex = SimulateRunScratchUtility.CellIndex(layer, col, row, numCols, cellsPerLayer);
                            overlay.CellIndexStamped = true;

                            pools.ActiveInputToggleOverlays.Add(overlay);
                        }
                    }
                }
            }
        }

        // Assigns the shared (Lo/Hi-independent) sprites onto a freshly-spawned overlay. Called
        // once per spawn; per-frame color tinting + state text live in InputToggleSystem.
        private static void ApplyOverlayCommonVisuals(InputToggleVisual overlay, GridSpriteDB spriteDB)
        {
            if (overlay.BackgroundRenderer != null && spriteDB.InputToggleBackgroundHi != null)
            {
                overlay.BackgroundRenderer.sprite = spriteDB.InputToggleBackgroundHi;
            }
            if (overlay.ArrowRenderer != null && spriteDB.InputToggleArrow != null)
            {
                overlay.ArrowRenderer.sprite = spriteDB.InputToggleArrow;
            }
        }

        // Writes the per-input subtype label ("A", "B", ...) once on spawn — the input cell's
        // SubtypeLabel doesn't change after the grid is loaded. Also stamps the onboarding
        // ElementTag id ("design:input-a", "design:input-in", ...) so Leaf tutorial calls can
        // address the overlay by subtype. Subtype is lowercased so the id format is consistent
        // with other "module:kebab-case-name" event ids in the project.
        private static void ApplyOverlaySubtypeLabel(InputToggleVisual overlay, InputOutputNodeTypeFlags subtype)
        {
            string shortLabel = GetInputSubtypeShortLabel(subtype);
            if (overlay.SubtypeText != null)
            {
                overlay.SubtypeText.SetText(shortLabel);
            }
            if (overlay.Tag != null)
            {
                // Pass the full source string so the SerializedHash32 keeps it readable in the
                // inspector ("design:input-a") rather than storing only the hash.
                string tagId = string.IsNullOrEmpty(shortLabel)
                    ? null
                    : "design:input-" + shortLabel.ToLowerInvariant();
                overlay.Tag.SetId(tagId);
            }
        }

        #endregion // Visuals
    }
}
