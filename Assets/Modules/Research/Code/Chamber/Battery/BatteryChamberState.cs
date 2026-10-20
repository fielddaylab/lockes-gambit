using FieldDay;
using FieldDay.Components;
using FieldDay.Audio;
using FieldDay.SharedState;
using SpaceFab.Materials;
using System;
using UnityEngine;
using BeauUtil;

namespace SpaceFab.Research
{
    /// <summary>
    /// Battery state.
    /// Holds scene-authored references (the slot kind it consumes, its
    /// circuit renderer, its voltage control), the static observation set
    /// the chip-picker UI will surface when this chamber is active, and a
    /// per-frame "voltage changed" flag set by VoltageUtility and consumed
    /// by BatteryChamberSystem.
    /// </summary>
    public class BatteryChamberState : SharedStateComponent, IRegistrationCallbacks
    {
        // Which slot kind on ChamberInterfacerState this Battery reads.
        // Battery is single-slot; defaults to Primary.
        public ChamberSlotKind SlotKind = ChamberSlotKind.Primary;

        public CircuitRenderer Circuit;
        public VoltageControl VoltageControl;

        // Scene-wired empty Transform under which the meter-rig prefab
        // (SmallBatteryMeter / BigBatteryMeter) is instantiated by
        // ResearchTransitionSystem at minigame setup.
        public Transform BatteryContainer;

        public GameObject SampleHolder;

        // Root of battery chamber's GameObject hierarchy; used to toggle visibility on activation/deactivation.
        public GameObject Root;

        public float PrimarySlotY;

        // Runtime ref to the instantiated meter rig's ChamberBattery.
        // Assigned by ResearchTransitionSystem after Instantiate; null
        // until then. VoltageUtility reads VoltageLevelSlots.Length off
        // this to cap voltage magnitude, and writes per-slot sprites
        // when refreshing visual state.
        [NonSerialized] public ChamberBattery Battery;

        // Observation chips the player can add while this chamber is active.
        // Read by the chip-picker UI (Tier 4).
        public MaterialPropertyLabel[] AvailableObservations = new MaterialPropertyLabel[]
        {
            MaterialPropertyLabel.Conductive,
            MaterialPropertyLabel.NonConductive,
            MaterialPropertyLabel.VoltageResistant,
            MaterialPropertyLabel.HighMobility,
        };

        // Battery's fixed temperature. Battery doesn't expose temperature
        // control to the player; the Thermal chamber does.
        [Range(0f, 1f)] public float Temperature = 0f;

        // Set by VoltageUtility on a button press; consumed and cleared by
        // BatteryChamberSystem.
        [NonSerialized] public bool VoltageChangedThisFrame;

        // Sound played when no current
        [AudioEvent] public StringHash32 NoCurrentSFX;

        // Track whether warning sound played
        [NonSerialized] public bool NoCurrentWarningPlayed;

        public void OnRegister()
        {
            // Hide the sample holder until the player drops a material into
            // the primary slot. BatteryChamberSystem.UpdateBattery toggles
            // it on the slot-change frame.
            if (SampleHolder != null)
            {
                SampleHolder.SetActive(false);
            }
            NoCurrentWarningPlayed = false;
            Root.SetActive(true);
        }

        public void OnDeregister()
        {
        }
    }

    /// <summary>
    /// Logic paired with BatteryChamberState.
    /// </summary>
    public static class BatteryChamberUtility
    {
        // Resets the Battery chamber to its default state. For the Battery this means snapping
        // the voltage dial back to the configured initial voltage; slot contents and other
        // chamber state are owned elsewhere and intentionally left untouched.
        public static void ResetState(BatteryChamberState state)
        {
            if (state == null)
            {
                return;
            }
            VoltageUtility.Reset(state.VoltageControl, Find.GlobalAsset<ResearchVoltageConfig>());
            state.Root.SetActive(true);
        }
    }
}
