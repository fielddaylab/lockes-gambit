using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication;
using SpaceFab.Save;
using SpaceFab.Supply;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab {
    /// <summary>
    /// Per-cell display state for a Cycles-row cell of the ProgressMeter.
    /// EMPTY hides the overlay; PENDING and FILLED swap in their respective sprites.
    /// </summary>
    public enum CycleCellState : byte {
        EMPTY,
        PENDING,
        FILLED,
    }

    /// <summary>
    /// Per-cell display state for a Funds-row cell of the ProgressMeter.
    /// EMPTY hides the overlay; the other states swap in their respective sprites.
    /// </summary>
    public enum FundsCellState : byte {
        EMPTY,
        PENDING_RECEIVED,
        PENDING_SPENT,
        FILLED,
    }

    /// <summary>
    /// Holds the per-cell display state for the ProgressMeter UI: cycle cell statuses,
    /// funds cell statuses, the current-day marker index, and a dirty flag polled by
    /// ProgressMeterUpdateSystem. Caches the bound view so the system can push updates
    /// without rescanning the scene each frame.
    /// </summary>
    public class ProgressMeterState : SharedStateComponent, IRegistrationCallbacks {
        public CycleCellState[] CycleStates;
        public FundsCellState[] FundsStates;
        public int CurrentDayIdx;
        public bool NeedsRefresh;
        public ProgressMeter ActiveMeter;

        public void OnRegister() {
            CycleStates = Array.Empty<CycleCellState>();
            FundsStates = Array.Empty<FundsCellState>();
            // -1 forces the first system tick to mirror PlayerProgressState.ElapsedCycles
            // even when ElapsedCycles is 0.
            CurrentDayIdx = -1;
            NeedsRefresh = true;
        }

        public void OnDeregister() {
        }
    }

    /// <summary>
    /// Owns all logic for ProgressMeter state and view. ProgressMeter and ProgressMeterCell
    /// hold references only — every mutation, rebuild, and refresh routes through here.
    /// </summary>
    public static class ProgressMeterUtility {
        #region View Lifecycle

        // Caches the view on the state, sizes state arrays to match the view's CellCount,
        // and marks the visuals dirty. Called when a view registers itself or when the
        // system performs a one-time scene fallback lookup.
        public static void BindMeterToState(ProgressMeterState state, ProgressMeter meter) {
            state.ActiveMeter = meter;
            EnsureStateMatchesMeter(state, meter);
            state.NeedsRefresh = true;
        }

        // Resizes state arrays to match the view's CellCount without rebuilding cells.
        public static void EnsureStateMatchesMeter(ProgressMeterState state, ProgressMeter meter) {
            if (state.CycleStates != null && state.CycleStates.Length == meter.CycleCells.Length
                && state.FundsStates != null && state.FundsStates.Length == meter.FundsCells.Length) {
                return;
            }

            Array.Resize(ref state.CycleStates, meter.CycleCells.Length);
            Array.Resize(ref state.FundsStates, meter.FundsCells.Length);
        }

        // OnEnable hook for the view. No-ops outside play mode and when the state has
        // not yet been registered (the system's scene-fallback path will catch that case).
        public static void TryRegisterMeter(ProgressMeter meter) {
            if (!Application.isPlaying) { return; }
            ProgressMeterState meterState = Find.State<ProgressMeterState>();
            if (meterState == null) { return; }
            BindMeterToState(meterState, meter);
        }

        // OnDisable hook for the view. Clears the cached reference if it still points
        // at this meter so a stale view does not get pushed to.
        public static void TryUnregisterMeter(ProgressMeter meter) {
            if (!Application.isPlaying) { return; }
            if (Game.SharedState.Has<ProgressMeterState>())
            {
                ProgressMeterState meterState = Find.State<ProgressMeterState>();
                if (meterState.ActiveMeter == meter)
                {
                    meterState.ActiveMeter = null;
                }
            }
        }

        #endregion // View Lifecycle

        #region Mutators

        // Updates both state and view to a new cell count, rebuilding cell GameObjects.
        public static void SetCellCount(ProgressMeterState state, ProgressMeter meter, int count) {
            count = Mathf.Max(0, count);
            Array.Resize(ref state.CycleStates, count);
            Array.Resize(ref state.FundsStates, count);
            // RebuildCells(meter);
            state.NeedsRefresh = true;
        }

        // Writes a per-cycle cell display state and marks visuals dirty.
        public static void SetCycleCellState(ProgressMeterState state, int idx, CycleCellState cellState) {
            if (idx < 0 || state.CycleStates == null || idx >= state.CycleStates.Length) {
                return;
            }
            if (state.CycleStates[idx] == cellState) { return; }
            state.CycleStates[idx] = cellState;
            state.NeedsRefresh = true;
        }

        // Writes a per-funds cell display state and marks visuals dirty.
        public static void SetFundsCellState(ProgressMeterState state, int idx, FundsCellState cellState) {
            if (idx < 0 || state.FundsStates == null || idx >= state.FundsStates.Length) {
                return;
            }
            state.FundsStates[idx] = cellState;
            state.NeedsRefresh = true;
        }

        // Updates the marker target index. Caller mirrors PlayerProgressState.ElapsedCycles.
        public static void SetCurrentDay(ProgressMeterState state, int dayIdx) {
            if (state.CurrentDayIdx == dayIdx) { return; }
            state.CurrentDayIdx = dayIdx;
            state.NeedsRefresh = true;
        }

        // Sets all states at given cycle and beyond (inclusive) to EMPTY
        public static void ClearCycleStateFrom(ProgressMeterState state, int idx)
        {
            for (int i = idx; i < state.CycleStates.Length; i++)
            {
                SetCycleCellState(state, i, CycleCellState.EMPTY);
            }
        }

        // Sets all states at given fund and beyond (inclusive) to EMPTY
        public static void ClearFundStateFrom(ProgressMeterState state, int idx)
        {
            for (int i = idx; i < state.FundsStates.Length; i++)
            {
                SetFundsCellState(state, i, FundsCellState.EMPTY);
            }
        }

        #endregion // Mutators

        #region Cells: Bind, Rebuild, Refresh

        public static int CalculatePendingCycleCells(ProgressMeter meter, MinigameSaveStates saveStates)
        {
            // rely on most-specific context (minigame) first, then broader context (save state) as fallback
            int numFabricationCycles = 0;

            // get numCycles from Fabrication context
            if (Game.SharedState.Has<FabricationMinigameState>())
            {
                var fabState = Find.State<FabricationMinigameState>();
                numFabricationCycles = fabState.TotalCycles;
            }
            else
            {
                // else rely on Fabrication Save State
                numFabricationCycles = saveStates.Fabrication.FinalizedTotalCycles;
            }
            numFabricationCycles = Mathf.Max(0, numFabricationCycles);

            int numSupplyCycles = 0;
            // get numCycles from SupplyChain context
            if (Game.SharedState.Has<SupplyMinigameState>())
            {
                var supplyState = Find.State<SupplyMinigameState>();
                numSupplyCycles = supplyState.TotalCycles;
            }
            else
            {
                // else rely on SupplyChain save state
                numSupplyCycles = saveStates.Supply.FinalizedTotalCycles;
            }
            numSupplyCycles = Mathf.Max(0, numSupplyCycles);

            return numFabricationCycles + numSupplyCycles;
        }

        public static void CalculatePendingFundsCells(ProgressMeter meter, MinigameSaveStates saveStates, int contractPayout, out int pendingReceivedCount, out int pendingSpentCount)
        {
            // rely on most-specific context (minigame) first, then broader context (save state) as fallback
            int supplyCosts = 0;

            // get funds from SupplyChain context
            if (Game.SharedState.Has<SupplyMinigameState>())
            {
                var supplyState = Find.State<SupplyMinigameState>();
                supplyCosts = supplyState.Cost;
            }
            else
            {
                // else rely on SupplyChain save state
                supplyCosts = saveStates.Supply.FinalizedCost;
            }
            // -1 is the "no confirmed result" sentinel, from both SetDefaults and a route edit
            // invalidating an earlier confirm. Clamp it the same way pending cycles does.
            supplyCosts = Mathf.Max(0, supplyCosts);

            int totalCosts = supplyCosts;

            pendingReceivedCount = contractPayout - supplyCosts;
            pendingSpentCount = supplyCosts;
        }

        /// <summary>
        /// Populates view.CycleCells / FundsCells from the existing children of each row
        /// container — without destroying anything. Called on view OnEnable to recover the
        /// runtime arrays after deserialization (the arrays are [NonSerialized]).
        /// </summary>
        public static void RebindCells(ProgressMeter meter) {
            if (meter == null
                || meter.CycleCellContainer == null
                || meter.FundsCellContainer == null) {
                return;
            }

            meter.CycleCells = new ProgressMeterCell[meter.CycleCellContainer.childCount];
            meter.FundsCells = new ProgressMeterCell[meter.FundsCellContainer.childCount];

            for (int i = 0; i < meter.CycleCellContainer.childCount; i++) {
                meter.CycleCells[i] = meter.CycleCellContainer.GetChild(i).GetComponent<ProgressMeterCell>();
            }

            for (int i = 0; i < meter.FundsCellContainer.childCount; i++) {
                meter.FundsCells[i] = meter.FundsCellContainer.GetChild(i).GetComponent<ProgressMeterCell>();
            }

            // Re-apply layout so cell positions and container/row/root widths track the
            // current cell prefab dimensions even when the prefab was authored against a
            // different cell size.
            // LayoutMeter(meter);
        }

        /// <summary>
        /// Ensures the view's runtime cell arrays are populated. Prefers RebindCells when
        /// the row containers already hold the correct number of cells (cheap, non-destructive);
        /// falls back to RebuildCells otherwise.
        /// </summary>
        // public static void EnsureCellsBound(ProgressMeter meter) {
        //     if (meter == null) { return; }

        //     bool arraysOk = meter.CycleCells != null
        //         && meter.FundsCells != null
        //         && meter.CycleCells.Length == meter.CellCount
        //         && meter.FundsCells.Length == meter.CellCount;
        //     if (arraysOk) { return; }

        //     bool childrenMatch = meter.CycleCellContainer != null
        //         && meter.FundsCellContainer != null
        //         && meter.CycleCellContainer.childCount == meter.CellCount
        //         && meter.FundsCellContainer.childCount == meter.CellCount;
        //     if (childrenMatch) {
        //         RebindCells(meter);
        //         return;
        //     }

        //     RebuildCells(meter);
        // }

        /// <summary>
        /// Tears down and rebuilds the cell GameObjects under both row containers.
        /// Invoked at runtime (via SetCellCount) and at edit time (via the meter's
        /// "Rebuild Progress Meter Cells" inspector context menu). Runs inline; no
        /// deferral, since callers fire it explicitly rather than from OnValidate.
        /// </summary>
//         public static void RebuildCells(ProgressMeter meter) {
//             if (meter == null
//                 || meter.CycleCellPrefab == null
//                 || meter.FundsCellPrefab == null
//                 || meter.CycleCellContainer == null
//                 || meter.FundsCellContainer == null) {
//                 return;
//             }

//             int desired = Mathf.Max(0, meter.CellCount);

//             // 1. Clear any existing cells under each row container.
//             // ClearChildren(meter.CycleCellContainer);
//             // ClearChildren(meter.FundsCellContainer);

//             // 2. Allocate runtime cell-reference arrays sized to the desired count.
//             meter.CycleCells = new ProgressMeterCell[desired];
//             meter.FundsCells = new ProgressMeterCell[desired];

//             // 3. Instantiate fresh cell prefabs into each row, store back-references.
//             //    At edit time, register each created GameObject with Undo so the prefab
//             //    stage / scene gets marked dirty (Unity does not auto-detect scripted
//             //    Instantiate as a prefab modification — without this, exiting prefab
//             //    mode silently discards the new cells).
//             for (int i = 0; i < desired; i++) {
//                 meter.CycleCells[i] = (ProgressMeterCell)UnityEngine.Object.Instantiate(meter.CycleCellPrefab, meter.CycleCellContainer);
//                 meter.FundsCells[i] = (ProgressMeterCell)UnityEngine.Object.Instantiate(meter.FundsCellPrefab, meter.FundsCellContainer);
// #if UNITY_EDITOR
//                 if (!Application.isPlaying) {
//                     UnityEditor.Undo.RegisterCreatedObjectUndo(meter.CycleCells[i].gameObject, "Rebuild Progress Meter Cells");
//                     UnityEditor.Undo.RegisterCreatedObjectUndo(meter.FundsCells[i].gameObject, "Rebuild Progress Meter Cells");
//                 }
// #endif
//             }

        //     // 4. Drive cell positions, cell container widths, row widths, and meter root
        //     //    width from the cell prefab's RectTransform — replaces ContentSizeFitter.
        //     // LayoutMeter(meter);
        // }

        /// <summary>
        /// Pushes ProgressMeterState into the view: applies overlay sprites for each cell,
        /// disables the overlay Image for EMPTY entries, and aligns the current-day marker
        /// to the Cycle cell at CurrentDayIdx. Sprites come from the ProgressMeterSpriteSet
        /// GlobalAsset; refresh early-outs if it has not been registered.
        /// </summary>
        public static void RefreshVisuals(ProgressMeter meter, ProgressMeterState state) {
            if (meter == null || meter.CycleCells == null || meter.FundsCells == null) {
                return;
            }

            ApplyMarkerPosition(meter, state.CurrentDayIdx);
            ProgressMeterSpriteSet sprites = Find.GlobalAsset<ProgressMeterSpriteSet>();
            if (sprites == null) {
                return;
            }

            int cycleLen = state.CycleStates == null ? 0 : state.CycleStates.Length;
            Assert.True(cycleLen == meter.CycleCells.Length);
            for (int i = 0; i < cycleLen; i++) {
                ApplyCycleCell(meter.CycleCells[i], state.CycleStates[i], sprites);
            }

            int fundsLen = state.FundsStates == null ? 0 : state.FundsStates.Length;
            Assert.True(fundsLen == meter.FundsCells.Length);
            for (int i = 0; i < fundsLen; i++) {
                ApplyFundsCell(meter.FundsCells[i], state.FundsStates[i], sprites);
            }
        }

        #endregion // Cells: Bind, Rebuild, Refresh

        #region Display

        public static void Show(ProgressMeter meter)
        {
            meter.gameObject.SetActive(true);
        }

        public static void Hide(ProgressMeter meter)
        {
            meter.gameObject.SetActive(false);
        }

        #endregion // Display

        #region Internal

        /// <summary>
        /// Drives the meter's RectTransform sizes from the cell prefab dimensions and the
        /// cell count. Walks: cells → cell container → row (cell container's parent) →
        /// meter root. Heights of rows and root are author-controlled; only widths and
        /// the cell container's height are computed.
        /// </summary>
        // private static void LayoutMeter(ProgressMeter meter) {
        //     if (meter == null || meter.CycleCellPrefab == null || meter.FundsCellPrefab == null) { return; }

        //     // 1. Position and size cells; size each cell container exactly to its cells.
        //     RectTransform cellPrefabRect = meter.CycleCellPrefab.transform as RectTransform;
        //     if (cellPrefabRect == null) { return; }
        //     Vector2 cellSize = cellPrefabRect.rect.size;
        //     LayoutCellRow(meter.CycleCells, meter.CycleCellContainer, cellSize, meter.CycleCellLayout);

        //     cellPrefabRect = meter.FundsCellPrefab.transform as RectTransform;
        //     if (cellPrefabRect == null) { return; }
        //     cellSize = cellPrefabRect.rect.size;
        //     LayoutCellRow(meter.FundsCells, meter.FundsCellContainer, cellSize, meter.FundsCellLayout);

        //     // 2. Size each row (cell container's parent) to fit title + cell container.
        //     float cycleRowWidth = SizeRowFromCellContainer(meter.CycleCellContainer, meter.CycleRowTitle, meter.CycleCells.Length);
        //     float fundsRowWidth = SizeRowFromCellContainer(meter.FundsCellContainer, meter.FundsRowTitle, meter.FundsCells.Length);

        //     // 3. Size the meter root to fit the wider of the two rows.
        //     SizeRootWidthToMaxRow(meter, Mathf.Max(cycleRowWidth, fundsRowWidth));
        // }

        // Anchors each cell to the container's top-left and positions it at i * cellWidth.
        // Sizes the container's width to count * cellWidth and its height to cellHeight.
        // private static void LayoutCellRow(ProgressMeterCell[] cells, RectTransform container, Vector2 cellSize, HorizontalLayoutGroup layoutGroup) {
        //     if (cells == null || container == null) { return; }

        //     Vector2 topLeft = new Vector2(0f, 1f);
        //     for (int i = 0; i < cells.Length; i++) {
        //         if (cells[i] == null) { continue; }
        //         RectTransform cellRect = cells[i].transform as RectTransform;
        //         if (cellRect == null) { continue; }
        //         cellRect.anchorMin = topLeft;
        //         cellRect.anchorMax = topLeft;
        //         cellRect.pivot = topLeft;
        //         cellRect.sizeDelta = cellSize;
        //         cellRect.anchoredPosition = new Vector2(i * cellSize.x, 0f);
        //     }

        //     layoutGroup.ForceRebuild();

        //     float margin = 10;
        //     Vector2 size = container.sizeDelta;
        //     size.x = cells.Length * cellSize.x + margin;
        //     size.y = cellSize.y;
        //     container.sizeDelta = size;
        // }

        // Sizes the cell container's parent (the row) to width = title width + cell
        // container width. Height is left as authored. Returns the new row width so the
        // caller can size the meter root.
        // private static float SizeRowFromCellContainer(RectTransform cellContainer, TMP_Text title, int numCells) {
        //     if (cellContainer == null) { return 0f; }
        //     RectTransform rowRect = cellContainer.parent as RectTransform;
        //     if (rowRect == null) { return 0f; }

        //     float titleWidth = 0f;
        //     if (title != null) {
        //         RectTransform titleRect = title.transform as RectTransform;
        //         if (titleRect != null) {
        //             titleWidth = titleRect.rect.width;
        //         }
        //     }

        //     float rowWidth = titleWidth + cellContainer.rect.width + (numCells - 1) * GetHorizontalLayoutGroupSpacing(cellContainer);
        //     Vector2 size = rowRect.sizeDelta;
        //     size.x = rowWidth;
        //     rowRect.sizeDelta = size;
        //     return rowWidth;
        // }

        // Returns the HorizontalLayoutGroup.spacing on the given container, or 0 if no
        // such component is present. Lets row sizing track whatever inter-cell gap the
        // user authored on the prefab.
        // private static float GetHorizontalLayoutGroupSpacing(RectTransform container) {
        //     if (container == null) { return 0f; }
        //     HorizontalLayoutGroup hlg = container.GetComponent<HorizontalLayoutGroup>();
        //     return hlg != null ? hlg.spacing : 0f;
        // }

        // Sizes the meter root's width to fit the widest row. Height is left as authored.
        // private static void SizeRootWidthToMaxRow(ProgressMeter meter, float maxRowWidth) {
        //     RectTransform rootRect = meter.transform as RectTransform;
        //     if (rootRect == null) { return; }
        //     Vector2 size = rootRect.sizeDelta;
        //     size.x = maxRowWidth;
        //     rootRect.sizeDelta = size;
        // }

        // Destroys all children of the given container. At runtime uses Object.Destroy.
        // At edit time uses Undo.DestroyObjectImmediate so the destruction is registered
        // on the undo stack and dirties the prefab stage / scene — DestroyImmediate alone
        // does not, so prefab-mode changes get silently discarded on exit.
//         private static void ClearChildren(RectTransform container) {
//             for (int i = container.childCount - 1; i >= 0; i--) {
//                 GameObject child = container.GetChild(i).gameObject;
//                 if (child.name.Equals("Inset BG")) { continue; }
//                 if (Application.isPlaying) {
//                     UnityEngine.Object.Destroy(child);
//                 } else {
// #if UNITY_EDITOR
//                     UnityEditor.Undo.DestroyObjectImmediate(child);
// #else
//                     UnityEngine.Object.DestroyImmediate(child);
// #endif
//                 }
//             }
//         }

        // Applies a CycleCellState to the cell's overlay image. EMPTY hides the overlay.
        private static void ApplyCycleCell(ProgressMeterCell cell, CycleCellState cellState, ProgressMeterSpriteSet sprites) {
            if (cell == null || cell.OverlayImage == null) { return; }

            cell.BaseImage.enabled = true;
            cell.BaseImage.sprite = sprites.CycleBase;
            cell.OverlayImage.enabled = true;
            switch (cellState) {
                case CycleCellState.PENDING:
                    cell.OverlayImage.sprite = sprites.CycleFilled;
                    cell.OverlayImage.color = sprites.CyclePendingColor;
                    break;
                case CycleCellState.FILLED:
                    cell.OverlayImage.sprite = sprites.CycleFilled;
                    cell.OverlayImage.color = sprites.CycleConfirmedColor;
                    break;
                default:
                    cell.OverlayImage.enabled = false;
                    break;
            }
        }

        // Applies a FundsCellState to the cell's overlay image. EMPTY hides the overlay.
        private static void ApplyFundsCell(ProgressMeterCell cell, FundsCellState cellState, ProgressMeterSpriteSet sprites) {
            if (cell == null || cell.OverlayImage == null) { return; }

            if (sprites.FundsBase == null) {
                cell.BaseImage.enabled = false; // no base image for funds cell
            }
            cell.OverlayImage.enabled = true;
            switch (cellState) {
                case FundsCellState.PENDING_RECEIVED:
                    cell.OverlayImage.sprite = sprites.FundsPendingReceived;
                    break;
                case FundsCellState.PENDING_SPENT:
                    cell.OverlayImage.sprite = sprites.FundsPendingSpent;
                    break;
                case FundsCellState.FILLED:
                    cell.OverlayImage.sprite = sprites.FundsFilled;
                    break;
                default:
                    cell.OverlayImage.enabled = false;
                    break;
            }
        }

        // Aligns the current-day marker's anchored x with the target cell's center.
        // Converts cell world position to the marker parent's local space so the result
        // is independent of layout-group offsets and canvas scale.
        private static void ApplyMarkerPosition(ProgressMeter meter, int dayIdx) {
            if (meter.CurrentDayMarker == null) { return; }

            if (meter.CycleCells == null || meter.CycleCells.Length == 0) {
                meter.CurrentDayMarker.gameObject.SetActive(false);
                return;
            }

            // Clamp so a runaway ElapsedCycles past the end of the meter does not throw.
            int clamped = Mathf.Clamp(dayIdx, 0, meter.CycleCells.Length - 1);
            ProgressMeterCell targetCell = meter.CycleCells[clamped];
            if (targetCell == null) { return; }

            RectTransform markerParent = meter.CurrentDayMarker.parent as RectTransform;
            if (markerParent == null) { return; }

            Vector3 worldCenter = targetCell.transform.position;
            Vector3 localCenter = markerParent.InverseTransformPoint(worldCenter);

            Vector2 markerPos = meter.CurrentDayMarker.anchoredPosition;
            markerPos.x = localCenter.x + targetCell.Rect.sizeDelta.x / 2f;
            meter.CurrentDayMarker.anchoredPosition = markerPos;
            meter.CurrentDayMarker.gameObject.SetActive(true);
        }

        #endregion // Internal
    }
}
