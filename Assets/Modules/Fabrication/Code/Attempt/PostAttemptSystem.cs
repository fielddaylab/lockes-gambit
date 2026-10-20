using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Sequence;
using SpaceFab.Fabrication.StationControl;
using SpaceFab.Fabrication.Stations;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication {
    /// <summary>
    /// Manages the player's current attempt (from timer start to timer end).
    /// Runs on Update phase at order 0, no category mask. Currently a stub.
    /// </summary>
    public class PostAttemptSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.PostAttemptMask),
                new SysPermissions()
                    .ReadWriteShared<ModeState>()
                    .ReadWriteShared<ResultDisplayState>()
                    .ReadWriteShared<FabricationMinigameState>()
                    .ReadShared<TimeState>()
                    .ReadShared<WaferState>()
            );
        }

        // TODO: implement post attempt sequence progression.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ModeState modeState,
                out ResultDisplayState displayState,
                out SequenceState sequenceState,
                out ContractState contractState
                );

            Find.State(
                out FabricationMinigameState fabState,
                out TimeState timeState,
                out WaferState waferState
                );

            if (modeState.CurrMode != LevelMode.PostAttempt) { return; }

            if (modeState.ChangedModeThisFrame)
            {
                // Record the run on the live minigame state. ExportState copies this into
                // FabricationSaveState.FinalizedTotalCycles when the player exits the scene.
                // compute total cycles based on buckets
                int stationNum = sequenceState.StepRuntime.Length;
                FabricationSequence fabSequence = sequenceState.Level.Sequence;
                float Coefficient = contractState.ContractAssets?.fabCoefficient ?? 1;

                float bucket1 = stationNum * fabSequence.bucketThreshold1 * Coefficient;
                float bucket2 = stationNum * fabSequence.bucketthreshold2 * Coefficient;

                float elapsedTime = TimeStateUtility.GetElapsed(timeState);
                if (elapsedTime <= bucket1) { fabState.TotalCycles = 1; }
                else if (elapsedTime <= bucket2) { fabState.TotalCycles = 2; }
                else { fabState.TotalCycles = 3; }
                
                float accuracy = WaferStateUtility.GetAggregatedPrecision(waferState);
                fabState.Precision = accuracy;

                // display results
                Log.Msg("[PostAttemptSystem] displaying results");
                displayState.DisplayRequestedThisFrame = true;
            }
        }
    }
}
