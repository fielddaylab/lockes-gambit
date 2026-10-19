using SpaceFab.Design.Visuals;
using UnityEngine;

namespace SpaceFab.Design
{
    public static class DrawUtility
    {
        public static void DrawMetal(ref GridCell cell)
        {
            if (cell.CellType == CellType.Input || cell.CellType == CellType.Output) return;

            cell.CellType = CellType.Metal;
        }

        public static void DrawVia(ToolModeState toolModeState, GridStackState gridState, ref GridCell cell, Vector2Int gridPos)
        {
            if (cell.CellType == CellType.Input || cell.CellType == CellType.Output) { return; }

            StackLayer linkedLayerType = toolModeState.ActiveLayer == StackLayer.Metal ? StackLayer.Transistor : StackLayer.Metal;
            var linkedLayer = gridState.GridStack.GridLayers[(int)linkedLayerType];
            var linkedCell = GridLayerUtility.GetCell(linkedLayer, gridPos);

            if (linkedCell.CellType == CellType.Input || linkedCell.CellType == CellType.Output) { return; }

            // ensure any data from existing transfer type gets (i.e. via) gets erased
            EraseUtility.EraseTransferData(toolModeState, gridState, cell, gridPos, (int)toolModeState.ActiveLayer);

            cell.TransferType = TransferType.Via;

            int cellEdgeIndex = toolModeState.ActiveLayer == StackLayer.Metal ? (int)EdgeDir.DESCEND : (int)EdgeDir.ASCEND;
            int linkedEdgeIndex = toolModeState.ActiveLayer == StackLayer.Metal ? (int)EdgeDir.ASCEND : (int)EdgeDir.DESCEND;
            cell.Edges[cellEdgeIndex].EdgeState = EdgeState.Connected;
            linkedCell.Edges[linkedEdgeIndex].EdgeState = EdgeState.Connected;
        }

        public static void DrawGate(ToolModeState toolModeState, GridStackState gridState, ref GridCell cell, Vector2Int gridPos)
        {
            if (cell.CellType == CellType.Input || cell.CellType == CellType.Output || !cell.TransferEraseable) { return; }

            StackLayer linkedLayerType = toolModeState.ActiveLayer == StackLayer.Metal ? StackLayer.Transistor : StackLayer.Metal;
            var linkedLayer = gridState.GridStack.GridLayers[(int)linkedLayerType];
            var linkedCell = GridLayerUtility.GetCell(linkedLayer, gridPos);

            if (linkedCell.CellType == CellType.Input || linkedCell.CellType == CellType.Output) { return; }

            // ensure any data from existing transfer type gets (i.e. via) gets erased
            EraseUtility.EraseTransferData(toolModeState, gridState, cell, gridPos, (int)toolModeState.ActiveLayer);

            cell.TransferType = toolModeState.ActiveLayer == StackLayer.Metal ? TransferType.GateAbove : TransferType.GateBelow;
            linkedCell.TransferType = toolModeState.ActiveLayer == StackLayer.Metal ? TransferType.GateBelow : TransferType.GateAbove;
        }

        public static void DragDrawNodeOfType(ToolModeState toolModeState, GridStackState gridState, VisualGridStackState visualState, CellType type, Vector2Int gridPos)
        {
            // create edge between last known pos and curr pos
            var layer = gridState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var fromCell = GridLayerUtility.GetCell(layer, toolModeState.LastKnownDragCoord);
            var toCell = GridLayerUtility.GetCell(layer, gridPos);
            var fromDir = GridStackUtility.DirFromToCell(toolModeState.LastKnownDragCoord, gridPos);
            var reverseDir = GridStackUtility.GetOppositeDir(fromDir);

            // disallow drag from inputs/outputs on transistor layer
            if (type == CellType.NTransistor || type == CellType.PTransistor)
            {
                // inputs and outputs are on the metal layer, so we need to grab that layer
                GridLayer metalLayer = gridState.GridStack.GridLayers[(int)StackLayer.Metal];
                var fromCellMetal = GridLayerUtility.GetCell(metalLayer, toolModeState.LastKnownDragCoord);
                var toCellMetal = GridLayerUtility.GetCell(gridState.GridStack.GridLayers[0], gridPos);
                if (fromCellMetal.CellType == CellType.Input || fromCellMetal.CellType == CellType.Output)
                {
                    ToolModeUtility.TerminateDrag(toolModeState);
                    return;
                }

                if (!CanDrawNode(gridState, toolModeState.ActiveLayer, toCell, gridPos))
                {
                    ToolModeUtility.TerminateDrag(toolModeState);
                    return;
                }
            }

            if (toCell.CellType == CellType.Input || toCell.CellType == CellType.Output) {
                toCell.Edges[(int)reverseDir].EdgeState = EdgeState.Connected;
            }
            fromCell.Edges[(int)fromDir].EdgeState = EdgeState.Connected;
            toCell.Edges[(int)reverseDir].EdgeState = EdgeState.Connected;

            // set properties
            if (type == CellType.Metal)
            {
                DrawMetal(ref toCell);
            }
            else
            {
                toCell.CellType = type;
            }

            // save changes
            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, toolModeState.LastKnownDragCoord, fromCell);
            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, gridPos, toCell);
            }
        public static bool CanDrawNode(GridStackState gridState, StackLayer activeLayer, GridCell cell, Vector2Int gridPos)
        {
            if (cell.CellType == CellType.Input || cell.CellType == CellType.Output) return false;
            if (activeLayer == StackLayer.Transistor && !cell.TransferEraseable) return false;
            if (activeLayer == StackLayer.Metal && !cell.NodeEraseable) return false;

            var linkedLayer = gridState.GridStack.GridLayers[(int)GridStackUtility.GetOppositeLayer(activeLayer)];
            var linkedCell = GridLayerUtility.GetCell(linkedLayer, gridPos);
            if (linkedCell.CellType == CellType.Input || linkedCell.CellType == CellType.Output) return false;

            return true;
        }
    }
}
