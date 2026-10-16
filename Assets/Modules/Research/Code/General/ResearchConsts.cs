using BeauUtil;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Research
{
    public static class ResearchConsts
    {

    }

    public static class ResearchScriptTriggers
    {
        public static readonly StringHash32 OnSetupComplete = "OnSetupComplete";
        public static readonly StringHash32 OnSampleLifted = "OnSampleLifted";
        public static readonly StringHash32 OnSlotFilled = "OnSlotFilled";
        public static readonly StringHash32 OnVoltageIncreased = "OnVoltageIncreased";
        public static readonly StringHash32 OnVoltageDecreased = "OnVoltageDecreased";
        public static readonly StringHash32 OnHeatIncreased = "OnHeatIncreased";
        public static readonly StringHash32 OnHeatDecreased = "OnHeatDecreased";
        public static readonly StringHash32 OnObservationPickerOpened = "OnObservationPickerOpened";
        public static readonly StringHash32 OnObservationAdded = "OnObservationAdded";
        public static readonly StringHash32 OnObservationRemoved = "OnObservationRemoved";
        public static readonly StringHash32 OnChamberSwitched = "OnChamberSwitched";
        public static readonly StringHash32 OnPropertyAdded = "OnPropertyAdded";
        public static readonly StringHash32 OnPropertyRemoved = "OnPropertyRemoved";
        // Fires the frame after the Verify (submit) button becomes visible, once its ElementTag is
        // registered. Onboarding scripts that highlight the verify button should listen for this
        // rather than OnObservationAdded — the button isn't active (so its tag isn't in the lookup)
        // until the sample panel renders the viewmodel a couple of systems after the add.
        public static readonly StringHash32 OnVerifyButtonShown = "OnVerifyButtonShown";
        public static readonly StringHash32 OnHypothesisSubmitted = "OnHypothesisSubmitted";
        public static readonly StringHash32 OnMaterialExploded = "OnMaterialExploded";
        public static readonly StringHash32 OnLightEmissionPlayed = "OnLightEmissionPlayed";
        public static readonly StringHash32 OnHighMobilityPlayed = "OnHighMobilityPlayed";
    }
}
