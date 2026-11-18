using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.UI;
using SpaceFab;
using SpaceFab.Design;
using SpaceFab.Materials;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceFab.Research {
    /// <summary>
    /// Shared state for the Research drag-and-drop loop. Holds the cursor
    /// hint, raycaster reference, and the runtime drag payload: the
    /// currently-allocated ResearchMaterialInstance, the slot the lift came
    /// from (if any), and the kind enum for that slot. The Instance carries
    /// the dragged MaterialAsset and its OriginSource; the drag-state's job
    /// is to track lift origin and own the cursor + raycaster machinery.
    /// </summary>
    public class ResearchDragState : SharedStateComponent, IRegistrationCallbacks {
        public Transform DragRoot;
        public CursorHint DragCursor;
        // PhysicsRaycaster covers both itself and Physics2DRaycaster (subclass),
        // letting the scene's camera use whichever fits its physics layout.
        // The drag system reads/writes eventMask, declared on PhysicsRaycaster
        // (not on BaseRaycaster, and not on GraphicRaycaster — which uses
        // blockingMask instead and is intentionally unsupported here).
        public PhysicsRaycaster Raycaster;

        // Global swap gate, ANDed with ResearchSlot.AllowSwap. Lets the user
        // disable swapping mid-game (e.g., during a tutorial step) without
        // changing every slot.
        public bool AllowSwap = true;

        [NonSerialized] public ResearchMaterialDragInstance CurrentInstance;
        [NonSerialized] public ResearchSlot SlotHoveredOver;
        [NonSerialized] public ResearchSlot LiftedFromSlot;
        [NonSerialized] public ChamberSlotKind LiftedFromKind;

        public void OnRegister() {
        }

        public void OnDeregister() {
            CurrentInstance = null;
            SlotHoveredOver = null;
            LiftedFromSlot = null;
        }
    }

    /// <summary>
    /// Logic paired with ResearchDragState. Lift / Deposit / Cancel form the
    /// drag-state machine. ResearchDragSystem resolves the shared states once
    /// per ProcessWork and passes them in. Slot mutation routes through
    /// ResearchSlotUtility.FillInSlot; instance allocation/release routes
    /// through ResearchMaterialInstanceUtility.
    /// </summary>
    public static class ResearchDragUtility {
        // Begins a drag from a Source on the tray. Allocates a new Instance
        // carrying the source's material, reparents it under DragRoot, and
        // marks the lift as coming from the source (not a slot).
        public static bool LiftFromSource(ResearchDragState dragState, ChamberInterfacerState interfacerState, ResearchMaterialInstancePool pool, ResearchMaterialSource source) {
            if (source == null || source.Material == null) {
                return false;
            }
            ResearchMaterialDragInstance instance = ResearchMaterialInstanceUtility.Allocate(pool, source.Material, source);
            if (instance == null) {
                return false;
            }
            BeginDrag(dragState, instance, null, ChamberSlotKind.Primary);

            using (var table = TempVarTable.Alloc())
            {
                table.Set("source", "tray");
                ScriptUtility.Trigger(ResearchScriptTriggers.OnSampleLifted, table);
            }
            return true;
        }

        // Begins a drag by lifting a filled slot's current material. Allocates
        // an Instance carrying the slot's material, clears the slot (so its
        // rig stops rendering), and remembers the slot for cancel-restore.
        public static bool LiftFromSlot(ResearchDragState dragState, ChamberInterfacerState interfacerState, ResearchMaterialInstancePool pool, ResearchSlot slot) {
            if (slot == null || slot.CurrentMaterial == null) {
                return false;
            }
            ChamberSlotKind? kindOpt = ChamberInterfacerUtility.KindOf(interfacerState, slot);
            if (!kindOpt.HasValue) {
                return false;
            }

            MaterialAsset material = slot.CurrentMaterial;
            ResearchMaterialDragInstance instance = ResearchMaterialInstanceUtility.Allocate(pool, material, null);
            if (instance == null) {
                return false;
            }

            ResearchSlotUtility.FillInSlot(interfacerState, slot, kindOpt.Value, null);
            BeginDrag(dragState, instance, slot, kindOpt.Value);
            using (var table = TempVarTable.Alloc()) {
                table.Set("source", "slot");
                ScriptUtility.Trigger(ResearchScriptTriggers.OnSampleLifted, table);
            }
            return true;
        }

        // Drops the currently-dragged Instance into the given slot. If the
        // slot already holds a material and both global and per-slot AllowSwap
        // are set, swaps: the slot's previous material becomes the new drag
        // payload (a fresh Instance), and the drag continues. Otherwise the
        // current instance is released and the drag ends. Returns true if a
        // fill or swap was applied.
        public static bool DepositCurrentDrag(ResearchDragState dragState, ChamberInterfacerState interfacerState, ResearchMaterialInstancePool pool, ResearchSlot slot) {
            if (dragState.CurrentInstance == null) {
                return false;
            }

            // Reject slots that aren't wired as Primary or Secondary.
            ChamberSlotKind? kindOpt = ChamberInterfacerUtility.KindOf(interfacerState, slot);
            if (!kindOpt.HasValue) {
                CancelCurrentDrag(dragState, interfacerState, pool);
                return false;
            }
            ChamberSlotKind kind = kindOpt.Value;

            ResearchMaterialDragInstance dragged = dragState.CurrentInstance;
            MaterialAsset draggedMaterial = dragged.Material;

            // 1. A swap requires the slot to be filled with a different
            // material and both AllowSwap gates set.
            MaterialAsset swap = null;
            if (dragState.AllowSwap && slot.AllowSwap && slot.CurrentMaterial != null && slot.CurrentMaterial != draggedMaterial) {
                swap = slot.CurrentMaterial;
            }

            // 2. Fill the destination. A non-receptive slot causes FillInSlot
            // to return false, in which case we cancel.
            if (!ResearchSlotUtility.FillInSlot(interfacerState, slot, kind, draggedMaterial)) {
                CancelCurrentDrag(dragState, interfacerState, pool);
                return false;
            }

            SpacefabGame.Events.Dispatch(GameEvents.ResearchPlaceSample, EvtArgs.Box((interfacerState.ActiveChamber, kind, draggedMaterial)));

            // Retaining logic in case change for future, but currently want items to 
            // return back to the pool when swapped ou
            // if (swap != null)
            // {
            //     // 3a. Swap: release the current instance, allocate a fresh one
            //     // for the swapped-out material. LiftedFromSlot becomes this
            //     // slot so a later cancel restores the swapped material here.
            //     ResearchMaterialInstanceUtility.Release(pool, dragged);
            //     ResearchMaterialDragInstance newInstance = ResearchMaterialInstanceUtility.Allocate(pool, swap, null);
            //     if (newInstance == null)
            //     {
            //         EndDrag(dragState);
            //         return true;
            //     }
            //     dragState.CurrentInstance = newInstance;
            //     dragState.LiftedFromSlot = slot;
            //     dragState.LiftedFromKind = kind;
            //     if (dragState.DragRoot != null)
            //     {
            //         newInstance.transform.SetParent(dragState.DragRoot, false);
            //     }
            // }
            // else
            // {
            //     // 3b. No swap: release the instance and end the drag.
            //     ResearchMaterialInstanceUtility.Release(pool, dragged);
            //     EndDrag(dragState);
            // }
            SpacefabGame.Events.Dispatch(GameEvents.ResearchDropSample, EvtArgs.Ref(draggedMaterial));
            
            // 3. If was a swap, put back into pool instead of on cursor
            ResearchMaterialInstanceUtility.Release(pool, dragged);
            EndDrag(dragState);
            return true;
        }

        // Drops the dragged Instance back onto the tray. Releases the instance
        // to the pool; no slot side-effects, no source side-effects (the tray
        // sources are permanent fixtures).
        public static bool DepositOnTray(ResearchDragState dragState, ResearchMaterialInstancePool pool) {
            if (dragState.CurrentInstance == null) {
                return false;
            }
            ResearchMaterialInstanceUtility.Release(pool, dragState.CurrentInstance);
            EndDrag(dragState);
            return true;
        }

        // Aborts the drag. If the lift came from a slot, the original material
        // is restored. If the lift came from a Source (or has no origin), the
        // Instance is just released — the Source is unaffected.
        public static bool CancelCurrentDrag(ResearchDragState dragState, ChamberInterfacerState interfacerState, ResearchMaterialInstancePool pool) {
            ResearchMaterialDragInstance dragged = dragState.CurrentInstance;
            if (dragged == null) {
                return false;
            }

            // If the source slot's receptive flag has been flipped off
            // mid-drag, FillInSlot will reject the restore and the material is
            // lost. That requires a chamber to have actively deactivated the
            // source slot, which is a legitimate game-state outcome.
            if (dragState.LiftedFromSlot != null && dragged.Material != null) {
                ResearchSlotUtility.FillInSlot(interfacerState, dragState.LiftedFromSlot, dragState.LiftedFromKind, dragged.Material);
            }

            SpacefabGame.Events.Dispatch(GameEvents.ResearchDropSample, EvtArgs.Ref(dragged.Material));

            ResearchMaterialInstanceUtility.Release(pool, dragged);
            EndDrag(dragState);
            return true;
        }

        // Shared lift entry: stores the instance, parents it under DragRoot,
        // remembers the source slot (if any), locks the cursor, and strips
        // UI_Mask from the raycaster so the dragged Instance doesn't intercept
        // its own hover events.
        private static void BeginDrag(ResearchDragState dragState, ResearchMaterialDragInstance instance, ResearchSlot sourceSlot, ChamberSlotKind sourceKind) {
            dragState.CurrentInstance = instance;
            dragState.LiftedFromSlot = sourceSlot;
            dragState.LiftedFromKind = sourceKind;

            SpacefabGame.Events.Dispatch(GameEvents.ResearchGrabSample, EvtArgs.Ref(instance.Material));

            if (instance != null && dragState.DragRoot != null) {
                instance.transform.SetParent(dragState.DragRoot, false);
            }

            CursorHint.TryLock(dragState.DragCursor);
            if (dragState.Raycaster != null) {
                dragState.Raycaster.eventMask &= ~LayerMasks.UI_Mask;
            }
        }

        // Shared drag-end teardown: clears runtime state, unlocks the cursor,
        // restores the raycaster's UI_Mask. Does NOT release the instance —
        // each caller releases on its own path so swap semantics stay clean.
        private static void EndDrag(ResearchDragState dragState) {
            dragState.CurrentInstance = null;
            dragState.LiftedFromSlot = null;
            dragState.SlotHoveredOver = null;
            CursorHint.Unlock(dragState.DragCursor);
            if (dragState.Raycaster != null) {
                dragState.Raycaster.eventMask |= LayerMasks.UI_Mask;
            }
        }
    }
}
