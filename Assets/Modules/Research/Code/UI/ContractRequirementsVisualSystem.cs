using BeauUtil;
using FieldDay;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using SpaceFab.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// Renders the Contract Requirements list against the contract's
    /// research goals. LateUpdate order 500,
    /// alongside the sample panel's render and after the confirm path
    /// at order 60 has flipped its sandbox bit.
    /// </summary>
    public class ContractRequirementsVisualSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 500, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadShared<ResearchMinigameState>()
                    .ReadShared<PlayerProgressState>()
                    .ReadWriteShared<ResearchContractRequirementsPanelState>()
            );
        }

        private static void ProcessWork(float deltaTime) {
            if (!Game.SharedState.Has<ResearchContractRequirementsPanelState>()) {
                return;
            }

            Find.State(
                out ResearchContractRequirementsPanelState panel,
                out ResearchMinigameState researchState,
                out PlayerProgressState progressState
            );

            if (!panel.NeedsRefresh && !researchState.PropertyConfirmedThisFrame) {
                return;
            }

            ContractRequirementsVisualUtility.Apply(panel, researchState, progressState);
            panel.NeedsRefresh = false;
        }
    }

    /// <summary>
    /// Pushes contract-goal progress into the requirements panel's
    /// authored rows. Invoked only when a refresh has been requested (see
    /// ContractRequirementsVisualSystem).
    /// </summary>
    public static class ContractRequirementsVisualUtility {
        public static void Apply(ResearchContractRequirementsPanelState panel, ResearchMinigameState researchState, PlayerProgressState progressState) {
            if (panel == null || panel.Table == null || researchState == null || progressState == null) {
                return;
            }

            ContractUIUtility.UpdateVisualsWithCompletion(Find.State<ContractState>().CachedRequirements, panel.Table, researchState.SandboxProperties);
        }
    }
}
