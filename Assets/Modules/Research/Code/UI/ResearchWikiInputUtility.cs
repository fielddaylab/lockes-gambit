using BeauUtil;
using FieldDay;
using SpaceFab.Materials;
using SpaceFab.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// Seam between wiki chip clicks and the Research input flags. The
    /// wiki lives in every minigame scene, so every entry point guards on
    /// the Research states existing and on ResearchMask running before it
    /// raises a flag no system would consume.
    ///
    /// These are click-time entry points invoked from pooled chip
    /// handlers, so they resolve their own states via Find.State — the
    /// same pattern the panel MonoBehaviours use. The wiki *render* path
    /// takes its Research state as a passed-in WikiResearchContext
    /// instead.
    /// </summary>
    public static class ResearchWikiInputUtility {
        // Opens the wiki to the observation page matching the active
        // chamber. Special observations can occur in any chamber and so
        // are never the target here. No-op outside a scene carrying the
        // wiki, or when no page covers the chamber's type.
        public static void OpenObservationPageForActiveChamber(ChamberInterfacerState interfacerState) {
            if (interfacerState == null) {
                return;
            }

            ObservationType observationType;
            switch (interfacerState.ActiveChamber) {
                case ActiveChamberKind.Voltage:
                    observationType = ObservationType.Electrical;
                    break;
                case ActiveChamberKind.Thermal:
                    observationType = ObservationType.Thermal;
                    break;
                case ActiveChamberKind.Doping:
                    observationType = ObservationType.Dopant;
                    break;
                default:
                    return;
            }

            Find.State(out WikiViewState state, out WikiContent content, out PlayerProgressState progress);
            WikiContentUtility.FlushContentChanges(content, progress);

            var address = WikiContentUtility.LookupObservationPageAddress(content, observationType);
            WikiUtility.OpenTo(state, address);
        }

        // Opens the wiki to the properties page.m
        public static void OpenPropertyPage(PlayerProgressState progressState) {
            Find.State(out WikiViewState state, out WikiContent content);
            WikiContentUtility.FlushContentChanges(content, progressState);

            int tabId = WikiContentUtility.LookupTabId(content, "Properties");
            WikiUtility.OpenTo(state, tabId);
        }

        // The context observations are stored under right now. The doping
        // chamber records every observation against the substrate in the
        // primary slot; every other chamber records against nothing.
        // Mirrors ObservationCollectSystem's add path so grey-matching and
        // removal resolve the same entries the add produced.
        public static StringHash32 GetActiveObservationContext(ChamberInterfacerState interfacerState) {
            if (interfacerState == null || interfacerState.ActiveChamber != ActiveChamberKind.Doping) {
                return StringHash32.Null;
            }
            MaterialAsset substrate = interfacerState.PrimarySlot != null ? interfacerState.PrimarySlot.CurrentMaterial : null;
            return substrate != null ? substrate.AssetId : StringHash32.Null;
        }

        // Index of the slot holding (label, context), or -1 when the pair
        // isn't in the sample panel. Context-exact: a doping observation
        // and its non-doping namesake are different entries.
        public static int FindSlotIndex(HypothesisViewModelState viewModel, MaterialPropertyLabel label, StringHash32 context) {
            if (viewModel == null || viewModel.SlotLabels == null) {
                return -1;
            }
            int count = viewModel.SlotCount;
            for (int i = 0; i < count; i++) {
                if (viewModel.SlotLabels[i] == label && viewModel.SlotContexts[i] == context) {
                    return i;
                }
            }
            return -1;
        }

        // Wiki observation chip click. Already in the panel (greyed) →
        // remove; slots full → ignore; otherwise → add. Clicks with no
        // sample slotted fall through to ObservationCollectSystem, which
        // drops them.
        public static void HandleObservationChipClick(MaterialPropertyLabel label) {
            if (!IsResearchInputLive()) {
                return;
            }

            Find.State(
                out ResearchUIInputState inputState,
                out HypothesisViewModelState viewModel,
                out ChamberInterfacerState interfacerState
            );

            StringHash32 context = GetActiveObservationContext(interfacerState);
            int slotIndex = FindSlotIndex(viewModel, label, context);
            if (slotIndex >= 0) {
                // Locked slots are auto-populated from a confirmed
                // ancestor property and can't be given back.
                if ((viewModel.SlotLockedMask & (1u << slotIndex)) != 0) {
                    return;
                }
                ResearchUIInputUtility.RequestRemoveObservation(inputState, slotIndex);
                return;
            }

            if (viewModel.SlotCount >= HypothesisViewModelState.MaxObservationSlots) {
                return;
            }
            ResearchUIInputUtility.RequestPickerSelection(inputState, label);
        }

        // Wiki property chip click. Clicking the active hypothesis
        // deselects it; anything else selects. HypothesisViewModelSystem
        // does the validation (dynamic labels need a doping substrate;
        // already-fulfilled properties clear themselves).
        public static void HandlePropertyChipClick(MaterialPropertyLabel propertyLabel) {
            if (!IsResearchInputLive()) {
                return;
            }

            Find.State(
                out ResearchUIInputState inputState,
                out HypothesisViewModelState viewModel
            );

            if (viewModel.HypothesisSelected && viewModel.HypothesisLabel == propertyLabel) {
                ResearchUIInputUtility.RequestRemoveHypothesis(inputState);
            } else {
                ResearchUIInputUtility.RequestHypothesisSelection(inputState, propertyLabel);
            }
        }

        // Chip text for an observation label. Dopant observations are
        // comparative, so in the doping chamber they name the substrate
        // they are measured against.
        public static string GetObservationChipText(MaterialPropertyLabel label, ChamberInterfacerState interfacerState) {
            string text = MaterialPropertyLabelDisplay.GetObservationName(label);
            if (interfacerState == null || interfacerState.ActiveChamber != ActiveChamberKind.Doping) {
                return text;
            }
            if (MaterialObservationChamberLookup.GetChamberType(label) != ObservationType.Dopant) {
                return text;
            }

            MaterialAsset substrate = ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Primary);
            if (substrate == null) {
                return text;
            }
            // TODO: show element name if element name has been found
            ResearchMaterialView view = Find.NamedAsset<ResearchMaterialView>(substrate.AssetId);
            return view != null ? $"{text} than sample {view.SampleLabel}" : text;
        }

        // True when a chip click can produce work: the Research states
        // exist (we're in the Research scene, not another minigame's
        // wiki) and ResearchMask is running (not mid-setup, where the
        // consuming systems are suspended and would never clear the
        // flags).
        private static bool IsResearchInputLive() {
            if (!Game.SharedState.Has<ResearchUIInputState>()
                || !Game.SharedState.Has<HypothesisViewModelState>()
                || !Game.SharedState.Has<ChamberInterfacerState>()) {
                return false;
            }
            return !GameLoop.IsSuspended(UpdateMasks.ResearchMask);
        }
    }
}
