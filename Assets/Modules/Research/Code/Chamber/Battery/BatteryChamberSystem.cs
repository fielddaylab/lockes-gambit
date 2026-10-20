using FieldDay;
using FieldDay.Audio;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Drives Battery chamber state every frame the chamber is active. Reads
    /// the active slot and the Battery's voltage; computes current via the
    /// material's MaterialPhysicsProfile; updates the CircuitRenderer's bulb
    /// strength and flow speed. Clears the slot on voltage-stability failure.
    /// Runs on Update at order 100 under ResearchChamberMask, after
    /// CircuitAnimationSystem (order 0) reads its previous state.
    /// </summary>
    public class BatteryChamberSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 110, UpdateMasks.ResearchChamberMask),
                new SysPermissions()
                    .ReadWriteShared<ChamberInterfacerState>()
                    .ReadWriteShared<BatteryChamberState>()
                    .ReadWriteShared<ResearchExplosionState>()
                    .ReadWriteShared<ResearchPools>()
                    .ReadWrite<CircuitRenderer>()
                    .ReadWrite<ResearchSlot>()
            );
        }

        private static void ProcessWork(float deltaTime)
        {            
            ChamberInterfacerState interfacerState = Find.State<ChamberInterfacerState>();
            if (ChamberInterfacerUtility.GetActiveChamber(interfacerState) != ActiveChamberKind.Voltage)
            {
                return;
            }

            Find.State(out BatteryChamberState batteryChamberState,
                       out ResearchExplosionState explosionState,
                       out ResearchPools vfxPool);

            // Read once outside the per-Battery loop so the multi-Battery case
            // (unlikely today but cheap to support) doesn't re-poll the flag.
            bool slotChangedThisFrame = interfacerState.SlotMaterialUpdatedThisFrame;
            ChamberSlotKind changedKind = interfacerState.LastUpdatedKind;

            bool slotDirty = slotChangedThisFrame && changedKind == batteryChamberState.SlotKind;
            bool dirty = batteryChamberState.VoltageChangedThisFrame || slotDirty;

            if (dirty)
            {
                UpdateBattery(interfacerState, batteryChamberState, explosionState, vfxPool);
                batteryChamberState.VoltageChangedThisFrame = false;
            }

            // if (!interfacerState.SlotMaterialUpdatedThisFrame) return;
            // if (interfacerState.LastUpdatedKind != batteryChamberState.SlotKind) return;
            if (batteryChamberState.SampleHolder == null) return;

            bool filled = ChamberInterfacerUtility.GetCurrent(interfacerState, batteryChamberState.SlotKind) != null;
            batteryChamberState.SampleHolder.SetActive(filled);

        }

        // Single-Battery update: read material + voltage, run stability, drive
        // visuals. Splits out of ProcessWork so the loop body stays linear.
        private static void UpdateBattery(ChamberInterfacerState interfacerState, BatteryChamberState batteryChamber, ResearchExplosionState explosionState, ResearchPools vfxPool)
        {
            MaterialAsset material = ChamberInterfacerUtility.GetCurrent(interfacerState, batteryChamber.SlotKind);
            float voltage = batteryChamber.VoltageControl != null ? batteryChamber.Battery.CurrentVoltage : 0f;

            if (material == null)
            {
                CircuitUtility.SetLightStrength(batteryChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(batteryChamber.Circuit, 0f);
                return;
            }

            MaterialPhysicsProfile profile = Find.NamedAsset<MaterialPhysicsProfile>(material.AssetId);
            if (profile == null)
            {
                CircuitUtility.SetLightStrength(batteryChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(batteryChamber.Circuit, 0f);
                return;
            }

            if (!MaterialPhysicsUtility.IsStableAtVoltage(profile, voltage))
            {
                ResearchSlot slot = ChamberInterfacerUtility.GetSlot(interfacerState, batteryChamber.SlotKind);
                ResearchExplosionUtility.ExplodeSlot(
                    explosionState, vfxPool, interfacerState, slot, batteryChamber.SlotKind,
                    ExplosionStyle.VoltageBreakdown, delay: 1f);
                CircuitUtility.SetLightStrength(batteryChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(batteryChamber.Circuit, 0f);
                return;
            }

            float current = MaterialPhysicsUtility.GetCurrent(profile, voltage, batteryChamber.Temperature);
            if (current == 0 && voltage != 0) Sfx.Play(Find.State<BatteryChamberState>().NoCurrentSFX);
            CircuitUtility.SetLightStrength(batteryChamber.Circuit, current);
            CircuitUtility.SetFlowStrength(batteryChamber.Circuit, current, profile.IsHighMobility, profile.IsLightEmitting);
        }
    }
}
