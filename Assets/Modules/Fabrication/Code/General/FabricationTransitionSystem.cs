using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Music;
using FieldDay.Systems;
using SpaceFab.Fabrication.Sequence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication {
    /// <summary>
    /// Handles post-load setup and post-attempt teardown.
    /// Runs on Update phase at order 0, no category mask.
    /// </summary>
    public class FabricationTransitionSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.PreUpdate, 0, UpdateMasks.SetupMask).AllowDuringLoad(),
                new SysPermissions()
                    .ReadWriteShared<FabricationTransitionState>()
                    .ReadShared<ContractState>()
                    .ReadWriteShared<SequenceState>()
            );
        }

        // Reacts to the current exit-request state: shows the modal on request, hides it on confirm.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out FabricationTransitionState transitionState,
                out ModeState modeState,
                out ContractState contractState,
                out SequenceState sequenceState
                );

            Log.Msg("[FabricationTransitionSystem] Setting up level...");
            // setup
            if (contractState.ContractAssets) {
                sequenceState.Level = contractState.ContractAssets.FabricationLevel;
            }
            else {
                if (DebugFlags.LaunchedFromThisScene) {
                    sequenceState.Level = transitionState.DEBUG_FabricationLevel;
                } else {
                    Log.Warn("FabricationTransistionSystem] Tried to load contract assets but returned null!");
                }
            }

            Log.Msg("[FabricationTransitionSystem] Setup complete!");

            // Enter Pre-Attempt Mode
            Log.Msg("[FabricationTransitionSystem] Transitioning to PreAttempt Mode");
            ModeUtility.SetNewMode(modeState, LevelMode.PreAttempt);
            GameLoop.SuspendUpdates(UpdateMasks.SetupMask);
            GameLoop.ResumeUpdates(UpdateMasks.PreAttemptMask);
        }
    }
}
