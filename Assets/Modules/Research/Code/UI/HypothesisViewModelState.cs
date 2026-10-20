using BeauRoutine;
using BeauUtil;
using FieldDay.SharedState;
using SpaceFab.Materials;
using System;

namespace SpaceFab.Research {
    /// <summary>
    /// Per-frame viewmodel polled by the research UI on LateUpdate.
    /// HypothesisViewModelSystem recomputes every field under ResearchMask
    /// from the selected hypothesis, the slotted material, and the sandbox /
    /// player-progress confirmation state.
    ///
    /// Two views:
    /// - **Hypothesis selection** (HypothesisSelected / HypothesisLabel /
    ///   HypothesisContext / HypothesisLeafCount): the property the player
    ///   has picked from a wiki property page, keyed directly by label.
    ///   Context is the substrate id for dynamic (dopant) labels selected
    ///   in the doping chamber; Null otherwise. Drives the sample panel's
    ///   hypothesis chip and the wiki property-chip grey state.
    /// - **Slot view** (SlotLabels / SlotContexts / SlotCount /
    ///   SlotLockedMask): the ordered list of observations occupying the
    ///   sample panel's N slots, in researchState.Observations insertion
    ///   order. Drives the sample panel chips and the wiki observation-chip
    ///   grey state.
    /// </summary>
    public class HypothesisViewModelState : SharedStateComponent, FieldDay.IRegistrationCallbacks {
        // Capacity for the slot view; Always show 3 slots.
        public const int MaxObservationSlots = 3;

        // === Hypothesis selection ===

        [NonSerialized] public bool HypothesisSelected;
        [NonSerialized] public bool HypothesisVerified;

        // Valid only while HypothesisSelected.
        [NonSerialized] public MaterialPropertyLabel HypothesisLabel;
        [NonSerialized] public StringHash32 HypothesisContext;

        // Leaf count of the selected label's first-registered definition,
        // capped at MaxObservationSlots. Drives VerifyButtonVisible.
        [NonSerialized] public int HypothesisLeafCount;

        // === Slot view ===

        // Filled slots in display order. Index [0..SlotCount) is valid.
        [NonSerialized] public MaterialPropertyLabel[] SlotLabels;
        [NonSerialized] public StringHash32[] SlotContexts;

        [NonSerialized] public int SlotCount;

        // Bit i = slot i is locked (auto-confirmed, non-removable).
        [NonSerialized] public uint SlotLockedMask;

        // === Submit ===

        // True when at least one observation is selected and a property chip is selected.
        // Drives the submit button's visibility.
        [NonSerialized] public bool VerifyButtonVisible;

        // === Frame flags ===
        [NonSerialized] public bool HypothesisChangedThisFrame;

        // Rebuild request flag. Other systems set this via
        // HypothesisViewModelUtility.RequestRebuild when their work
        // invalidates the viewmodel. HypothesisViewModelSystem clears it
        // once it has recomputed.
        [NonSerialized] public bool NeedsRebuild;

        [NonSerialized] public Routine VerifyRoutine;

        public void OnRegister() {
            SlotLabels = new MaterialPropertyLabel[MaxObservationSlots];
            SlotContexts = new StringHash32[MaxObservationSlots];
        }

        public void OnDeregister() {
        }
    }
}
