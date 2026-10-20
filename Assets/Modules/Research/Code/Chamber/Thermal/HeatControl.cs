using FieldDay;
using FieldDay.Components;
using FieldDay.Scripting;
using System;
using TMPro;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// World-space UI for the Thermal chamber's heat dial.
    /// </summary>
    public class HeatControl : BatchedComponent, IRegistrationCallbacks
    {
        public ResearchSpriteButton IncreaseButton;
        public ResearchSpriteButton DecreaseButton;

        public ThermalChamberState OwningChamber;
        public bool CanAdjust = true;

        [NonSerialized] public int HeatIndex;
        [NonSerialized] public float CurrentTemperature;
        public SpriteRenderer HeatLevelSlot;

        [Header("Level Placard")]
        public SpriteRenderer PlacardBG;
        public TMP_Text PlacardText;
        public Color32[] PlacardColors;
        public string[] PlacardLabels;

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
            HeatUtility.Increase(this, Find.GlobalAsset<ResearchHeatConfig>());
        }

        private void HandleDecrease()
        {
            HeatUtility.Decrease(this, Find.GlobalAsset<ResearchHeatConfig>());
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
    public static class HeatUtility
    {
        // Bumps the voltage index up by one. No-op at the upper magnitude
        // bound or when the control is locked.
        public static void Increase(HeatControl control, ResearchHeatConfig config)
        {
            if (control == null || config == null || !control.CanAdjust) return;
            if (control.HeatIndex >= config.HeatLevels.Length) return;
            control.HeatIndex++;
            ApplyChange(control, config);

            using (var table = TempVarTable.Alloc()) {
                table.Set("heat", control.HeatIndex);
                ScriptUtility.Trigger(ResearchScriptTriggers.OnHeatIncreased, table);
            }
        }

        // Bumps the voltage index down by one. No-op at the lower magnitude
        // bound or when the control is locked.
        public static void Decrease(HeatControl control, ResearchHeatConfig config)
        {
            if (control == null || config == null || !control.CanAdjust) return;
            int minIndex = 0;
            if (control.HeatIndex <= minIndex) return;
            control.HeatIndex--;
            ApplyChange(control, config);

            ScriptUtility.Trigger(ResearchScriptTriggers.OnHeatDecreased);
        }

        public static void Reset(HeatControl control, ResearchHeatConfig config)
        {            
            if (control == null || config == null) return;
            control.HeatIndex = config.DefaultIndex;
            ApplyChange(control, config);
        }

        public static void SetAdjustable(HeatControl control, ResearchHeatConfig config, bool canAdjust)
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
        private static void ApplyChange(HeatControl control, ResearchHeatConfig config)
        {
            RefreshVisualState(control, config);
            if (control.OwningChamber != null)
            {
                control.OwningChamber.HeatChangedThisFrame = true;
            }
        }

        // Public so OnRegister can prime the visual state without going
        // through a mutator (which would set HeatChangedThisFrame).
        public static void RefreshVisualState(HeatControl control, ResearchHeatConfig config)
        {
            if (control == null || config == null) return;

            if (control.HeatIndex >= 0 && config.Temperatures != null && control.HeatIndex < config.Temperatures.Length)
            {
                control.CurrentTemperature = config.Temperatures[control.HeatIndex];
            }

            if (control.HeatLevelSlot != null && config.HeatLevels != null)
            {
                SpriteRenderer slot = control.HeatLevelSlot;
                slot.sprite = config.HeatLevels[control.HeatIndex];
            }

            control.PlacardBG.color = control.PlacardColors[control.HeatIndex];
            control.PlacardText.SetText(control.PlacardLabels[control.HeatIndex]);

            RefreshButtonVisibility(control, config);
        }

        // Hides increase/decrease at the magnitude bounds and hides
        // everything when the control is locked.
        private static void RefreshButtonVisibility(HeatControl control, ResearchHeatConfig config)
        {
            if (control == null || config == null) return;

            bool atLow = control.HeatIndex <= 0;
            bool atHigh = control.HeatIndex >= config.HeatLevels.Length - 1;
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
