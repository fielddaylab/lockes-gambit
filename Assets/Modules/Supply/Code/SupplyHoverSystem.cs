using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.HID;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using FieldDay.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Supply {
    public sealed class SupplyHoverSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&UpdateHover, new SysUpdate(GameLoopPhase.LateUpdate, -100, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyHoverState>());

            ecs.Register(&UpdateDetailsDisplay, new SysUpdate(GameLoopPhase.LateUpdate, -9, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<SupplyHoverState>());
        }

        static private void UpdateHover(float dt) {
            Find.State(out SupplyHoverState hoverState);

            Vector3? mousePos = null;
            SupplyRouteNode hoverNode = null;
            EdgeCollider2D hoverLine = null;

            hoverState.HoverDirty = false;

            if (CursorUtility.IsCursorWithinVirtualViewport() && !Game.Input.IsPointerOverCanvas()) {
                mousePos = Game.Rendering.PrimaryCamera.ScreenToWorldPoint(Input.mousePosition);
                GameObject go = Game.Input.CurrentPointerOver();
                if (go != null) {
                    switch(go.layer) {
                        case global::LayerMasks.SupplyChainPlanet_Index: {
                            hoverNode = go.GetComponentInParent<SupplyRouteNode>();
                            break;
                        }
                        case global::LayerMasks.SupplyChainRoute_Index: {
                            hoverLine = go.GetComponent<EdgeCollider2D>();
                            break;
                        }
                    }
                }
            }

            if (hoverState.MousePosition.HasValue != mousePos.HasValue) {
                hoverState.HoverDirty = true;
            }

            hoverState.MousePosition = mousePos;

            if (hoverState.Node != hoverNode) {
                if (hoverState.Node != null) {
                    SupplyRouteUtility.RemoveNodeHoverFlag(hoverState.Node, SupplyHoverFlags.Node);
                }

                hoverState.Node = hoverNode;

                if (hoverState.Node != null) {
                    SupplyRouteUtility.AddNodeHoverFlag(hoverState.Node, SupplyHoverFlags.Node);

                    if (hoverState.Node.Type == SupplyRouteNodeType.Producer || hoverState.Node.Type == SupplyRouteNodeType.Converter)
                    {
                        using (TempVarTable table = TempVarTable.Alloc())
                        {
                            table.Set("planet", hoverState.Node.Id);
                            ScriptUtility.Trigger(SupplyScriptTriggers.OnPlanetHovered, table);
                        }
                    }
                }

                hoverState.HoverDirty = true;
            }

            if (hoverState.Route != hoverLine) {
                if (hoverState.Route != null) {
                    
                }

                hoverState.Route = hoverLine;

                if (hoverState.Route != null) {

                }

                hoverState.HoverDirty = true;
            }
        }
    
        static private void UpdateDetailsDisplay(float dt) {
            Find.State(out SupplyHoverState hoverState);

            if (hoverState.HoverDirty) {
                SupplyRouteNode node = hoverState.Node;
                if (node && node.Type != SupplyRouteNodeType.Home) {
                    SupplyRouteUtility.PopulateNodeDetailsDisplay(hoverState.DetailsDisplay, node);
                    GuiCommands.SetActive(hoverState.DetailsDisplay, true);
                } else {
                    GuiCommands.SetActive(hoverState.DetailsDisplay, false);
                }
            }
        }
    }
}