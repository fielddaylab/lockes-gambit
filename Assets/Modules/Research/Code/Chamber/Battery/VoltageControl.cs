using FieldDay;
using FieldDay.Components;
using FieldDay.Scripting;
using System;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// World-space UI for the Battery chamber's voltage dial. Three sprite
    /// buttons step the voltage index up/down and flip across the center.
    /// Holds the current index + voltage; mutation goes through VoltageUtility,
    /// and a button press flips OwningChamber.VoltageChangedThisFrame so the
    /// Battery system recomputes on the next tick.
    ///
    /// The visible meter is on OwningChamber.Battery (a ChamberBattery on
    /// a small / big meter-rig prefab instantiated by ResearchTransitionSystem).
    /// VoltageUtility.RefreshVisualState writes the per-slot filled / empty
    /// sprites; the slot array length caps the dial's magnitude per polarity.
    /// </summary>
    public class VoltageControl : BatchedComponent, IRegistrationCallbacks
    {
        public ResearchSpriteButton IncreaseButton;
        public ResearchSpriteButton DecreaseButton;

        public BatteryChamberState OwningChamber;
        public bool CanAdjust = true;
        [NonSerialized] public int VoltageIndex;

        public void OnRegister()
        {
            if (IncreaseButton != null && IncreaseButton.Cursor != null)
            {
                IncreaseButton.Cursor.onClick.Register(HandleIncrease);
            }
            if (DecreaseButton != null && DecreaseButton.Cursor != null)
            {
                DecreaseButton.Cursor.onClick.Register(HandleDecrease);
            }
        }

        public void OnDeregister()
        {
            if (IncreaseButton != null && IncreaseButton.Cursor != null)
            {
                IncreaseButton.Cursor.onClick.Deregister(HandleIncrease);
            }
            if (DecreaseButton != null && DecreaseButton.Cursor != null)
            {
                DecreaseButton.Cursor.onClick.Deregister(HandleDecrease);
            }
        }

        private void HandleIncrease()
        {
            VoltageUtility.Increase(this, Find.GlobalAsset<ResearchVoltageConfig>());
        }

        private void HandleDecrease()
        {
            VoltageUtility.Decrease(this, Find.GlobalAsset<ResearchVoltageConfig>());
        }
    }

    /// <summary>
    /// Mutates VoltageControl in response to player input. Each mutator
    /// updates VoltageIndex + CurrentVoltage, refreshes the meter rig and
    /// flip transform, and signals OwningChamber.VoltageChangedThisFrame so
    /// the Battery system recomputes on its next ProcessWork.
    ///
    /// Magnitude is bounded per polarity by the active meter rig
    /// (OwningChamber.Battery.VoltageLevelSlots.Length), not by
    /// config.Voltages.Length. config.Voltages must be authored long enough
    /// to cover CenterIndex ± maxMagnitude for the largest battery prefab.
    /// </summary>
    public static class VoltageUtility
    {
        // Reads the active meter rig's slot count off the owning chamber.
        // Treats a missing rig as magnitude 0 so mutators no-op safely
        // before ResearchTransitionSystem has wired the prefab.
        private static int GetMaxMagnitude(VoltageControl control)
        {
            if (control == null || control.OwningChamber == null) return 0;
            ChamberBattery battery = control.OwningChamber.Battery;
            if (battery == null || battery.VoltageLevelSlots == null) return 0;
            return battery.VoltageLevelSlots.Length;
        }

        // Bumps the voltage index up by one. No-op at the upper magnitude
        // bound or when the control is locked.
        public static void Increase(VoltageControl control, ResearchVoltageConfig config)
        {
            if (control == null || config == null || !control.CanAdjust) return;
            int maxIndex = GetMaxMagnitude(control);
            if (control.VoltageIndex >= maxIndex) return;
            control.VoltageIndex++;
            ApplyChange(control, config);

            using (var table = TempVarTable.Alloc()) {
                table.Set("voltage", control.VoltageIndex);
                ScriptUtility.Trigger(ResearchScriptTriggers.OnVoltageIncreased, table);
            }
        }

        // Bumps the voltage index down by one. No-op at the lower magnitude
        // bound or when the control is locked.
        public static void Decrease(VoltageControl control, ResearchVoltageConfig config)
        {
            if (control == null || config == null || !control.CanAdjust) return;
            if (control.VoltageIndex <= 0) return;
            control.VoltageIndex--;
            ApplyChange(control, config);

            ScriptUtility.Trigger(ResearchScriptTriggers.OnVoltageDecreased);
        }

        // Snaps back to the configured default index. Used when the control
        // is locked while not at default. Authoring constraint: DefaultIndex
        // should equal CenterIndex (magnitude 0) so it sits within every
        // battery prefab's range — the small variant only supports
        // magnitude 2 around CenterIndex.
        public static void Reset(VoltageControl control, ResearchVoltageConfig config)
        {
            if (control == null || config == null) return;
            control.VoltageIndex = config.DefaultIndex;
            ApplyChange(control, config);
        }

        // Toggles whether the player can change voltage. Locking while not at
        // default snaps back; both states refresh button visibility.
        public static void SetAdjustable(VoltageControl control, ResearchVoltageConfig config, bool canAdjust)
        {
            if (control == null) return;
            control.CanAdjust = canAdjust;
            if (!canAdjust)
            {
                Reset(control, config);
            }
            RefreshButtonVisibility(control, config);
        }

        // Recomputes derived state after an index change and signals the
        // owning chamber to recompute current.
        private static void ApplyChange(VoltageControl control, ResearchVoltageConfig config)
        {
            RefreshVisualState(control, config);
            if (control.OwningChamber != null)
            {
                control.OwningChamber.VoltageChangedThisFrame = true;
            }
        }

        // Public so OnRegister can prime the visual state without going
        // through a mutator (which would set VoltageChangedThisFrame).
        public static void RefreshVisualState(VoltageControl control, ResearchVoltageConfig config)
        {
            if (control == null || config == null) return;

            // CurrentVoltage is independent of the meter; guard the
            // Voltages[] read on its own so an in-range index still
            // updates CurrentVoltage even when no meter rig exists yet.
            ChamberBattery battery = control.OwningChamber != null ? control.OwningChamber.Battery : null;
            if (battery == null) return;
            if (control.VoltageIndex >= 0 && config.Voltages != null && control.VoltageIndex < config.Voltages.Length)
            {
                battery.CurrentVoltage = config.Voltages[control.VoltageIndex];
            }

            // Meter fill: filled for slots [0..magnitude-1], empty for the
            // rest. Magnitude is the index's distance from CenterIndex —
            // polarity is indicated by BatteryFlip's rotation below, not
            // by which side of the meter lights up.
            if (battery.VoltageLevelSlots != null
                && config.VoltageSlotFilled != null && config.VoltageSlotEmpty != null)
            {
                for (int i = 0; i < battery.VoltageLevelSlots.Length; i++)
                {
                    SpriteRenderer slot = battery.VoltageLevelSlots[i];
                    if (slot == null) continue;
                    slot.sprite = i < control.VoltageIndex ? config.VoltageSlotFilled : config.VoltageSlotEmpty;
                }
            }

            RefreshButtonVisibility(control, config);
        }

        // Hides increase/decrease at the magnitude bounds and hides
        // everything when the control is locked.
        private static void RefreshButtonVisibility(VoltageControl control, ResearchVoltageConfig config)
        {
            if (control == null || config == null) return;

            ChamberBattery battery = control.OwningChamber != null ? control.OwningChamber.Battery : null;
            if (battery == null) return;

            int maxMag = GetMaxMagnitude(control);
            bool atLow = control.VoltageIndex <= 0;
            bool atHigh = control.VoltageIndex >= maxMag;
            bool show = control.CanAdjust;

            if (control.IncreaseButton != null)
            {
                control.IncreaseButton.gameObject.SetActive(show && !atHigh);
            }
            if (control.DecreaseButton != null)
            {
                control.DecreaseButton.gameObject.SetActive(show && !atLow);
            }
        }
    }
}
