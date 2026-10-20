using FieldDay;
using FieldDay.Systems;
using SpaceFab.Save;

namespace SpaceFab.Design
{
    /// <summary>
    /// Handles the Design results-panel "Continue" hand-off. When the player clears a level, this
    /// either advances to the next level by reloading the Design scene (which re-imports and lands
    /// on the new first-unsolved level), or — on the contract's last level — confirms the normal
    /// exit back to overarching. Runs on Update under SimulateModeMask, after the results panel
    /// systems.
    /// </summary>
    public class DesignContinueSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 6, UpdateMasks.SimulateModeMask),
                new SysPermissions()
                    .ReadWriteShared<DesignResultPanel>()
                    .ReadWriteShared<MinigameRequestExitState>()
                    .ReadShared<MinigameStateInterfacer>()
                    .ReadShared<DesignMinigameState>()
                    .ReadShared<MinigameSaveStates>()
            );
        }

        // Consumes ResultState.ContinueRequested and routes to next-level reload or exit.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out DesignResultPanel resultState,
                out MinigameRequestExitState requestExitState,
                out MinigameStateInterfacer interfacer
                );
            Find.State(
                out DesignMinigameState designState,
                out MinigameSaveStates saveStates
                );

            if (!resultState.ContinueRequested) { return; }
            resultState.ContinueRequested = false;

            // The active level was already marked solved when the suite passed; advance out of it.
            DesignLevelUtility.AdvanceFromActiveLevel(saveStates.Design, designState, requestExitState, interfacer);
        }
    }
}
