using BeauRoutine;
using FieldDay;
using FieldDay.Components;
using SpaceFab.Design.Visuals;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceFab.Design
{
    public class VisualGridCell : BatchedComponent
    {
        public SpriteRenderer PathRenderer;
        public SpriteRenderer ShadowPathRenderer;
        public SpriteRenderer SubRenderer;
        public SpriteRenderer PathOverlayRenderer;
        public SpriteRenderer PathOverlayBaseRenderer;
        public SpriteRenderer TransferRenderer;
        public SpriteRenderer SecondaryTransferRenderer;
        public SpriteRenderer[] DirRenderers;
        public SpriteMask FlowMask;

        public SpriteRenderer FlowIndicator;

        public Transform[] ShiftForMetalLayer;
    }

    public static class VisualGridCellUtility
    {
        private const int FLOW_SORT_ORDER = 500;
        private const int GATE_SORT_ORDER = 300;
        private const int SECONDARY_SORT_ORDER = 25;
        private const int METAL_SORT_ORDER = 200;
        private const int VIA_SORT_ORDER = 210;
        private const int TRANSISTOR_SORT_ORDER = 0;

        private const float PATH_FLOW_ALPHA = 0.4f; //alpha of path sprite when a flow pattern shows

        public const float METAL_OFFSET = 0.18f;
        public const float SEMI_OFFSET = -0.1f;
        // Per-cell flow state now lives in SimulateRunScratch rather than on GridCell. Callers
        // pass the pre-fetched scratch + cellIndex so this helper doesn't repeat the Find.State
        // lookup for every cell on the grid. During Tool mode, scratch may be null (Simulate
        // mode never entered) — the null-guard here falls back to FlowState.Empty for every cell.
        public static void UpdateFlowVisuals(VisualGridCell visualCell, GridCell cell, SimulateRunScratch scratch, int cellIndex, int layerIndex, GridSpriteDB spriteDB)
        {
            FlowState flow = scratch != null
                ? SimulateRunScratchUtility.GetCellFlow(scratch, cellIndex)
                : FlowState.Empty;
            // visualCell.FlowIndicator.sortingOrder = FLOW_SORT_ORDER;
            visualCell.FlowIndicator.sortingOrder = layerIndex == 0 ? METAL_SORT_ORDER : TRANSISTOR_SORT_ORDER;
            visualCell.FlowIndicator.sortingOrder -= 1;
            Vector2 flowSize = visualCell.FlowIndicator.size;
            float pathAlpha = flow == FlowState.Empty ? 1f : PATH_FLOW_ALPHA;
            visualCell.PathRenderer.SetAlpha(pathAlpha);
            foreach (var r in visualCell.DirRenderers) { r.SetAlpha(pathAlpha); };

            switch (flow) {
                    case (FlowState.Hi):
                        UpdateHiFlow(visualCell, cell, layerIndex, spriteDB);
                        break;
                    case (FlowState.Lo):
                        UpdateLoFlow(visualCell, cell, layerIndex, spriteDB);
                        break;
                    case (FlowState.Unstable):
                        UpdateUnstableFlow(visualCell, cell, layerIndex, spriteDB);
                        break;
                    default:
                        UpdateDefaultFlow(visualCell, cell, layerIndex, spriteDB);
                        break;
                }

            visualCell.FlowIndicator.size = flowSize;
        }

        private static void UpdateHiFlow(VisualGridCell visualCell, GridCell cell, int layerIndex, GridSpriteDB spriteDB)
        {
            if (layerIndex == (int)StackLayer.Metal)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowHiAbove;
            }
            else if (cell.CellType != CellType.Output && cell.CellType != CellType.Input)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowHiBelow;
            }

            SetTransferWithFlow(visualCell, cell, FlowState.Hi, spriteDB);
        }

        private static void UpdateLoFlow(VisualGridCell visualCell, GridCell cell, int layerIndex, GridSpriteDB spriteDB)
        {
            if (layerIndex == (int)StackLayer.Metal)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowLoAbove;
            }
            else if (cell.CellType != CellType.Output && cell.CellType != CellType.Input)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowLoBelow;
            }

            SetTransferWithFlow(visualCell, cell, FlowState.Lo, spriteDB);
        }

        private static void UpdateUnstableFlow(VisualGridCell visualCell, GridCell cell, int layerIndex, GridSpriteDB spriteDB)
        {
            if (layerIndex == (int)StackLayer.Metal)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowUnstableAbove;
            }
            else if (cell.CellType != CellType.Output && cell.CellType != CellType.Input)
            {
                visualCell.FlowIndicator.sprite = spriteDB.FlowUnstableBelow;
            }

            SetTransferWithFlow(visualCell, cell, FlowState.Unstable, spriteDB);
        }

        private static void UpdateDefaultFlow(VisualGridCell visualCell, GridCell cell, int layerIndex, GridSpriteDB spriteDB)
        {
            visualCell.FlowIndicator.sprite = null;

            SetTransferWithFlow(visualCell, cell, FlowState.Empty, spriteDB);
        }

        private static void SetTransferWithFlow(VisualGridCell visualCell, GridCell cell, FlowState flow, GridSpriteDB spriteDB)
        {
            if (cell.TransferType == TransferType.Via)
            {
                // lookup via for flow state
                var sprite = GridSpriteDBUtility.LookupViaSprite(spriteDB, flow);
                visualCell.TransferRenderer.sprite = sprite;
                visualCell.TransferRenderer.transform.localPosition = new Vector3(0, METAL_OFFSET, 0);
                visualCell.SecondaryTransferRenderer.sprite = spriteDB.ViaBase;
            }
            else if (cell.TransferType == TransferType.GateAbove)
            {
                var sprite = GridSpriteDBUtility.LookupGateSprite(spriteDB, flow);
                visualCell.TransferRenderer.sprite = sprite;
                visualCell.TransferRenderer.transform.localPosition = new Vector3(0, METAL_OFFSET, 0);
                visualCell.SecondaryTransferRenderer.sprite = spriteDB.ViaBase;
            }
        }

        public static void RefreshVisual(ref VisualGridCell visualCell, GridCell cellData, SimulateRunScratch scratch, int cellIndex, int layerIndex, int col, int row, GridSpriteDB spriteDB)
        {
            PathLibrary.AssembledPathData pathData = default;
            bool lookedUpEdge = false;

            // Reset
            visualCell.PathRenderer.sprite = null;
            visualCell.ShadowPathRenderer.sprite = null;
            visualCell.PathOverlayRenderer.sprite = null;
            visualCell.PathOverlayBaseRenderer.sprite = null;
            visualCell.SubRenderer.sprite = null;
            foreach (var r in visualCell.DirRenderers) { r.sprite = null; }
            visualCell.FlowMask.sprite = null;
            visualCell.FlowMask.backSortingOrder = 0;
            visualCell.FlowMask.frontSortingOrder = 0;
            visualCell.PathRenderer.color = Color.white;

            // Render according to cell data
            switch (cellData.CellType)
            {
                case CellType.Metal:
                case CellType.Input:
                case CellType.Output:
                    spriteDB.MetalLibrary.Lookup(EdgeUtility.CondenseEdges(cellData.Edges), out pathData);
                    lookedUpEdge = true;
                    break;
                case CellType.NTransistor:
                    RenderNTransistor(visualCell, ref cellData, ref pathData, ref lookedUpEdge, scratch, cellIndex, layerIndex, col, row, spriteDB);
                    break;
                case CellType.PTransistor:
                    RenderPTransistor(visualCell, ref cellData, ref pathData, ref lookedUpEdge, scratch, cellIndex, layerIndex, col, row, spriteDB);
                    break;
                //case CellType.Input:
                //    visualCell.PathRenderer.sprite = spriteDB.IOOuter;
                //    visualCell.SubRenderer.sprite = spriteDB.IOInner;
                //    // visualCell.TextRenderer.SetText(cellData.SubtypeLabel);
                //    break;
                //case CellType.Output:
                //    visualCell.PathRenderer.sprite = spriteDB.IOOuter;
                //    visualCell.SubRenderer.sprite = spriteDB.IOInner;
                //    // visualCell.TextRenderer.SetText(cellData.SubtypeLabel);
                //    break;
                default:
                    break;
            }

            switch (cellData.SubtypeLabel)
            {
                case InputOutputNodeTypeFlags.VPLUS:
                    visualCell.SubRenderer.sprite = spriteDB.InputConstantHigh;
                    visualCell.SubRenderer.transform.localScale = Vector3.one * 0.75f;
                    break;
                case InputOutputNodeTypeFlags.VMINUS:
                    visualCell.SubRenderer.sprite = spriteDB.InputConstantLow;
                    visualCell.SubRenderer.transform.localScale = Vector3.one * 0.75f;
                    break;
                default:
                    break;
            }

            // Reset
            visualCell.TransferRenderer.sprite = null;
            visualCell.SecondaryTransferRenderer.sprite = null;

            switch (cellData.TransferType)
            {
                case TransferType.Via:
                    visualCell.TransferRenderer.sprite = GridSpriteDBUtility.LookupViaSprite(spriteDB, FlowState.Empty);
                    visualCell.SecondaryTransferRenderer.sprite = spriteDB.ViaBase;
                    break;
                case TransferType.GateAbove:
                    visualCell.TransferRenderer.sprite = GridSpriteDBUtility.LookupGateSprite(spriteDB, FlowState.Empty);
                    visualCell.SecondaryTransferRenderer.sprite = spriteDB.ViaBase;
                    break;
                default:
                    break;
            }

            visualCell.PathRenderer.sortingOrder = layerIndex == 0 ? METAL_SORT_ORDER : TRANSISTOR_SORT_ORDER;
            visualCell.ShadowPathRenderer.sortingOrder = TRANSISTOR_SORT_ORDER + 20;
            visualCell.PathOverlayRenderer.sortingOrder = visualCell.PathRenderer.sortingOrder + 3;
            visualCell.PathOverlayBaseRenderer.sortingOrder = visualCell.PathOverlayRenderer.sortingOrder - 1;
            visualCell.SubRenderer.sortingOrder = visualCell.PathRenderer.sortingOrder + 10;
            foreach (var r in visualCell.DirRenderers) { r.sortingOrder = visualCell.PathRenderer.sortingOrder + 5; }
            visualCell.TransferRenderer.sortingOrder = cellData.TransferType == TransferType.Via ? VIA_SORT_ORDER : GATE_SORT_ORDER;
            visualCell.SecondaryTransferRenderer.sortingOrder = SECONDARY_SORT_ORDER;

            visualCell.FlowMask.backSortingOrder = visualCell.PathRenderer.sortingOrder - 50;
            visualCell.FlowMask.frontSortingOrder = visualCell.PathRenderer.sortingOrder + 50;

            if (lookedUpEdge)
            {
                visualCell.PathRenderer.sprite = pathData.Sprite;
                var angles = visualCell.PathRenderer.transform.rotation.eulerAngles;
                angles.z = 90 * pathData.Turns;
                visualCell.PathRenderer.transform.rotation = Quaternion.Euler(angles);

                if (layerIndex == 0) {
                    visualCell.ShadowPathRenderer.sprite = pathData.Sprite;
                    visualCell.ShadowPathRenderer.transform.rotation = Quaternion.Euler(angles);
                }

                visualCell.FlowMask.transform.rotation = Quaternion.Euler(angles);
                visualCell.FlowMask.sprite = pathData.Sprite;
            }

            UpdateFlowVisuals(visualCell, cellData, scratch, cellIndex, layerIndex, spriteDB);
        }

        // Helper: per-cell temp-transform read, null-safe for Tool mode (scratch may not yet
        // be initialized or the player may not have entered Simulate this session).
        private static CellType GetTempTransform(SimulateRunScratch scratch, int cellIndex)
        {
            if (scratch == null) { return CellType.NONE; }
            return SimulateRunScratchUtility.GetCellTempTransform(scratch, cellIndex);
        }

        private static void RenderNTransistor(VisualGridCell visualCell, ref GridCell cellData, ref PathLibrary.AssembledPathData pathData, ref bool lookedUpEdge, SimulateRunScratch scratch, int cellIndex, int layerIndex, int col, int row, GridSpriteDB spriteDB)
        {
            var condensedEdges = EdgeUtility.CondenseEdges(cellData.Edges);
            spriteDB.TransistorLibrary.Lookup(condensedEdges, out pathData);
            lookedUpEdge = true;
            visualCell.PathRenderer.color = spriteDB.NColor;

            CellType selfTempTransform = GetTempTransform(scratch, cellIndex);
            if (selfTempTransform != CellType.NONE)
            {
                if (selfTempTransform == CellType.PTransistor)
                {
                    visualCell.PathOverlayRenderer.sprite = spriteDB.InvertedOverlay;
                    visualCell.PathOverlayBaseRenderer.sprite = spriteDB.InvertedOverlayBase;
                    visualCell.PathOverlayRenderer.color = spriteDB.PColor;
                    visualCell.PathOverlayBaseRenderer.color = spriteDB.NColor;

                    visualCell.PathRenderer.color = spriteDB.PColor;
                }
            }

            GridStackState stackState = Find.State<GridStackState>();
            Dimensions dims = stackState.GridStack.LayerDims;
            int cellsPerLayer = dims.X * dims.Y;

            // set dir renderers
            for (int i = 0; i < 4; i++)
            {
                if (condensedEdges[i] == EdgeState.Connected)
                {
                    // lookup adjacent
                    int adjCol = col;
                    int adjRow = row;

                    // N
                    if (i == 0) { adjRow++; }
                    // E
                    else if (i == 1) { adjCol++; }
                    // S
                    else if (i == 2) { adjRow--; }
                    // W
                    else if (i == 3) { adjCol--; }

                    if (GridStackUtility.InBounds(stackState, adjCol, adjRow))
                    {
                        var adjCell = GridLayerUtility.GetCell(stackState.GridStack.GridLayers[layerIndex], adjCol, adjRow);
                        // if P, set N to P half of renderer
                        if (adjCell.CellType == CellType.PTransistor)
                        {
                            int adjCellIndex = SimulateRunScratchUtility.CellIndex(layerIndex, adjCol, adjRow, dims.X, cellsPerLayer);
                            CellType adjTempTransform = GetTempTransform(scratch, adjCellIndex);
                            if (selfTempTransform != CellType.PTransistor && adjTempTransform != CellType.NTransistor)
                            {
                                visualCell.DirRenderers[i].sprite = spriteDB.NSide;
                            }
                        }
                    }
                }
            }
        }

        private static void RenderPTransistor(VisualGridCell visualCell, ref GridCell cellData, ref PathLibrary.AssembledPathData pathData, ref bool lookedUpEdge, SimulateRunScratch scratch, int cellIndex, int layerIndex, int col, int row, GridSpriteDB spriteDB)
        {
            var condensedEdges = EdgeUtility.CondenseEdges(cellData.Edges);
            spriteDB.TransistorLibrary.Lookup(condensedEdges, out pathData);
            lookedUpEdge = true;
            visualCell.PathRenderer.color = spriteDB.PColor;

            CellType selfTempTransform = GetTempTransform(scratch, cellIndex);
            if (selfTempTransform != CellType.NONE)
            {
                if (selfTempTransform == CellType.NTransistor)
                {
                    visualCell.PathOverlayRenderer.sprite = spriteDB.InvertedOverlay;
                    visualCell.PathOverlayBaseRenderer.sprite = spriteDB.InvertedOverlayBase;
                    visualCell.PathOverlayRenderer.color = spriteDB.NColor;
                    visualCell.PathOverlayBaseRenderer.color = spriteDB.PColor;

                    visualCell.PathRenderer.color = spriteDB.NColor;
                }
            }

            GridStackState stackState = Find.State<GridStackState>();
            Dimensions dims = stackState.GridStack.LayerDims;
            int cellsPerLayer = dims.X * dims.Y;

            // set dir renderers
            for (int i = 0; i < 4; i++)
            {
                if (condensedEdges[i] == EdgeState.Connected)
                {
                    // lookup adjacent
                    int adjCol = col;
                    int adjRow = row;

                    // N
                    if (i == 0) { adjRow++; }
                    // E
                    else if (i == 1) { adjCol++; }
                    // S
                    else if (i == 2) { adjRow--; }
                    // W
                    else if (i == 3) { adjCol--; }

                    if (GridStackUtility.InBounds(stackState, adjCol, adjRow))
                    {
                        var adjCell = GridLayerUtility.GetCell(stackState.GridStack.GridLayers[layerIndex], adjCol, adjRow);
                        // if P, set N to P half of renderer
                        if (adjCell.CellType == CellType.NTransistor)
                        {
                            int adjCellIndex = SimulateRunScratchUtility.CellIndex(layerIndex, adjCol, adjRow, dims.X, cellsPerLayer);
                            CellType adjTempTransform = GetTempTransform(scratch, adjCellIndex);
                            if (selfTempTransform != CellType.NTransistor && adjTempTransform != CellType.PTransistor)
                            {
                                visualCell.DirRenderers[i].sprite = spriteDB.PSide;
                            }
                        }
                    }
                }
            }
        }
    }
}
