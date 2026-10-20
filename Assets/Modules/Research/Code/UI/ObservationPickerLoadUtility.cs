//using SpaceFab.Materials;
//using System;
//using UnityEngine;

//namespace SpaceFab.Research {
//    /// <summary>
//    /// Builds the observation picker chip set for the currently-active
//    /// chamber. Available observations are constant per chamber, so the
//    /// pool sync + layout + overlay resize happen once on chamber load —
//    /// not per Add-Observation click.
//    ///
//    /// Per-chip disabled state (greyed-out because the label is already
//    /// in a sample slot) is owned by ObservationPickerRefreshSystem and
//    /// updates whenever the hypothesis viewmodel changes.
//    ///
//    /// Dormant: observation selection now happens on the wiki's
//    /// observation pages, and nothing opens the picker overlay anymore.
//    /// Kept compiling so the overlay can be brought back without
//    /// rebuilding it.
//    /// </summary>
//    public static class ObservationPickerLoadUtility {
//        // Padding (px) added above and below the chip column when
//        // resizing ChipPickerOverlay's RectTransform height to fit the
//        // alloced chips.
//        private const float OverlayPadding = 16f;

//        // Gap (px) between adjacent picker chips. Mirrors the hypothesis
//        // panel chip gap so the two columns read as a single visual
//        // language.
//        private const float ChipGap = 8f;

//        // Frees any previously-alloced picker chips, then allocs one new
//        // chip per label in `availableObservations`, parents under
//        // panel.PickerChipContainer, binds a captured-index click
//        // handler per chip, lays them out vertically, and resizes the
//        // overlay to fit. Initial disabled state is false for every
//        // chip; ObservationPickerRefreshSystem updates it on the next
//        // HypothesisChangedThisFrame.
//        public static void LoadFor(ResearchSamplePanel panel, ResearchPools pools, ChamberInterfacerState interfacerState, MaterialPropertyLabel[] availableObservations) {
//            if (panel == null || pools == null || pools.PickerChipPool == null) return;
//            if (panel.PickerChipContainer == null) return;

//            // 1. Clean slate — return any leftover chips from a prior
//            // chamber to the pool and clear the parallel lists.
//            SamplePanelInputUtility.FreeAllPickerChips(panel, pools);

//            int count = availableObservations != null ? availableObservations.Length : 0;
//            for (int i = 0; i < count; i++) {
//                ResearchObservationChip chip = pools.PickerChipPool.Alloc();
//                if (chip == null) {
//                    break;
//                }
//                chip.transform.SetParent(panel.PickerChipContainer, false);

//                // Capture the index *before* adding to the lists so the
//                // handler resolves the same slot the chip occupies.
//                int captured = pools.ActivePickerChips.Count;
//                Action handler = () => panel.HandlePickerChip(captured);
//                if (chip.Click != null) {
//                    chip.Click.onClick.Register(handler);
//                }

//                MaterialPropertyLabel label = availableObservations[i];
//                ObservationType observationType = MaterialObservationChamberLookup.GetChamberType(label);
//                // Picker chips always render the filled sprite — the
//                // picker shows what the player *can* add, so the chips
//                // read as "ready to take" rather than "empty slot."
//                // ResearchObservationChipAssets supplies the filled
//                // sprite per ObservationType.
//                string text = ResearchWikiInputUtility.GetObservationChipText(label, interfacerState);
//                chip.SetState(text, ChipFillState.Filled, false, observationType);
//                chip.SetPickerChipDisabledVisual(false);

//                pools.ActivePickerChips.Add(chip);
//                panel.PickerClickHandlers.Add(handler);
//                panel.PickerLabels.Add(label);
//            }

//            // 2. Lay out the alloced chips vertically and resize the
//            // overlay's RectTransform to fit. Width is left as authored.
//            float contentHeight = ResearchUILayoutUtility.LayoutVerticalCentered(pools.ActivePickerChips, pools.ActivePickerChips.Count, ChipGap);
//            if (panel.ChipPickerOverlay != null) {
//                RectTransform overlayRect = panel.ChipPickerOverlay.transform as RectTransform;
//                if (overlayRect != null) {
//                    Vector2 size = overlayRect.sizeDelta;
//                    size.y = contentHeight + 2f * OverlayPadding;
//                    overlayRect.sizeDelta = size;
//                }
//            }
//        }
//    }
//}
