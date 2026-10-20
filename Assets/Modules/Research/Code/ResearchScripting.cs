using System;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using Leaf.Runtime;
using SpaceFab.Materials;
using SpaceFab.UI;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Leaf-callable queries and commands specific to the Research minigame.
    /// </summary>
    public static class ResearchScripting {
        // Opens the shared wiki to the material page of the last sample to have a property newly
        // confirmed this session. No-op when no research session is active, nothing new has been
        // discovered yet, no wiki is present, or that material has no authored material page.
        [LeafMember("OpenWikiToLastDiscovery")]
        public static void Leaf_OpenWikiToLastDiscovery() {
            if (!Game.SharedState.Has<ResearchMinigameState>()) { return; }
            ResearchMinigameState researchState = Find.State<ResearchMinigameState>();
            if (!researchState.LastDiscovery.IsValid) { return; }

            var contents = Find.State<WikiContent>();
            var address = WikiContentUtility.LookupMaterialPageAddress(contents, researchState.LastDiscovery.MaterialId);
            
            WikiUtility.OpenTo(Find.State<WikiViewState>(), address);
        }

        // Resets the currently-active research chamber to its default state (e.g. the Battery's
        // voltage dial back to its initial setting). No-op when no chamber interfacer is present
        // or no chamber is active. Each chamber kind routes to its own ResetState below.
        [LeafMember("ResetCurrentChamber")]
        public static void Leaf_ResetCurrentChamber() {
            if (!Game.SharedState.Has<ChamberInterfacerState>()) { return; }

            switch (Find.State<ChamberInterfacerState>().ActiveChamber) {
                case ActiveChamberKind.Voltage:
                    if (Game.SharedState.Has<BatteryChamberState>()) {
                        BatteryChamberUtility.ResetState(Find.State<BatteryChamberState>());
                    }
                    break;
                case ActiveChamberKind.Thermal:
                    if (Game.SharedState.Has<ThermalChamberState>()) {
                        ThermalChamberUtility.ResetState(Find.State<ThermalChamberState>());
                    }
                    break;
                case ActiveChamberKind.Doping:
                    if (Game.SharedState.Has<DopingChamberState>()) {
                        Find.State(out ChamberInterfacerState interfacerState);
                        ResearchSlotUtility.FillInSlot(interfacerState, ChamberInterfacerUtility.GetSlot(interfacerState, ChamberSlotKind.Primary), ChamberSlotKind.Primary, null);
                        DopingChamberUtility.ResetState(Find.State<DopingChamberState>());
                    }
                    break;
            }
        }

        [LeafMember("LockChamber")]
        public static void Leaf_LockChamber(string chamberId)
        {
            if (Enum.TryParse(chamberId, out ActiveChamberKind chamberKind)) {
                Find.State(out ChamberInterfacerState interfacer);
                interfacer.LastUnlockedChamber = chamberKind - 1;
                ResearchUIAssets uiAssets = Find.GlobalAsset<ResearchUIAssets>();
                foreach (var panel in Find.Components<ResearchSamplePanel>()) {
                    if (panel == null) continue;
                    SamplePanelInputUtility.LockChamberButton(panel, chamberKind, uiAssets);
                }
            }
        }

        [LeafMember("UnlockChamber")]
        public static void Leaf_UnlockChamber(string chamberId)
        {
            if (Enum.TryParse(chamberId, out ActiveChamberKind chamberKind)) {
                Find.State(out ChamberInterfacerState interfacer);
                interfacer.LastUnlockedChamber = chamberKind;
                ResearchUIAssets uiAssets = Find.GlobalAsset<ResearchUIAssets>();
                foreach (var panel in Find.Components<ResearchSamplePanel>()) {
                    if (panel == null) continue;
                    SamplePanelInputUtility.UnlockChamberButton(panel, chamberKind, uiAssets);
                }
            }
        }

        [LeafMember("AddResearchObservation")]
        public static void Leaf_AddObservation(StringHash32 materialId, MaterialPropertyLabel observationType) {
            Assert.True(!MaterialPropertyLabelUtility.IsPersistent(observationType), "Property '{0}' is persistent!", observationType);
            Find.State(out ResearchMinigameState minigameState, out HypothesisViewModelState viewModelState);
            if (ResearchInventoryUtility.AddObservation(minigameState, materialId, observationType, null)) {
                HypothesisViewModelUtility.RequestRebuild(viewModelState);
            }
        }

        [LeafMember("HasConfirmedProperty")]
        public static bool Leaf_HasConfirmedProperty(StringHash32 materialId, MaterialPropertyLabel observationType) {
            Assert.True(MaterialPropertyLabelUtility.IsPersistent(observationType), "Property '{0}' is not persistent!", observationType);
            Find.State(out PlayerProgressState playerProgress, out WikiContent wikiContent);
            MaterialPropertyRecord propRecord = WikiContentUtility.GetMaterialRecord(materialId, playerProgress, wikiContent.ResearchContext);
            return MaterialPropertyRecordUtility.Has(propRecord, observationType, null);
        }
    }
}