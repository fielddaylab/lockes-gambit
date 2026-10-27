using BeauUtil;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Music;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Handles post-load setup for the Research minigame: populates the available-materials
    /// and required-research-materials sets from ChapterDef and ContractDef, then
    /// transitions from SetupMask to ResearchMask.
    /// Runs on PreUpdate phase at order 0 under SetupMask.
    /// </summary>
    public class ResearchTransitionSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.PreUpdate, 0, UpdateMasks.SetupMask),
                new SysPermissions()
                    .ReadShared<ChapterState>()
                    .ReadShared<PlayerProgressState>()
                    .ReadWriteShared<ResearchMinigameState>()
                    .ReadWriteShared<ResearchSampleTrayState>()
                    .ReadWriteShared<ChamberInterfacerState>()
                    .ReadWriteShared<BatteryChamberState>()
                    .ReadWriteShared<ThermalChamberState>()
                    .ReadWriteShared<HypothesisViewModelState>()
                    .ReadWriteShared<ResearchPools>()
                    .ReadWrite<ResearchSamplePanel>()
            );
        }

        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ChapterState chapterState,
                out PlayerProgressState playerProgress,
                out ResearchMinigameState researchState,
                out ResearchSampleTrayState trayState
            );
            Find.State(
                out ChamberInterfacerState interfacerState,
                out BatteryChamberState batteryChamberState,
                out ThermalChamberState thermalChamberState
                );
            Find.State(
                out HypothesisViewModelState hypothesisViewModelState,
                out ContractState contractState,
                out ResearchContractRequirementsPanelState requirementsPanel
                );

            researchState.AvailableMaterials.Clear();
            if (chapterState.ChapterDefinition != null) {
                StringHash32[] excluded = chapterState.ChapterDefinition.ExcludeFromResearch;
                foreach (var id in chapterState.ChapterDefinition.AvailableMaterials) {
                    if (IsExcluded(excluded, id)) continue;
                    researchState.AvailableMaterials.Add(id);
                }
            }

            //researchState.RequiredResearchGoals.Clear();
            if (contractState.ContractDefinition)
            {
                researchState.RequiredResearchGoals = contractState.ContractDefinition.RequiredMaterialProperties();
            }

            // Seed the sandbox with previously-confirmed properties for the
            // materials in this chapter's scope. Must run after AvailableMaterials
            // is populated (above). Mid-session
            // resume goes through ResearchStateUtility.ImportState instead and
            // bypasses this load.
            ResearchStateUtility.LoadFromPlayerProgress(researchState, playerProgress);

            // Spawn the tray's draggable samples for this chapter's available
            // materials. Idempotent on re-entry — the utility clears any
            // previously-spawned gems before refilling.
            ResearchSampleTrayUtility.SpawnTray(trayState, researchState);

            // Seed the hypothesis viewmodel: no hypothesis selected on
            // entry, first rebuild on the next LateUpdate pass.
            hypothesisViewModelState.HypothesisSelected = false;
            hypothesisViewModelState.HypothesisContext = StringHash32.Null;
            HypothesisViewModelUtility.RequestRebuild(hypothesisViewModelState);

            // Initialize requirements display
            ContractUIUtility.InitializeVisuals(Find.State<ContractState>().CachedRequirements, requirementsPanel.Table);
            Positioning.SetHeightDelta(requirementsPanel.Bounds, requirementsPanel.Table.Sizer.LastSize.y + requirementsPanel.HeightPadding);

            // Init Battery Chamber. Instantiate the meter rig variant for
            // this save's unlock state under BatteryContainer, then prime
            // the dial: assigning Battery first lets RefreshVisualState
            // populate the freshly-spawned slots in the same pass.
            ResearchVoltageConfig voltageConfig = Find.GlobalAsset<ResearchVoltageConfig>();
            if (voltageConfig != null && batteryChamberState.BatteryContainer != null && batteryChamberState.Battery == null)
            {
                GameObject meterPrefab = playerProgress.BigBatteryUnlocked ? voltageConfig.BigBatteryMeterPrefab : voltageConfig.SmallBatteryMeterPrefab;
                if (meterPrefab != null)
                {
                    GameObject meterInstance = UnityEngine.Object.Instantiate(meterPrefab, batteryChamberState.BatteryContainer, false);
                    batteryChamberState.Battery = meterInstance.GetComponent<ChamberBattery>();
                }

                if (batteryChamberState.VoltageControl != null)
                {
                    batteryChamberState.VoltageControl.VoltageIndex = voltageConfig.DefaultIndex;
                    VoltageUtility.RefreshVisualState(batteryChamberState.VoltageControl, voltageConfig);
                }
            }

            // Init Thermal Chamber.
            interfacerState.LastUnlockedChamber = playerProgress.DopingChamberUnlocked ? ActiveChamberKind.Doping
                : playerProgress.ThermalChamberUnlocked ? ActiveChamberKind.Thermal : ActiveChamberKind.Voltage;
            
            if (playerProgress.ThermalChamberUnlocked)
            {
                ResearchHeatConfig heatConfig = Find.GlobalAsset<ResearchHeatConfig>();
                if (heatConfig != null && thermalChamberState.HeatControl != null)
                {
                    thermalChamberState.HeatControl.HeatIndex = heatConfig.DefaultIndex;
                    HeatUtility.RefreshVisualState(thermalChamberState.HeatControl, heatConfig);
                }
            }

            ChamberInterfacerUtility.SetActiveChamber(interfacerState, ActiveChamberKind.Voltage);

            GameLoop.SuspendUpdates(UpdateMasks.SetupMask);
            GameLoop.ResumeUpdates(UpdateMasks.ResearchMask | UpdateMasks.ResearchChamberMask);

            using (var table = TempVarTable.Alloc()) {
                table.Set("minigame", "research");
                ScriptUtility.Trigger(ResearchScriptTriggers.OnSetupComplete, table);
            }
        }

        // Linear membership check against ChapterDef.ExcludeFromResearch.
        // The list is small (handful of ids per chapter) so a hash-set
        // build is not worth the allocation.
        private static bool IsExcluded(StringHash32[] excluded, StringHash32 id) {
            if (excluded == null) return false;
            for (int i = 0; i < excluded.Length; i++) {
                if (excluded[i] == id) return true;
            }
            return false;
        }
    }
}
