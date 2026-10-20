using FieldDay;
using FieldDay.Systems;
using SpaceFab.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// Repaints the wiki's page content when Research state the open page
    /// renders from has changed. Observation and property chips read the
    /// hypothesis viewmodel for their greyed state and the chamber for
    /// their doping-comparison text, so a chip click has to reach the wiki
    /// as well as the sample panel.
    ///
    /// LateUpdate 550: after HypothesisViewModelSystem (100) has raised
    /// HypothesisChangedThisFrame and before WikiRefreshSystem's drain
    /// (800), so a click restyles the wiki in the same frame it changes
    /// the panel. ActiveChamberChangedThisFrame is raised by a chamber
    /// button during the EventSystem pass and not cleared until
    /// ChamberActivationSystem runs in the next frame's Update, so it is
    /// readable here on the click frame; SlotMaterialUpdatedThisFrame
    /// survives until LateUpdate 1000.
    /// </summary>
    public class ResearchWikiRefreshBridgeSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 550, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadShared<HypothesisViewModelState>()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadWriteShared<WikiViewState>()
            );
        }

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out HypothesisViewModelState viewModel,
                out ChamberInterfacerState interfacerState,
                out WikiViewState wikiState
            );

            bool changed = viewModel.HypothesisChangedThisFrame
                || interfacerState.ActiveChamberChangedThisFrame
                || interfacerState.SlotMaterialUpdatedThisFrame;
            if (!changed) {
                return;
            }

            WikiUtility.Invalidate(wikiState, WikiViewDirtyFlags.PageChips);
        }
    }
}
