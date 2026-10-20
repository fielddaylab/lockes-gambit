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
    public class ThermalChamberState : SharedStateComponent, IRegistrationCallbacks
    {
        // Which slot kind on ChamberInterfacerState this chamber reads.
        // Thermal is single-slot; defaults to Primary.
        public ChamberSlotKind SlotKind = ChamberSlotKind.Primary;

        public CircuitRenderer Circuit;
        public HeatControl HeatControl;

        [Range(0f, 1f)] public float Voltage = 1f;
        public GameObject SampleHolder;

        // Root of thermal chamber's GameObject hierarchy; used to toggle visibility on activation/deactivation.
        public GameObject Root;

        // Observation chips the player can add while this chamber is active.
        // Read by the chip-picker UI (Tier 4).
        public MaterialPropertyLabel[] AvailableObservations = new MaterialPropertyLabel[]
        {
            MaterialPropertyLabel.HeatActivated,
            MaterialPropertyLabel.HeatDeactivated,
            MaterialPropertyLabel.HeatUnaffected,
            MaterialPropertyLabel.HeatResistant
        };

        public float PrimarySlotY;

        [NonSerialized] public bool HeatChangedThisFrame;

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
            Root.SetActive(false);
        }

        public void OnDeregister()
        {
        }
    }

    public static class ThermalChamberUtility
    {
        public static void ResetState(ThermalChamberState state)
        {
            if (state == null)
            {
                return;
            }
            HeatUtility.Reset(state.HeatControl, Find.GlobalAsset<ResearchHeatConfig>());
            state.Root.SetActive(true);
        }
    }
}
