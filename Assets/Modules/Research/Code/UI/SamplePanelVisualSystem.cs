using BeauUtil;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using SpaceFab.UI;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Renders every active ResearchSamplePanel against the current
    /// hypothesis viewmodel + the slotted material. LateUpdate order 500.
    /// The panel's slot chips and hypothesis chip read from the
    /// hypothesis viewmodel (single source of truth); the SAMPLE header
    /// is derived live from the slotted material's ResearchMaterialView.
    /// </summary>
    public class SamplePanelVisualSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 500, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadShared<HypothesisViewModelState>()
                    .ReadShared<ResearchMinigameState>()
                    .ReadShared<PlayerProgressState>()
                    .ReadShared<ContractState>()
                    .ReadWrite<ResearchSamplePanel>()
            );
        }

        // Tracks the verify (submit) button's visibility across frames so we can fire
        // OnVerifyButtonShown one frame after it appears — see the dispatch note below.
        private static bool s_PrevSubmitVisible;
        private static bool s_VerifyShownPending;

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out ChamberInterfacerState interfacerState,
                out HypothesisViewModelState hypoVm,
                out WikiViewState wikiState
            );
            ResearchMinigameState researchState = Find.State<ResearchMinigameState>();

            foreach (var panel in Find.Components<ResearchSamplePanel>()) {
                SamplePanelVisualUtility.Apply(panel, interfacerState, hypoVm, researchState, wikiState);
                if (panel.CompleteButton != null) {
                    Find.State(out PlayerProgressState progressState, out ContractState contractState);
                    panel.CompleteButton.gameObject.SetActive(contractState.ContractDefinition && ContractProgressUtility.IsContractSatisfied(progressState, researchState, contractState.ContractDefinition));
                }
            }

            // Onboarding hook: fire OnVerifyButtonShown the frame AFTER the verify button becomes
            // visible. Apply() above activates the button this frame; its ElementTag only registers
            // with the lookup on the next registration pass, so a highlight fired now would miss it.
            // The one-frame defer (arm this frame, dispatch next) guarantees the tag is resolvable.
            if (s_VerifyShownPending) {
                s_VerifyShownPending = false;
                ScriptUtility.Trigger(ResearchScriptTriggers.OnVerifyButtonShown);
            }
            if (hypoVm.VerifyButtonVisible && !s_PrevSubmitVisible) {
                s_VerifyShownPending = true;
            }
            s_PrevSubmitVisible = hypoVm.VerifyButtonVisible;
        }
    }

    /// <summary>
    /// Pushes derived sample state into a ResearchSamplePanel's visuals.
    /// Reads the slotted material directly off ChamberInterfacerState +
    /// the per-material ResearchMaterialView (for SampleNumber). No
    /// intermediate viewmodel; the cheap lookup beats a one-frame-stale
    /// copy. Battery scope: the picker pulls from BatteryChamberState —
    /// extend via a switch on ChamberInterfacerState.ActiveChamber when
    /// other chambers come online.
    /// </summary>
    public static class SamplePanelVisualUtility {
        public static void Apply(
            ResearchSamplePanel panel,
            ChamberInterfacerState interfacerState,
            HypothesisViewModelState hypoVm,
            ResearchMinigameState researchState,
            WikiViewState wikiState
        ) {
            if (panel == null) {
                return;
            }

            ResearchSlot primarySlot = interfacerState.PrimarySlot;
            MaterialAsset primaryMaterial = primarySlot != null ? primarySlot.CurrentMaterial : null;

            ResearchSlot secondarySlot = interfacerState.SecondarySlot;
            MaterialAsset secondaryMaterial = secondarySlot != null ? secondarySlot.CurrentMaterial : null;

            // Submit button mirrors the hypothesis viewmodel's
            // SubmitButtonVisible flag (true only when the slotted
            // material satisfies every leaf on the active page).
            // Driven explicitly here so it works in both the empty-state
            // and filled-state paths regardless of where the button
            // sits in the panel hierarchy.
            if (panel.VerifyButton != null) {
                panel.VerifyButton.gameObject.SetActive(hypoVm.VerifyButtonVisible && !hypoVm.HypothesisVerified);
            }

            WikiContent contents = Find.State<WikiContent>();

            if (panel.AddObservationButton != null) {
                bool obsTabOpen = false;
                if (wikiState.Expanded && wikiState.CurrentTabId >= 0) {
                    obsTabOpen = contents.Tabs[wikiState.CurrentTabId].AssetId == "Observations";
                    // TODO: grey out button when obs tab is open
                }
                panel.AddObservationButton.gameObject.SetActive(hypoVm.SlotCount <= 3 && !hypoVm.HypothesisVerified);
            }

            if (panel.AddPropertyButton != null) {
                bool propsTabOpen = false;
                if (wikiState.Expanded && wikiState.CurrentTabId >= 0) {
                    propsTabOpen = contents.Tabs[wikiState.CurrentTabId].AssetId == "Properties";
                    // TODO: grey out button when obs tab is open
                }
                panel.AddPropertyButton.gameObject.SetActive(hypoVm.SlotCount > 0 && !hypoVm.HypothesisSelected && !hypoVm.HypothesisVerified);
            }

            ResearchUIAssets uiAssets = Find.GlobalAsset<ResearchUIAssets>();
            ActiveChamberKind chamberKind = ChamberInterfacerUtility.GetActiveChamber(interfacerState);

            if (panel.VoltageChamberButton != null) {
                panel.VoltageChamberButton.Interactable = chamberKind != ActiveChamberKind.Voltage;
            }
            if (panel.ThermalChamberButton != null) {
                if (interfacerState.LastUnlockedChamber < ActiveChamberKind.Thermal) {
                    SamplePanelInputUtility.LockChamberButton(panel, ActiveChamberKind.Thermal, uiAssets);
                }
                else {
                    panel.ThermalChamberButton.Interactable = chamberKind != ActiveChamberKind.Thermal;
                }
            }
            if (panel.DopingChamberButton != null) {
                if (interfacerState.LastUnlockedChamber < ActiveChamberKind.Doping) {
                    SamplePanelInputUtility.LockChamberButton(panel, ActiveChamberKind.Doping, uiAssets);
                }
                else {
                    panel.DopingChamberButton.Interactable = chamberKind != ActiveChamberKind.Doping;
                }
            }

            if (panel.ChamberText != null) {
                panel.ChamberText.text = chamberKind == ActiveChamberKind.None ? "" : $"{chamberKind} Chamber";
            }

            // 1. Empty-state path: no material slotted
            bool isDopingChamber = interfacerState.ActiveChamber == ActiveChamberKind.Doping;
            bool isSlotFilled = isDopingChamber ?
                primaryMaterial != null && secondaryMaterial != null : primaryMaterial != null;

            if (!isSlotFilled) {
                if (panel.EmptyState != null) {
                    panel.EmptyState.SetActive(true);
                }
                if (panel.MainContent != null) {
                    panel.MainContent.SetActive(false);
                }
                // Explicitly hide the slot chips even if MainContent's
                // hierarchy doesn't parent them — otherwise stale
                // filled chips from the previously-slotted material
                // remain visible after the sample is pulled.
                if (panel.SlotChips != null) {
                    for (int i = 0; i < panel.SlotChips.Length; i++) {
                        if (panel.SlotChips[i] != null) {
                            panel.SlotChips[i].gameObject.SetActive(false);
                        }
                    }
                }
                if (panel.HypothesisChip != null)
                    panel.HypothesisChip.gameObject.SetActive(false);
                SamplePanelInputUtility.ClosePicker(panel);
                return;
            }

            if (panel.EmptyState != null) {
                panel.EmptyState.SetActive(false);
            }
            if (panel.MainContent != null) {
                panel.MainContent.SetActive(true);
                panel.DopingGroup.SetActive(isDopingChamber);
            }

            // 2. Sample label — derived from the slotted material's view.
            if (panel.SampleLabel != null) {
                // Known materials (any property confirmed in the sandbox)
                // show their ShortName; unknown materials show their
                // sample number prefixed with "SAMPLE ".
                MaterialAsset targetMaterial = isDopingChamber ? secondaryMaterial : primaryMaterial;
                
                bool known = researchState != null
                    && researchState.SandboxProperties.TryGetValue(targetMaterial.AssetId, out var record)
                    && !MaterialPropertyRecordUtility.IsEmpty(record);

                ResearchMaterialView view = Find.NamedAsset<ResearchMaterialView>(targetMaterial.AssetId);
                panel.SampleSprite.sprite = targetMaterial.GemSprite;
                if (known) {
                    panel.SampleLabel.text = targetMaterial.ShortName;
                    panel.SampleLabelBG.color = Color.black;
                } else {
                    //int sampleNumber = view != null ? view.SampleNumber : 0;
                    panel.SampleLabel.text = "?";
                    panel.SampleLabelBG.color = Color.gray; // gray out unknown sample label
                }

                // Set the substrate label and sprite if currently on doping chamber
                if (isDopingChamber)
                {
                    bool substrateKnown = researchState.SandboxProperties.TryGetValue(primaryMaterial.AssetId, out var substrateRecord)
                        && !MaterialPropertyRecordUtility.IsEmpty(substrateRecord);
                    panel.SubstrateSprite.sprite = primaryMaterial.GemSprite;
                    if (substrateKnown) {
                        panel.SubstrateLabel.text = primaryMaterial.ShortName;
                    } else {
                        panel.SubstrateLabel.text = "?";
                    }
                }
            }

            // 3. Slot chips render the viewmodel's slot view (player
            // picks in insertion order). Filled slots [0..SlotCount)
            // show the picked label + that label's per-type sprite;
            // remaining slots up to capacity render dashed-empty.
            int slotCount = hypoVm.SlotCount;

            if (panel.SlotChips != null) {
                for (int i = 0; i < panel.SlotChips.Length; i++) {
                    panel.SlotChips[i].gameObject.SetActive(true);
                    bool filled = i < slotCount;
                    bool locked = filled && (hypoVm.SlotLockedMask & (1u << i)) != 0;
                    string label = null;
                    ObservationType type = default;
                    if (filled) {
                        MaterialPropertyLabel slotLabel = hypoVm.SlotLabels[i];
                        label = MaterialPropertyLabelDisplay.GetObservationName(slotLabel);
                        panel.SlotChips[i].SetProperty(label, ChipFillState.Filled, locked, slotLabel, useEmptyDashedSprite: true);
                    } else {
                        panel.SlotChips[i].SetState(label, ChipFillState.Empty, locked, type, useEmptyDashedSprite: true);
                    }
                    panel.SlotChips[i].gameObject.SetActive(filled);
                }
            }

            // 3-1. Render hypothesis chip slot. Never locked — a
            // fulfilled hypothesis is auto-cleared by the viewmodel
            // rebuild, so it never displays in a locked state.
            panel.HypothesisChip.gameObject.SetActive(true);
            bool hypoFilled = hypoVm.HypothesisSelected;
            string hypoLabel = null;
            ObservationType hypoType = ObservationType.Component;
            if (hypoFilled) {
                MaterialPropertyLabel hypo = hypoVm.HypothesisLabel;
                hypoLabel = MaterialPropertyLabelDisplay.GetPropertyName(hypo, hypoVm.HypothesisContext);
                hypoType = MaterialObservationChamberLookup.GetChamberType(hypo);
                panel.HypothesisChip.SetProperty(hypoLabel, ChipFillState.Confirmed, false, hypo);
            } else {
                panel.HypothesisChip.SetState(hypoLabel, ChipFillState.Empty, false, hypoType, true);
            }
        }
    }
}
