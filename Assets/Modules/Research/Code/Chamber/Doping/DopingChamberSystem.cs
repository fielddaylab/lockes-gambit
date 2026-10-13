using System;
using System.Linq;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Drives Doping chamber state every frame the chamber is active. Reads
    /// the active slots; computes variance via the
    /// material's MaterialPhysicsProfile; updates the CircuitRenderer's bulb
    /// strength and flow speed. Clears the slot on temperature-stability failure.
    /// Runs on Update at order 100 under ResearchChamberMask, after
    /// CircuitAnimationSystem (order 0) reads its previous state.
    /// </summary>
    public class DopingChamberSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 130, UpdateMasks.ResearchChamberMask),
                new SysPermissions()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadWriteShared<DopingChamberState>()
                    .ReadShared<ResearchExplosionState>()
                    .ReadShared<ResearchPools>()
                    .ReadWrite<CircuitRenderer>()
                    .ReadWrite<ResearchSlot>()
            );
        }

        private static void ProcessWork(float deltaTime)
        {
            ChamberInterfacerState interfacerState = Find.State<ChamberInterfacerState>();
            if (ChamberInterfacerUtility.GetActiveChamber(interfacerState) != ActiveChamberKind.Doping)
                return;

            DopingChamberState dopingChamberState = Find.State<DopingChamberState>();
            
            if (!interfacerState.SlotMaterialUpdatedThisFrame && !dopingChamberState.AtomicViewChangedThisFrame)
                return;

            Find.State(out ResearchExplosionState explosionState,
                       out ResearchPools pools,
                       out ResearchMinigameState researchState);

            if (interfacerState.LastUpdatedKind == ChamberSlotKind.Primary) {
                UpdateSemiconductor(interfacerState, dopingChamberState, researchState, explosionState, pools);
            }
            else {
                UpdateDopant(interfacerState, dopingChamberState, researchState, explosionState, pools);
            }

            dopingChamberState.AtomicViewChangedThisFrame = false;

            if (dopingChamberState.SampleHolder == null) return;

            bool substrateSlotted = ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Primary) != null;
            dopingChamberState.SampleHolder.SetActive(substrateSlotted);
            ChamberInterfacerUtility.SetReceptive(interfacerState, ChamberSlotKind.Secondary, substrateSlotted);

            Find.State(out ResearchSampleTrayState trayState);
            ResearchSampleTrayUtility.SetTrayView(trayState, interfacerState);

            if (!substrateSlotted) return;
            UpdateAtomicView(interfacerState, dopingChamberState, researchState);
        }

        private static void UpdateSemiconductor(ChamberInterfacerState interfacerState, DopingChamberState dopingChamber, ResearchMinigameState researchState, ResearchExplosionState explosionState, ResearchPools vfxPool)
        {
            MaterialAsset material = ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Primary);
            MaterialPhysicsProfile profile = material == null ? null : Find.NamedAsset<MaterialPhysicsProfile>(material.AssetId);

            // Clear dopant
            ResearchSlotUtility.FillInSlot(interfacerState, ChamberInterfacerUtility.GetSlot(interfacerState, ChamberSlotKind.Secondary), ChamberSlotKind.Secondary, null);
            dopingChamber.EmptyState.text = "";
            dopingChamber.SubstrateAtom.gameObject.SetActive(false);
            dopingChamber.DopantAtom.gameObject.SetActive(false);

            if (material == null || profile == null) {
                dopingChamber.EmptyState.text = "ADD\nSEMICONDUCTOR SAMPLE\n|\nV";
                dopingChamber.EmptyState.color = Color.white;
                CircuitUtility.SetLightStrength(dopingChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(dopingChamber.Circuit, 0f);
                return;
            }

            // Substrates must be confirmed semiconductors.
            ResearchSlot slot = ChamberInterfacerUtility.GetSlot(interfacerState, ChamberSlotKind.Primary);
            if (!ResearchStateUtility.HasConfirmed(researchState, material.AssetId, MaterialPropertyLabel.Semiconductor, StringHash32.Null)) {
                dopingChamber.EmptyState.text = "NOT A KNOWN\nSEMICONDUCTOR";
                dopingChamber.EmptyState.color = Color.red;

                ResearchExplosionUtility.ExplodeSlot(
                    explosionState, vfxPool, interfacerState, slot, ChamberSlotKind.Primary,
                    ExplosionStyle.InvalidSubstrate, delay: 1f); // TODO: add explosion style if needed
                dopingChamber.AtomicViewChangedThisFrame = true;

                return;
            }

            if (material.ConstituentElementNames.Length > 1) {
                dopingChamber.HostElementIndex = material.AtomicRadii[0] >= material.AtomicRadii[1] ? 0 : 1;
            }
            dopingChamber.SubstrateAtom.gameObject.SetActive(true);

            float current = MaterialPhysicsUtility.GetCurrent(profile, dopingChamber.Voltage, dopingChamber.Temperature);
            CircuitUtility.SetLightStrength(dopingChamber.Circuit, current);
            CircuitUtility.SetFlowStrength(dopingChamber.Circuit, current, profile.IsHighMobility, profile.IsLightEmitting);
        }

        private static void UpdateDopant(ChamberInterfacerState interfacerState, DopingChamberState dopingChamber, ResearchMinigameState researchState, ResearchExplosionState explosionState, ResearchPools vfxPool)
        {
            MaterialAsset substrate = ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Primary);
            MaterialPhysicsProfile profile = substrate == null ? null : Find.NamedAsset<MaterialPhysicsProfile>(substrate.AssetId);

            if (substrate == null || profile == null) {
                return;
            }

            float current = MaterialPhysicsUtility.GetCurrent(profile, dopingChamber.Voltage, dopingChamber.Temperature);
            MaterialAsset dopant = ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Secondary);
            if (dopant == null) {
                CircuitUtility.SetLightStrength(dopingChamber.Circuit, current);
                CircuitUtility.SetFlowStrength(dopingChamber.Circuit, current, profile.IsHighMobility, profile.IsLightEmitting);
                return;
            }

            ResearchSlot slot = ChamberInterfacerUtility.GetSlot(interfacerState, ChamberSlotKind.Secondary);

            int index = dopingChamber.HostElementIndex;
            bool validRadius = dopant.ConstituentElementNames.Length <= 1 && substrate.SimpleAtomicRadii[index] > dopant.SimpleAtomicRadii[0];
            bool validElectronDiff = Mathf.Abs(substrate.ValenceElectronCounts[index] - dopant.ValenceElectronCounts[0]) == 1;

            if (validRadius && validElectronDiff) {
                float dopedCurrent = MaterialPhysicsUtility.GetCurrent(profile, dopingChamber.Voltage, 1f);
                CircuitUtility.SetLightStrength(dopingChamber.Circuit, dopedCurrent);
                CircuitUtility.SetFlowStrength(dopingChamber.Circuit, dopedCurrent, profile.IsHighMobility, profile.IsLightEmitting);

                // Auto-populate 'Atom fits' chip
                if (ResearchInventoryUtility.AddObservation(researchState, dopant.AssetId, MaterialPropertyLabel.AtomicRadiusCompliant, substrate.AssetId)) {
                    Find.State(out HypothesisViewModelState viewModelState);
                    HypothesisViewModelUtility.RequestRebuild(viewModelState);
                }
            }
            else if (!validRadius) {
                ResearchExplosionUtility.ExplodeSlot(
                    explosionState, vfxPool, interfacerState, slot, ChamberSlotKind.Secondary,
                    ExplosionStyle.TooBig, delay: 1f);
                CircuitUtility.SetLightStrength(dopingChamber.Circuit, current);
                CircuitUtility.SetFlowStrength(dopingChamber.Circuit, current, profile.IsHighMobility, profile.IsLightEmitting);
                dopingChamber.AtomicViewChangedThisFrame = true;
            }
        }

        private static void UpdateAtomicView(ChamberInterfacerState interfacer, DopingChamberState dopingChamber, ResearchMinigameState researchState)
        {
            // Substrate atom
            MaterialAsset substrate = ChamberInterfacerUtility.GetCurrent(interfacer, ChamberSlotKind.Primary);
            if (substrate == null) return;
            int hostIndex = dopingChamber.HostElementIndex;
            Assert.False(dopingChamber.SubstrateAtom == null);
            
            MaterialAtom substrateAtom = dopingChamber.SubstrateAtom;
            int cap = substrate.ValenceElectronCounts[hostIndex];
            for (int i = 0; i < substrateAtom.ElectronSprites.Length; i++) {
                substrateAtom.ElectronSprites[i].SetAlpha(i < cap ? 1f : 0f);
            }

            float substrateScale = MaterialAtomicViewUtility.EvaluateAtomScale(substrate.SimpleAtomicRadii[dopingChamber.HostElementIndex]);
            substrateAtom.MaterialSprite.transform.localScale = new Vector3(substrateScale, substrateScale, substrateScale);

            // Dopant atom -- empty
            Assert.False(dopingChamber.DopantAtom == null);

            MaterialAsset dopant = ChamberInterfacerUtility.GetCurrent(interfacer, ChamberSlotKind.Secondary);
            MaterialAtom dopantAtom = dopingChamber.DopantAtom;
            dopantAtom.gameObject.SetActive(dopant != null);
            if (dopant == null) { return; }

            // Dopant atom -- filled
            ResearchMaterialView dopantView = Find.NamedAsset<ResearchMaterialView>(dopant.AssetId);
            int count = dopant.ValenceElectronCounts[0];
            int excess = count - cap;
            bool dopantKnown = researchState != null
                && researchState.SandboxProperties.TryGetValue(dopant.AssetId, out var dopantRecord)
                && !MaterialPropertyRecordUtility.IsEmpty(dopantRecord);

            dopantAtom.MaterialSprite.color = dopantView.AtomColor[0];
            dopantAtom.Label.text = dopantKnown ? dopant.ShortName : "?";
            float dopantScale = MaterialAtomicViewUtility.EvaluateAtomScale(dopant.SimpleAtomicRadii[0]);
            dopantAtom.MaterialSprite.transform.localScale = new Vector3(dopantScale, dopantScale, dopantScale);

            for (int i = 0; i < dopantAtom.ElectronSprites.Length; i++) {
                SpriteRenderer electron = dopantAtom.ElectronSprites[i];
                
                if (count <= cap) {
                    electron.SetAlpha(i < count ? 1f : 0f);
                } else {
                    bool front = i < cap;
                    bool back = i >= dopantAtom.ElectronSprites.Length - excess;
                    electron.SetAlpha(front || back ? 1f : 0f);
                }
            }
        }
    }
}
