using FieldDay;
using FieldDay.Components;
using FieldDay.Audio;
using FieldDay.SharedState;
using SpaceFab.Materials;
using System;
using UnityEngine;
using BeauUtil;
using FieldDay.UI;
using TMPro;

namespace SpaceFab.Research
{
    public class DopingChamberState : SharedStateComponent, IRegistrationCallbacks
    {
        public CircuitRenderer Circuit;
        public GameObject SampleHolder;

        // Root of doping chamber's GameObject hierarchy; used to toggle visibility on activation/deactivation.
        public GameObject Root;

        public MaterialAtom SubstrateAtom;
        public MaterialAtom DopantAtom;
        public TextMeshPro EmptyState;
        
        // Observation chips the player can add while this chamber is active.
        // Read by the chip-picker UI (Tier 4).
        public MaterialPropertyLabel[] AvailableObservations = new MaterialPropertyLabel[]
        {
            MaterialPropertyLabel.AtomicRadiusCompliant,
            MaterialPropertyLabel.ValenceOneLessThan,
            MaterialPropertyLabel.ValenceOneMoreThan
        };

        // Voltage and temperature are static for doping chamber
        public readonly float Voltage = 1f;
        public readonly float Temperature = 0f;

        // Index of element with the biggest atomic radius. Set as 0 if a substrate is not polyelemental.
        public int HostElementIndex;

        public float PrimarySlotY;

        [NonSerialized] public bool AtomicViewChangedThisFrame;

        // Sound played when no current
        [AudioEvent] public StringHash32 NoCurrentSFX;

        // Track whether warning sound played
        [NonSerialized] public bool NoCurrentWarningPlayed;

        public void OnRegister()
        {
            // Hide the atomic view and the secondary slot until the player
            // drops a material into the primary slot.
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

    public static class DopingChamberUtility
    {
        public static void ResetState(DopingChamberState state)
        {
            if (state == null)
            {
                return;
            }

            state.HostElementIndex = 0;
            state.AtomicViewChangedThisFrame = true;
            state.Root.SetActive(true);
        }
    }
}
