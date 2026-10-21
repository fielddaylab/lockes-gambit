using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using System;
using UnityEngine;

namespace SpaceFab.Supply {
    public sealed class SupplyRouteDrawingSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ClearCollectionDirtyFlags, new SysUpdate(GameLoopPhase.PreUpdate, -101, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .WriteShared<SupplyRouteCollection>());
            
			ecs.Register(&HandleQueuedRouteChanges, new SysUpdate(GameLoopPhase.LateUpdate, -100, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyRouteCollection>()
                    .ReadWriteShared<SupplyRouteDrawingState>()
                    .ReadShared<SupplyShipIndex>());

            ecs.Register(&HandleQueuedRouteChanges, new SysUpdate(GameLoopPhase.LateUpdate, 15, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyRouteCollection>()
                    .ReadWriteShared<SupplyRouteDrawingState>()
                    .ReadShared<SupplyShipIndex>());

            ecs.Register(&DetermineRequestedActionSystem, new SysUpdate(GameLoopPhase.LateUpdate, -8, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadShared<SupplyRouteCollection>()
                    .ReadShared<SupplyHoverState>()
                    .ReadWriteShared<SupplyRouteDrawingState>());

            ecs.Register(&EvaluateActionPreview, new SysUpdate(GameLoopPhase.LateUpdate, -7, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyRouteCollection>()
                    .ReadShared<SupplyHoverState>()
                    .ReadWriteShared<SupplyRouteDrawingState>()
                    .ReadShared<SupplyShipIndex>());

            ecs.Register(&ExecuteActionIfRequested, new SysUpdate(GameLoopPhase.LateUpdate, 0, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyRouteCollection>()
                    .ReadShared<SupplyHoverState>()
                    .ReadWriteShared<SupplyRouteDrawingState>()
                    .ReadShared<SupplyShipIndex>()
                    .ReadWriteShared<SupplyMinigameState>());

            ecs.Register(&UpdatePreviewRouteLines, new SysUpdate(GameLoopPhase.LateUpdate, 10, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyRouteDrawingState>()
                    .ReadShared<SupplyHoverState>());
        }

        static private void ClearCollectionDirtyFlags(float dt) {
            Find.State(out SupplyRouteCollection routes);
            routes.AreFragmentsDirty = false;
            // Routes dirtied outside the Supply frame (restored from save during preload) take over
            // the mask for this frame instead of being cleared before anything renders them.
            routes.UpdatedRouteMask = routes.PendingRouteRefreshMask;
            routes.PendingRouteRefreshMask = default;
        }

        static private void HandleQueuedRouteChanges(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyHoverState hover, out SupplyRouteDrawingState draw, out SupplyShipIndex ships);

            if (draw.QueuedRouteIndex != draw.RouteIndex) {
                if (draw.QueuedRouteIndex < 0) {
                    draw.QueuedRouteIndex = -1;
                    SupplyRouteUtility.CloseRouteDrawing(draw, routes, ships);
                } else {
                    SupplyRouteUtility.OpenRouteDrawing(draw, routes, ships, draw.QueuedRouteIndex);
                }
            }
        }

        // Determines the requested action based on what the player is currently hovering over
        static private void DetermineRequestedActionSystem(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyHoverState hover, out SupplyRouteDrawingState draw);

            draw.PreviewDirty = false;

            if (draw.RouteIndex < 0) {
                SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.None, 0);
                return;
            }

            ref SupplyRouteData routeData = ref routes.Routes[draw.RouteIndex];

            if (hover.HoverDirty || hover.Route != null || draw.ForceUpdatePreview) {
                if (draw.ForceUpdatePreview) {
                    draw.PreviewDirty = true;
                }

                draw.ForceUpdatePreview = false;

                if (hover.Route != null) {
                    int segment = SupplyRouteUtility.TryGetClosestSegment(hover.Route, hover.MousePosition.Value);
                    Assert.True(segment >= 0);
                    SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.RemoveSegment, segment);
                } else if (hover.Node != null) {
                    if (hover.Node.Type == SupplyRouteNodeType.Home) {
                        if (routeData.NodeCount < 2) {
                            SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.DeleteRoute, 0);
                        } else {
                            SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.CompleteRouteHome, 0);
                        }
                    } else {
                        if (SupplyRouteUtility.IsNodeInRoute(routeData, hover.Node)) {
                            if (routeData.Nodes[routeData.NodeCount - 1] == hover.Node) {
                                SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.RemoveLastNode, 0);
                            } else {
                                SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.None, 0);
                            }
                        } else {
                            SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.AddNonTerminalNode, 0);
                        }
                    }
                } else if (hover.MousePosition.HasValue) {
                   SupplyRouteUtility.SetDrawingHoverAction(draw, routeData.NodeCount < 2 ? SupplyRouteDrawAction.DeleteRouteAuto : SupplyRouteDrawAction.CompleteRouteAuto, 0);
                } else {
                    SupplyRouteUtility.SetDrawingHoverAction(draw, SupplyRouteDrawAction.None, 0);
                }
            }
        }
    
        // Regenerates preview data for the player's action
        static private unsafe void EvaluateActionPreview(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyHoverState hover, out SupplyRouteDrawingState draw, out SupplyShipIndex ships);

            if (!draw.PreviewDirty) {
                return;
            }

            ref SupplyRouteData previewRoute = ref routes.TempRouteBuffer;
            previewRoute.Clear();
            ref SupplyRouteStats previewStats = ref routes.TempRouteStats;
            previewStats = default;

            routes.TempRouteFragmentConsume = -1;
            routes.TempRouteFragmentCreate = default;

            if (draw.RouteIndex < 0) {
                return;
            }

            SupplyShipStats shipStats = ships.ShipStats[draw.RouteIndex];

            SupplyRouteData currentRoute = routes.Routes[draw.RouteIndex];
            SupplyRouteStats currentStats = routes.RouteStats[draw.RouteIndex];

            SupplyRouteNode hoverNode = hover.Node;

            switch (draw.HoverAction) {
                case SupplyRouteDrawAction.None: {
                    previewStats = currentStats;
                    break;
                }

                case SupplyRouteDrawAction.AddNonTerminalNode: {
                    Assert.NotNullOrDestroyed(hoverNode, "Hover node should not be null here");
                    FragmentFindResult fragmentFind;
                    SupplyRouteData.Copy(currentRoute, ref previewRoute);
                    if (currentRoute.NodeCount >= SupplyRouteData.MaxNonTerminalNodes) {
                        previewStats = currentStats;
                        previewStats.Flags |= SupplyRouteResultFlags.PathTooLong;
                    } else if (SupplyRouteUtility.IsNodeInOtherRoutes(hoverNode, draw.RouteIndex, out int overlapIndex)) {
                        previewStats = currentStats;
                        previewStats.Flags |= SupplyRouteResultFlags.NodeInAnotherPath;
                    } else if ((fragmentFind = SupplyRouteUtility.TryFindFragment(hoverNode, out int fragmentIndex)) != FragmentFindResult.None) {
                        if (fragmentFind == FragmentFindResult.Middle) {
                            previewStats = currentStats;
                            previewStats.Flags |= SupplyRouteResultFlags.NodeInInvalidSegment;
                        } else {
                            SupplyRouteFragmentData fragmentData = routes.Fragments[fragmentIndex];
                            if (previewRoute.NodeCount + fragmentData.NodeCount > SupplyRouteData.MaxNonTerminalNodes) {
                                previewStats = currentStats;
                                previewStats.Flags |= SupplyRouteResultFlags.PathTooLong;
                            } else {
                                routes.TempRouteFragmentConsume = fragmentIndex;
                                if (fragmentFind == FragmentFindResult.Last) {
                                    for (int i = fragmentData.NodeCount; i-- > 0;) {
                                        SupplyRouteNode node = SupplyRouteUtility.GetNodeForIndex(fragmentData.Nodes[i]);
                                        previewRoute.Nodes[previewRoute.NodeCount++] = node;
                                        previewRoute.NodeMask.Set(node.Index);
                                    }
                                } else {
                                    for (int i = 0; i < fragmentData.NodeCount; i++) {
                                        SupplyRouteNode node = SupplyRouteUtility.GetNodeForIndex(fragmentData.Nodes[i]);
                                        previewRoute.Nodes[previewRoute.NodeCount++] = node;
                                        previewRoute.NodeMask.Set(node.Index);
                                    }
                                }
                                SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                            }
                        }
                    } else {
                        previewRoute.Nodes[previewRoute.NodeCount++] = hoverNode;
                        previewRoute.NodeMask.Set(hoverNode.Index);
                        SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                    }
                    break;
                }

                case SupplyRouteDrawAction.RemoveSegment: {
                    int cutNodeIndex = draw.HoverActionArg + 1; // second node in segment
                    previewRoute.NodeCount = cutNodeIndex;
                    for(int i = 0; i < cutNodeIndex; i++) {
                        SupplyRouteNode node = currentRoute.Nodes[i];
                        previewRoute.Nodes[i] = node;
                        previewRoute.NodeMask.Set(node.Index);
                    }

                    int isolatedNodeCount = currentRoute.NodeCount - cutNodeIndex;
                    if (isolatedNodeCount > 1) {
                        SupplyRouteFragmentData fragment;
                        fragment.NodeCount = (byte) isolatedNodeCount;
                        for(int i = 0; i < isolatedNodeCount; i++) {
                            fragment.Nodes[i] = (byte)currentRoute.Nodes[cutNodeIndex + i].Index;
                        }
                        routes.TempRouteFragmentCreate = fragment;
                    }
                    SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                    break;
                }

                case SupplyRouteDrawAction.RemoveLastNode: {
                    SupplyRouteData.Copy(currentRoute, ref previewRoute);
                    SupplyRouteNode node = previewRoute.Nodes[--previewRoute.NodeCount];
                    previewRoute.Nodes[previewRoute.NodeCount] = null;
                    previewRoute.NodeMask.Unset(node.Index);
                    SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                    break;
                }

                case SupplyRouteDrawAction.CompleteRouteAuto: {
                    SupplyRouteData.Copy(currentRoute, ref previewRoute);
                    SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                    break;
                }

                case SupplyRouteDrawAction.CompleteRouteHome: {
                    SupplyRouteData.Copy(currentRoute, ref previewRoute);
                    previewRoute.Nodes[previewRoute.NodeCount++] = hoverNode;
                    SupplyRouteUtility.TryEvaluatePath(previewRoute, shipStats, draw.RouteIndex, out previewStats);
                    break;
                }

                case SupplyRouteDrawAction.DeleteRoute: {
                    previewStats = default;
                    break;
                }
            }
        }
    
        // Executes the previewed action
        static private unsafe void ExecuteActionIfRequested(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyHoverState hover, out SupplyRouteDrawingState draw, out SupplyShipIndex ships);

            if (draw.HoverAction == SupplyRouteDrawAction.None || !draw.InputLayer.IsInputEnabled()) {
                return;
            }

            if (!Game.Input.IsMousePressed(FieldDay.HID.MouseButton.Left)) {
                return;
            }

            SupplyRouteData previewData = routes.TempRouteBuffer;
            SupplyRouteStats previewStats = routes.TempRouteStats;

            if ((previewStats.Flags & SupplyRouteResultFlags.ErrorMask) != 0) {
                Log.Warn("Can't execute the action - warnings!");
                // TODO: animation
                return;
            }

            SupplyRouteData.Copy(previewData, ref routes.Routes[draw.RouteIndex]);
            routes.RouteStats[draw.RouteIndex] = previewStats;

            routes.UpdatedRouteMask.Set(draw.RouteIndex);

            // The single point where a player edit lands on a route - add, remove, cut and delete
            // all funnel through here - so it is also where a confirmed result stops being valid.
            SupplyStateUtility.InvalidateFinalizedSolution(Find.State<SupplyMinigameState>());

            if (routes.TempRouteFragmentConsume >= 0) {
                SupplyRouteUtility.RemoveFragmentAtIndex(routes.TempRouteFragmentConsume);
                routes.TempRouteFragmentConsume = -1;
            } else if (routes.TempRouteFragmentCreate.NodeCount > 0) {
                SupplyRouteUtility.AddFragment(routes.TempRouteFragmentCreate);
                routes.TempRouteFragmentCreate = default;
            }

            Log.Msg("[SupplyRouteDrawingSystem] executing action {0} on route {1}", draw.HoverAction, draw.RouteIndex);

            switch(draw.HoverAction) {
                case SupplyRouteDrawAction.CompleteRouteAuto:
                case SupplyRouteDrawAction.CompleteRouteHome: {
                    SupplyRouteUtility.QueueRouteDrawingClose();
                    Log.Msg("[SupplyRouteDrawingSystem] firing OnRouteCompleted");
                    ScriptUtility.Trigger(SupplyScriptTriggers.OnRouteCompleted);
                    break;
                }
                case SupplyRouteDrawAction.DeleteRoute:
                case SupplyRouteDrawAction.DeleteRouteAuto: {
                    // Just queue the close — the actual route kill + OnRouteFullyRemoved happen in
                    // SupplyRouteUtility.CloseRouteDrawing, which all close paths funnel through.
                    SupplyRouteUtility.QueueRouteDrawingClose();
                    break;
                }
                default: {
                    SupplyRouteUtility.UpdateRouteCollider(draw.RouteCollider, previewData);
                    if (draw.HoverAction == SupplyRouteDrawAction.RemoveSegment) {
                        Log.Msg("[SupplyRouteDrawingSystem] firing OnRouteSegmentDeleted");
                        ScriptUtility.Trigger(SupplyScriptTriggers.OnRouteSegmentDeleted);
                    }
                    break;
                }
            }

            draw.ForceUpdatePreview = true;
        }
    
        // Updates the preview lines
        static private unsafe void UpdatePreviewRouteLines(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyHoverState hover, out SupplyRouteDrawingState draw);
            Find.GlobalAsset(out SupplyRouteConfig config);

            if (draw.RouteIndex < 0 || !hover.MousePosition.HasValue) {
                draw.CursorLine.enabled = false;
                draw.PreviewDeleteLine.enabled = false;
                return;
            }

            if (draw.ForceUpdatePreview) {
                return;
            }

            SupplyRouteData routeData = routes.Routes[draw.RouteIndex];
            SupplyRouteData previewData = routes.TempRouteBuffer;
            SupplyRouteStats previewStats = routes.TempRouteStats;

            SupplyRouteNode node = hover.Node;

            Vector3* cursorLinePositions = stackalloc Vector3[SupplyRouteData.MaxNodes];
            int cursorLinePositionCount = 0;

            Vector3* deletePositions = stackalloc Vector3[SupplyRouteData.MaxNodes];
            int deletePositionCount = 0;

            SupplyRouteLineConfig cursorLineConfig = config.EmptyCursorLine;

            switch (draw.HoverAction) {
                case SupplyRouteDrawAction.AddNonTerminalNode:
                case SupplyRouteDrawAction.CompleteRouteHome: {
                    cursorLinePositionCount = 1 + previewData.NodeCount - routeData.NodeCount;
                    int writeHead = 0;
                    for (int i = routeData.NodeCount - 1; i < previewData.NodeCount; i++) {
                        cursorLinePositions[writeHead++] = previewData.Nodes[i].Position;
                    }
                    if ((previewStats.Flags & SupplyRouteResultFlags.ErrorMask) != 0) {
                        cursorLineConfig = config.InvalidCursorLine;
                    } else {
                        cursorLineConfig = config.PendingCursorLine;
                    }
                    break;
                }
                case SupplyRouteDrawAction.CompleteRouteAuto:
                case SupplyRouteDrawAction.DeleteRouteAuto: {
                    cursorLinePositionCount = 2;
                    cursorLinePositions[0] = routeData.Nodes[routeData.NodeCount - 1].Position;
                    cursorLinePositions[1] = hover.MousePosition.Value;
                    break;
                }
                case SupplyRouteDrawAction.RemoveLastNode: {
                    deletePositionCount = 2;
                    deletePositions[0] = routeData.Nodes[routeData.NodeCount - 2].Position;
                    deletePositions[1] = routeData.Nodes[routeData.NodeCount - 1].Position;
                    break;
                }
                case SupplyRouteDrawAction.RemoveSegment: {
                    deletePositionCount = 2;
                    deletePositions[0] = routeData.Nodes[draw.HoverActionArg].Position;
                    deletePositions[1] = routeData.Nodes[draw.HoverActionArg + 1].Position;
                    break;
                }
            }

            if (cursorLinePositionCount > 1) {
                draw.CursorLine.enabled = true;
                draw.CursorLine.positionCount = cursorLinePositionCount;
                draw.CursorLine.SetPositions(Unsafe.NativeArray(cursorLinePositions, cursorLinePositionCount));
                SupplyRouteLineConfig.Apply(draw.CursorLine, cursorLineConfig);
            } else {
                draw.CursorLine.enabled = false;
            }

            if (deletePositionCount > 1) {
                draw.PreviewDeleteLine.enabled = true;
                draw.PreviewDeleteLine.positionCount = deletePositionCount;
                draw.PreviewDeleteLine.SetPositions(Unsafe.NativeArray(deletePositions, deletePositionCount));
            } else {
                draw.PreviewDeleteLine.enabled = false;
            }
        }
    }
}