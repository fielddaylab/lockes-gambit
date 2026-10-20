using BeauUtil.Variants;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;

namespace SpaceFab.Research
{
    public class ChamberActivationSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.ResearchChamberMask),
                new SysPermissions()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadShared<BatteryChamberState>()
                    .ReadShared<ThermalChamberState>()
                    .ReadShared<DopingChamberState>()
                    .ReadShared<ResearchSampleTrayState>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            ChamberInterfacerState interfacer = Find.State<ChamberInterfacerState>();
            if (!interfacer.ActiveChamberChangedThisFrame) { return; }
            
            Find.State(
                out BatteryChamberState batteryChamber,
                out ThermalChamberState thermalChamber,
                out DopingChamberState dopingChamber,
                out ResearchSampleTrayState trayState
            );

            batteryChamber.Root.SetActive(false);
            thermalChamber.Root.SetActive(false);
            dopingChamber.Root.SetActive(false);

            ActiveChamberKind activeChamber = ChamberInterfacerUtility.GetActiveChamber(interfacer);
            ResearchPools pools = Find.State<ResearchPools>();
            float primarySlotY = -0.26f;

            switch (activeChamber) {
                case ActiveChamberKind.Voltage:
                    BatteryChamberUtility.ResetState(batteryChamber);
                    primarySlotY = batteryChamber.PrimarySlotY;
                    if (pools != null) {
                    }
                    break;
                case ActiveChamberKind.Thermal:
                    ThermalChamberUtility.ResetState(thermalChamber);
                    primarySlotY = thermalChamber.PrimarySlotY;
                    if (pools != null) {
                    }
                    break;
                case ActiveChamberKind.Doping:
                    DopingChamberUtility.ResetState(dopingChamber);
                    primarySlotY = dopingChamber.PrimarySlotY;
                    if (pools != null) {
                    }
                    break;
            }

            Positioning.SetLocalY(interfacer.PrimarySlot.transform, primarySlotY);

            ResearchSlotUtility.FillInSlot(interfacer, ChamberInterfacerUtility.GetSlot(interfacer, ChamberSlotKind.Secondary), ChamberSlotKind.Secondary, null);
            ChamberInterfacerUtility.SetReceptive(interfacer, ChamberSlotKind.Primary, activeChamber != ActiveChamberKind.None);
            ChamberInterfacerUtility.SetReceptive(interfacer, ChamberSlotKind.Secondary, false);
            ResearchSampleTrayUtility.SetTrayView(trayState, interfacer);

            ScriptUtility.WriteVariable(new TableKeyPair("research", "chamberId"), activeChamber.ToString());
            ScriptUtility.Trigger(ResearchScriptTriggers.OnChamberSwitched);

            interfacer.ActiveChamberChangedThisFrame = false;
        }
    }
}