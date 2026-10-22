using FieldDay;
using FieldDay.HID;
using FieldDay.Physics;
using FieldDay.Systems;
using SpaceFab;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Polls input and drives the Research drag-and-drop state machine. On
    /// left-click, runs Physics2D overlap queries against the gem and slot
    /// layers and routes lift / deposit / cancel through ResearchDragUtility.
    /// On right-click, cancels any active drag. Each frame, repositions the
    /// drag's current Instance to follow the cursor.
    /// </summary>
    public class ResearchDragSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 100, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadWriteShared<ResearchDragState>()
                    .ReadWriteShared<ChamberInterfacerState>()
                    .ReadWriteShared<ResearchMaterialInstancePool>()
                    .ReadShared<ResearchSampleTrayState>()
                    .ReadWrite<ResearchSlot>()
                    .ReadWrite<ResearchMaterialSource>()
                    .ReadWrite<ResearchMaterialDragInstance>()
            );
        }

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out ResearchDragState dragState,
                out ChamberInterfacerState interfacerState,
                out ResearchMaterialInstancePool pool,
                out ResearchSampleTrayState trayState
            );

            // 1. Resolve cursor position. Click handling bails when the cursor
            // is over UI canvas or outside the virtual viewport, but the drag
            // preview still tracks world position so it doesn't freeze.
            Camera cam = Game.Rendering.PrimaryCamera;
            Vector2 worldPos = Vector2.zero;
            bool worldValid = cam != null && dragState.Raycaster.enabled;
            if (worldValid) {
                Vector3 worldPos3 = cam.ScreenToWorldPoint(Input.mousePosition);
                worldPos = new Vector2(worldPos3.x, worldPos3.y);
            }
            bool cursorOnCanvas = Game.Input.IsPointerOverCanvas();
            bool cursorValid = worldValid && CursorUtility.IsCursorWithinVirtualViewport() && !cursorOnCanvas;

            // 2. Right-click always cancels.
            bool cancelQueued = false;
            if (Game.Input.IsMousePressed(MouseButton.Right)) {
                cancelQueued = true;
            } else {
                bool leftClicked = Game.Input.IsMousePressed(MouseButton.Left);
                OverlapResults overlap = default;

                if (dragState.CurrentInstance != null) {
                    // 2a. Drag in progress — overlap query runs regardless of
                    // click so hover tracking still works.
                    if (cursorValid) {
                        GetPositionOverlap(worldPos, out overlap);
                    }

                    if (leftClicked) {
                        if (overlap.Slot != null) {
                            ResearchDragUtility.DepositCurrentDrag(dragState, interfacerState, pool, overlap.Slot);
                        } else if (cursorValid && IsOverTray(trayState, worldPos)) {
                            // Dropping anywhere in the tray region releases the
                            // instance back to the pool.
                            ResearchDragUtility.DepositOnTray(dragState, pool);
                        } else {
                            cancelQueued = true;
                        }
                    } else {
                        // Just track hover. TODO: hover VFX hookup once slots
                        // have a HoverVfx field.
                        dragState.SlotHoveredOver = overlap.Slot;
                    }
                } else if (cursorValid && leftClicked) {
                    // 2b. Idle, valid click — try to lift. Source on the tray
                    // and Instance share the gem layer; slot lift falls
                    // through if neither hits.
                    GetPositionOverlap(worldPos, out overlap);
                    if (overlap.Source != null) {
                        ResearchDragUtility.LiftFromSource(dragState, interfacerState, pool, overlap.Source);
                    } else if (overlap.Slot != null && overlap.Slot.AllowLift && overlap.Slot.CurrentMaterial != null) {
                        ResearchDragUtility.LiftFromSlot(dragState, interfacerState, pool, overlap.Slot);
                    }
                }
            }

            if (cancelQueued) {
                ResearchDragUtility.CancelCurrentDrag(dragState, interfacerState, pool);
            }

            // 3. Reposition the dragged Instance to follow the cursor.
            if (worldValid && dragState.CurrentInstance != null) {
                Transform t = dragState.CurrentInstance.transform;
                Vector3 p = t.position;
                t.position = new Vector3(worldPos.x, worldPos.y, p.z);
            }
        }

        // Single-radius Physics2D overlap query against the gem and slot
        // layers. Source-layer hits resolve to a ResearchMaterialSource for
        // click-to-lift; slot hits resolve to a ResearchSlot for drop or
        // slot-lift. Tray drops are detected separately via IsOverTray.
        private static void GetPositionOverlap(Vector2 worldPos, out OverlapResults results) {
            const float OverlapRadius = 0.01f;
            Collider2D gemCollider = Physics2D.OverlapCircle(worldPos, OverlapRadius, LayerMasks.ResearchGem_Mask);
            Collider2D slotCollider = Physics2D.OverlapCircle(worldPos, OverlapRadius, LayerMasks.ResearchSlot_Mask);
            results.Source = gemCollider != null ? gemCollider.ResolveComponent<ResearchMaterialSource>() : null;
            results.Slot = slotCollider != null ? slotCollider.ResolveComponent<ResearchSlot>() : null;
        }

        // True if the cursor's world position falls inside the tray's region
        // collider. A direct collider test (no Physics2D layer query) since
        // there's only one tray in the scene.
        private static bool IsOverTray(ResearchSampleTrayState trayState, Vector2 worldPos) {
            if (trayState == null || trayState.Region == null) {
                return false;
            }
            return trayState.Region.OverlapPoint(worldPos);
        }

        private struct OverlapResults {
            public ResearchMaterialSource Source;
            public ResearchSlot Slot;
        }
    }
}
