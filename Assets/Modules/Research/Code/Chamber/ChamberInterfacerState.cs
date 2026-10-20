using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using SpaceFab.Materials;
using System;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Identifies one of the two slot positions a chamber can use. Single-slot
    /// chambers use Primary only; dual-slot chambers (e.g., a future Combiner)
    /// use both. The Research scene wires which physical ResearchSlot fills
    /// each role; chamber systems toggle each role's receptive flag on activation.
    /// </summary>
    public enum ChamberSlotKind {
        Primary,
        Secondary,
    }

    /// <summary>
    /// Identifies which chamber the player is currently interacting with.
    /// Single shared discriminator on ChamberInterfacerState; chamber systems
    /// short-circuit when the active kind is not their own. Tier 2's station-
    /// transition flow is the canonical writer; chamber systems read it.
    /// </summary>
    public enum ActiveChamberKind : byte {
        None,
        Voltage,
        Thermal,
        Doping
    }

    /// <summary>
    /// Shared state that decouples ResearchSlot writes from any specific
    /// chamber. Holds the two slot references for the current scene and a
    /// per-slot receptive flag chamber systems toggle. After a successful
    /// FillInSlot, the frame-flag fields describe the write so chamber
    /// systems can poll this frame; ChamberInterfacerRefreshSystem clears
    /// the frame-flag fields at end of frame. Receptive flags are persistent
    /// and owned by chamber systems, not the refresh system.
    /// </summary>
    public class ChamberInterfacerState : SharedStateComponent, IRegistrationCallbacks {
        // Slot wiring — set in OnRegister from the serialized refs above.
        public ResearchSlot PrimarySlot;
        public ResearchSlot SecondarySlot;

        // Per-slot receptive gate; chamber systems set true on activation and
        // false on deactivation. Default false: with no chamber present,
        // FillInSlot is a no-op.
        [NonSerialized] public bool PrimaryReceptive;
        [NonSerialized] public bool SecondaryReceptive;

        // Frame-flag triple; cleared by ChamberInterfacerRefreshSystem each
        // frame. LastUpdatedKind is only meaningful while the flag is set.
        [NonSerialized] public bool SlotMaterialUpdatedThisFrame;
        [NonSerialized] public ChamberSlotKind LastUpdatedKind;
        [NonSerialized] public MaterialAsset LastUpdatedMaterial;

        // Currently-active chamber discriminator. Default None; activation flow
        // sets it when the player navigates into a chamber. Chamber systems
        // short-circuit when this is not their own kind.
        [NonSerialized] public bool ActiveChamberChangedThisFrame;
        [NonSerialized] public ActiveChamberKind ActiveChamber;
        [NonSerialized] public ActiveChamberKind LastUnlockedChamber;

        public void OnRegister()
        {
            
        }

        public void OnDeregister() {
            LastUpdatedMaterial = null;
        }
    }

    /// <summary>
    /// Logic paired with ChamberInterfacerState. Handles per-slot receptive
    /// toggles, the frame-flag write performed by ResearchSlotUtility, and
    /// convenience reads for chamber systems.
    /// </summary>
    public static class ChamberInterfacerUtility {
        // Called by FillInSlot after a successful write. Sets the frame-flag
        // and stores which slot kind moved + the new material (may be null).
        public static void MarkSlotUpdated(ChamberInterfacerState interfacerState, ChamberSlotKind kind, MaterialAsset material) {
            interfacerState.SlotMaterialUpdatedThisFrame = true;
            interfacerState.LastUpdatedKind = kind;
            interfacerState.LastUpdatedMaterial = material;
        }

        // True if the given slot kind is currently accepting writes.
        public static bool IsReceptive(ChamberInterfacerState interfacerState, ChamberSlotKind kind) {
            return kind == ChamberSlotKind.Primary ? interfacerState.PrimaryReceptive : interfacerState.SecondaryReceptive;
        }

        // Toggle a slot kind on/off. Chamber systems own this — call true on
        // activation, false on deactivation. Does not clear the slot's held
        // material; that's a chamber-side decision.
        public static void SetReceptive(ChamberInterfacerState interfacerState, ChamberSlotKind kind, bool value) {
            if (kind == ChamberSlotKind.Primary) {
                interfacerState.PrimaryReceptive = value;
            } else {
                interfacerState.SecondaryReceptive = value;
            }
        }

        // Returns the ResearchSlot bound to the given kind, or null if the
        // scene didn't wire one.
        public static ResearchSlot GetSlot(ChamberInterfacerState interfacerState, ChamberSlotKind kind) {
            return kind == ChamberSlotKind.Primary ? interfacerState.PrimarySlot : interfacerState.SecondarySlot;
        }

        // Returns the material currently held by the given slot kind, or null
        // if the slot is empty or not wired.
        public static MaterialAsset GetCurrent(ChamberInterfacerState interfacerState, ChamberSlotKind kind) {
            ResearchSlot slot = GetSlot(interfacerState, kind);
            return slot != null ? slot.CurrentMaterial : null;
        }

        // Sets the currently-active chamber. Activation flow is the only
        // caller; chamber systems read via GetActiveChamber.
        public static void SetActiveChamber(ChamberInterfacerState interfacerState, ActiveChamberKind kind) {
            if (interfacerState.ActiveChamber == kind) { return; }

            interfacerState.ActiveChamber = kind;
            interfacerState.ActiveChamberChangedThisFrame = true;
        }

        // Returns the currently-active chamber kind.
        public static ActiveChamberKind GetActiveChamber(ChamberInterfacerState interfacerState) {
            return interfacerState.ActiveChamber;
        }

        // Reverse lookup: given a physical ResearchSlot, return which kind it
        // is bound to in the interfacer state. Returns null if the slot is
        // not wired as either Primary or Secondary. Drag system uses this to
        // turn a click on a slot into the kind argument FillInSlot needs.
        public static ChamberSlotKind? KindOf(ChamberInterfacerState interfacerState, ResearchSlot slot) {
            if (slot == null) {
                return null;
            }
            if (interfacerState.PrimarySlot == slot) {
                return ChamberSlotKind.Primary;
            }
            if (interfacerState.SecondarySlot == slot) {
                return ChamberSlotKind.Secondary;
            }
            return null;
        }
    }
}
