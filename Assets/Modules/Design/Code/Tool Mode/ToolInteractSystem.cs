using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFab.Design {
    /// <summary>
    /// Manages Tool mode interactions, in which the player is actively shaping the grid.
    /// Delegates to DrawUtility or EraseUtility depending on the currently selected tool type.
    /// Runs on any Update phase at order 10 under ToolModeMask.
    /// </summary>
    public class ToolInteractSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhaseMask.Update, 10, UpdateMasks.ToolModeMask),
                new SysPermissions()
                    .ReadWriteShared<ToolModeState>()
                    .ReadWriteShared<GridStackState>()
                    .ReadWriteShared<VisualGridStackState>()
            );
        }

        // Gates on the active tool, then routes mouse input to down/drag/up handlers.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ToolModeState toolModeState,
                out GridStackState gridStackState,
                out VisualGridStackState visualState
                );

            if (toolModeState.ActiveTool == ToolType.None) {
                return;
            }

            ProcessInputs(toolModeState, gridStackState, visualState);
        }

        #region Coordinate Inputs

        // Dispatches the current frame's left-mouse state to down/drag/up handlers.
        static private void ProcessInputs(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState) {
            // The pointer is over UI / an overlay collider (e.g. an output visual). New
            // interactions must not begin here, but a mouse-up still has to close out a drag
            // that is already in progress — otherwise releasing over an overlay would strand
            // the drag and OnDragReleased would never fire. So when the pointer is over a
            // GameObject we skip down/drag but still let an active drag terminate.
            bool pointerOverUI = EventSystem.current.IsPointerOverGameObject();
            // A drag is active once mouse-down seeds StartingDragCoord; TerminateDrag resets it
            // to the empty sentinel.
            bool dragActive = toolModeState.StartingDragCoord != DesignConsts.EMPTY_DRAG_COORD;

            if (!pointerOverUI) {
                if (Input.GetMouseButtonDown(0)) {
                    HandleLeftMouseDown(toolModeState, gridStackState, visualState);
                }
                if (Input.GetMouseButton(0)) {
                    HandleLeftMouseDrag(toolModeState, gridStackState, visualState);
                }
            }

            // Always honor the release of an in-progress drag, even over UI / an overlay.
            if (Input.GetMouseButtonUp(0) && (!pointerOverUI || dragActive)) {
                HandleLeftMouseUp(toolModeState);
            }
        }

        // On mouse-down: convert the cursor to a grid coord and invoke the click handler for the cell's layer and occupancy.
        static private void HandleLeftMouseDown(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState) {
            // get mouse position in world space
            // var worldPos = Game.Rendering.PrimaryCamera.ScreenToWorldPoint(Input.mousePosition);
            // var gridPos = new Vector2Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.y));

            // this needs to be relative to the grid
            var worldPos = Game.Rendering.PrimaryCamera.ScreenToWorldPoint(Input.mousePosition);
            var gridPos = GridStackUtility.ConvertToGridSpace(worldPos, gridStackState, visualState);

            // if grid cell is out of bounds:
            if (!GridStackUtility.InBounds(gridStackState, gridPos.x, gridPos.y)) {
                // do nothing
                return;
            }

            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            // if grid cell is empty:
            if (GridLayerUtility.IsCellEmpty(layer, gridPos)) {
                if (toolModeState.ActiveLayer == StackLayer.Metal) {
                    ClickEmptyMLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
                else {
                    ClickEmptyTLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
            }
            // if grid cell is full:
            else {
                if (toolModeState.ActiveLayer == StackLayer.Metal) {
                    ClickOccupiedMLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
                else {
                    ClickOccupiedTLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
            }
            Log.Msg("[InteractMgr] Click Coords: (x: " + gridPos.x + " , y: " + gridPos.y + ")");

            // A click in-bounds with an active tool may have changed the grid; broadcast so
            // FoundValidSolution and similar derived flags can re-evaluate. Dispatched here
            // (delegating site) rather than per-handler to keep the count to one per input.

            // begin dragging
            toolModeState.StartingDragCoord = gridPos;
            toolModeState.LastKnownDragCoord = gridPos;
        }

        // On mouse-held: compute a new grid coord, bail on redundant/too-fast/out-of-bounds moves, and invoke the drag handler for the cell.
        static private void HandleLeftMouseDrag(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState) {
            var worldPos = Game.Rendering.PrimaryCamera.ScreenToWorldPoint(Input.mousePosition);
            var gridPos = GridStackUtility.ConvertToGridSpace(worldPos, gridStackState, visualState);

            if (gridPos == toolModeState.LastKnownDragCoord) {
                // no change in drag position
                return;
            }

            if (toolModeState.LastTerminatedDragCoord != -Vector2Int.one) {
                // no change from last terminated drag position (player needs to release mouse before new drag can begin)
                return;
            }

            var dif = gridPos - toolModeState.LastKnownDragCoord;
            if (dif.x != 0 && dif.y != 0) {
                // only orthogonal movement allowed; collapse to one dimension (x)
                gridPos.y = toolModeState.LastKnownDragCoord.y;
            }

            // if dragging too quickly
            if (Math.Abs(dif.x) > 1 || Math.Abs(dif.y) > 1) {
                // terminate drag
                ToolModeUtility.TerminateDrag(toolModeState);
                return;
            }

            // if out of bounds:
            if (!GridStackUtility.InBounds(gridStackState, gridPos.x, gridPos.y)) {
                // terminate drag
                ToolModeUtility.TerminateDrag(toolModeState);
                return;
            }

            // if within bounds:
            Log.Msg("[InteractMgr] Dragging to (x: " + gridPos.x + " , y: " + gridPos.y + ")");

            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            // if grid cell is empty:
            if (GridLayerUtility.IsCellEmpty(layer, gridPos)) {
                if (toolModeState.ActiveLayer == StackLayer.Metal) {
                    DragEmptyMLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
                else {
                    DragEmptyTLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
            }
            // if grid cell is full:
            else {
                if (toolModeState.ActiveLayer == StackLayer.Metal) {
                    DragOccupiedMLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
                else {
                    DragOccupiedTLayerCell(toolModeState, gridStackState, visualState, gridPos);
                }
            }

            // A drag step in-bounds with an active tool may have changed the grid; broadcast so
            // FoundValidSolution and similar derived flags can re-evaluate. Fires once per
            // crossed cell — handlers downstream are idempotent.

            // continue dragging
            if (gridPos != toolModeState.LastTerminatedDragCoord) {
                toolModeState.LastKnownDragCoord = gridPos;
            }
        }

        // On mouse-up: terminate any active drag and reset the drag-coord state.
        static private void HandleLeftMouseUp(ToolModeState toolModeState) {
            ToolModeUtility.TerminateDrag(toolModeState, true);
        }

        #endregion // Coordinate Inputs

        #region Clicks

        // Click on an empty Metal-layer cell: apply the active tool (draw/erase/via/gate) and refresh visuals.
        static private void ClickEmptyMLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var cell = GridLayerUtility.GetCell(layer, gridPos);
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            // check tool
            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawMetal:
                    DrawUtility.DrawMetal(ref cell);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Metal")));
                    break;
                case ToolType.DrawVia:
                    DrawUtility.DrawVia(toolModeState, gridStackState, ref cell, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Via")));
                    break;
                case ToolType.DrawGate:
                    DrawUtility.DrawGate(toolModeState, gridStackState, ref cell, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Gate")));
                    break;
                default:
                    break;
            }

            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, gridPos, cell);
        }

        // Click on an empty Transistor-layer cell: apply the active tool.
        static private void ClickEmptyTLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var cell = GridLayerUtility.GetCell(layer, gridPos);
            var gridCoord = new GridCoord((int) toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            // check tool
            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawNNodes:
                    if (DrawUtility.CanDrawNode(gridStackState, toolModeState.ActiveLayer, cell, gridPos))
                    {
                        cell.CellType = CellType.NTransistor;
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "NNodes")));
                    }
                    break;
                case ToolType.DrawPNodes:
                    if (DrawUtility.CanDrawNode(gridStackState, toolModeState.ActiveLayer, cell, gridPos))
                    {
                        cell.CellType = CellType.PTransistor;
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "PNodes")));
                    }
                    break;
                case ToolType.DrawVia:
                    DrawUtility.DrawVia(toolModeState, gridStackState, ref cell, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Via")));
                    break;
                case ToolType.DrawGate:
                    DrawUtility.DrawGate(toolModeState, gridStackState, ref cell, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Gate")));
                    break;
                default:
                    break;
            }

            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, gridPos, cell);
        }

        // Click on an occupied Metal-layer cell: draw-tools only apply if the existing cell is Metal.
        static private void ClickOccupiedMLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var cell = GridLayerUtility.GetCell(layer, gridPos);
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            // check tool
            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawMetal:
                    // Do nothing. Click only matters if node is empty.
                    break;
                case ToolType.DrawVia:
                    // place a via if metal
                    if (cell.CellType == CellType.Metal) {
                        DrawUtility.DrawVia(toolModeState, gridStackState, ref cell, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Via")));
                    }
                    break;
                case ToolType.DrawGate:
                    // place a gate if metal
                    if (cell.CellType == CellType.Metal) {
                        DrawUtility.DrawGate(toolModeState, gridStackState, ref cell, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Gate")));
                    }
                    break;
                default:
                    break;
            }

            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, gridPos, cell);
        }

        // Click on an occupied Transistor-layer cell: preserves edge connections when swapping N/P type.
        static private void ClickOccupiedTLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var cell = GridLayerUtility.GetCell(layer, gridPos);
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            // check tool
            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawNNodes:
                    // only relevant if the occupied cell is a p-transistor and not pre-assigned
                    if (cell.CellType == CellType.PTransistor && cell.NodeEraseable) {
                        cell.CellType = CellType.NTransistor;
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "NNodes")));
                        // note: preserves edge connections
                    }
                    break;
                case ToolType.DrawPNodes:
                    // only relevant if the occupied cell is a n-transistor and not pre-assigned
                    if (cell.CellType == CellType.NTransistor && cell.NodeEraseable) {
                        cell.CellType = CellType.PTransistor;
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "PNodes")));
                        // note: preserves edge connections
                    }
                    break;
                case ToolType.DrawVia:
                    // place a via if transistor
                    if (cell.CellType == CellType.NTransistor || cell.CellType == CellType.PTransistor) {
                        DrawUtility.DrawVia(toolModeState, gridStackState, ref cell, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Via")));
                    }
                    break;
                case ToolType.DrawGate:
                    // place a gate if transistor
                    if (cell.CellType == CellType.NTransistor || cell.CellType == CellType.PTransistor) {
                        DrawUtility.DrawGate(toolModeState, gridStackState, ref cell, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Gate")));
                    }
                    break;
                default:
                    break;
            }

            GridLayerUtility.SetCellAndUpdateVisuals(visualState, layer, gridPos, cell);
        }

        #endregion // Clicks

        #region Dragging

        // Drag onto an empty Metal-layer cell: only Erase and DrawMetal do anything.
        static private void DragEmptyMLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            // check tool
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawMetal:
                    DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, CellType.Metal, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Metal")));
                    break;
                default:
                    break;
            }
        }

        // Drag onto an empty Transistor-layer cell: erase or draw N/P transistor.
        static private void DragEmptyTLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            // check tool
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawNNodes:
                    DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, CellType.NTransistor, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "NNodes")));
                    break;
                case ToolType.DrawPNodes:
                    DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, CellType.PTransistor, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "PNodes")));
                    break;
                default:
                    break;
            }
        }

        // Drag onto an occupied Metal-layer cell: erase or extend metal.
        static private void DragOccupiedMLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            // check tool
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawMetal:
                    DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, CellType.Metal, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Metal")));
                    break;
                default:
                    break;
            }
        }

        // Drag onto an occupied Transistor-layer cell: refuse to drag over inputs/outputs; otherwise draw-over with the selected transistor type (preserving existing type when it matches the opposing tool).
        static private void DragOccupiedTLayerCell(ToolModeState toolModeState, GridStackState gridStackState, VisualGridStackState visualState, Vector2Int gridPos) {
            var layer = gridStackState.GridStack.GridLayers[(int)toolModeState.ActiveLayer];
            var cell = GridLayerUtility.GetCell(layer, gridPos);
            var gridCoord = new GridCoord((int)toolModeState.ActiveLayer, gridPos.x, gridPos.y);

            // check tool
            switch (toolModeState.ActiveTool) {
                case ToolType.Erase:
                    EraseUtility.EraseCellBothLayers(toolModeState, gridStackState, gridPos);
                    SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "Erase")));
                    break;
                case ToolType.DrawNNodes:
                    if (DrawUtility.CanDrawNode(gridStackState, toolModeState.ActiveLayer, cell, gridPos))
                    {
                        DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, cell.CellType, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "NNodes")));
                    }
                    break;
                case ToolType.DrawPNodes:
                    // do not allow dragging onto inputs/outputs
                    if (DrawUtility.CanDrawNode(gridStackState, toolModeState.ActiveLayer, cell, gridPos)) {
                        DrawUtility.DragDrawNodeOfType(toolModeState, gridStackState, visualState, cell.CellType, gridPos);
                        SpacefabGame.Events.Dispatch(GameEvents.DesignGridModified, EvtArgs.Box((gridCoord, "PNodes")));
                    }
                    break;
                default:
                    break;
            }
        }

        #endregion // Dragging
    }
}
