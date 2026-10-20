using FieldDay;
using FieldDay.Audio;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Drives Thermal chamber state every frame the chamber is active. Reads
    /// the active slot and the Thermal's temperature; computes heat via the
    /// material's MaterialPhysicsProfile; updates the CircuitRenderer's bulb
    /// strength and flow speed. Clears the slot on temperature-stability failure.
    /// Runs on Update at order 100 under ResearchChamberMask, after
    /// CircuitAnimationSystem (order 0) reads its previous state.
    /// </summary>
    public class ThermalChamberSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 120, UpdateMasks.ResearchChamberMask),
                new SysPermissions()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadWriteShared<ThermalChamberState>()
                    .ReadShared<ResearchExplosionState>()
                    .ReadShared<ResearchPools>()
                    .ReadWrite<CircuitRenderer>()
                    .ReadWrite<ResearchSlot>()
            );
        }

        private static void ProcessWork(float deltaTime)
        {
            ChamberInterfacerState interfacerState = Find.State<ChamberInterfacerState>();
            if (ChamberInterfacerUtility.GetActiveChamber(interfacerState) != ActiveChamberKind.Thermal)
            {
                return;
            }

            Find.State(out ThermalChamberState thermalChamberState,
                       out ResearchExplosionState explosionState,
                       out ResearchPools vfxPool);

            // Read once outside the per-Battery loop so the multi-Battery case
            // (unlikely today but cheap to support) doesn't re-poll the flag.
            bool slotChangedThisFrame = interfacerState.SlotMaterialUpdatedThisFrame;
            ChamberSlotKind changedKind = interfacerState.LastUpdatedKind;

            bool slotDirty = slotChangedThisFrame && changedKind == thermalChamberState.SlotKind;
            bool dirty = thermalChamberState.HeatChangedThisFrame || slotDirty;

            if (dirty)
            {
                UpdateHeater(interfacerState, thermalChamberState, explosionState, vfxPool);
                thermalChamberState.HeatChangedThisFrame = false;
            }

            // if (!interfacerState.SlotMaterialUpdatedThisFrame) return;
            // if (interfacerState.LastUpdatedKind != thermalChamberState.SlotKind) return;
            if (thermalChamberState.SampleHolder == null) return;

            bool filled = ChamberInterfacerUtility.GetCurrent(interfacerState, thermalChamberState.SlotKind) != null;
            thermalChamberState.SampleHolder.SetActive(filled);

        }

        private static void UpdateHeater(ChamberInterfacerState interfacerState, ThermalChamberState thermalChamber, ResearchExplosionState explosionState, ResearchPools vfxPool)
        {
            MaterialAsset material = ChamberInterfacerUtility.GetCurrent(interfacerState, thermalChamber.SlotKind);
            float temperature = thermalChamber.HeatControl != null ? thermalChamber.HeatControl.CurrentTemperature : 0f;

            if (material == null)
            {
                CircuitUtility.SetLightStrength(thermalChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(thermalChamber.Circuit, 0f);
                return;
            }

            MaterialPhysicsProfile profile = Find.NamedAsset<MaterialPhysicsProfile>(material.AssetId);
            if (profile == null)
            {
                CircuitUtility.SetLightStrength(thermalChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(thermalChamber.Circuit, 0f);
                return;
            }

            if (!MaterialPhysicsUtility.IsStableAtTemperature(profile, temperature))
            {
                ResearchSlot slot = ChamberInterfacerUtility.GetSlot(interfacerState, thermalChamber.SlotKind);
                ResearchExplosionUtility.ExplodeSlot(
                    explosionState, vfxPool, interfacerState, slot, thermalChamber.SlotKind,
                    ExplosionStyle.TemperatureBreakdownHot, delay: 1f);
                CircuitUtility.SetLightStrength(thermalChamber.Circuit, 0f);
                CircuitUtility.SetFlowStrength(thermalChamber.Circuit, 0f);
                return;
            }

            float current = MaterialPhysicsUtility.GetCurrent(profile, thermalChamber.Voltage, temperature);
            if (current == 0) Sfx.Play(Find.State<BatteryChamberState>().NoCurrentSFX);
            CircuitUtility.SetLightStrength(thermalChamber.Circuit, current);
            CircuitUtility.SetFlowStrength(thermalChamber.Circuit, current, profile.IsHighMobility, profile.IsLightEmitting);
        }
    }
}
