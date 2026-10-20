using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Movement;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.Sequence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication {
    /// <summary>
    /// Manages the lead-in to a new player attempt
    /// Runs on Update phase at order 0, no category mask. Currently a stub.
    /// </summary>
    public class AttemptLeadInSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.AttemptLeadInMask),
                new SysPermissions()
                    .ReadShared<ModeState>()
                    .ReadWriteShared<LayoutState>()
                    .ReadWriteShared<CountdownState>()
            );
        }

        // TODO: implement attempt lead in sequence progression.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ModeState modeState,
                out LayoutState layoutState,
                out CountdownState countdownState,
                out RobotState robotState
                );
            Find.State(
                out SequenceState sequenceState,
                out SequenceVisualsState SequenceVisualsState,
                out ContractState contractState
                );

            if (modeState.CurrMode != LevelMode.AttemptLeadIn) { return; }

            if (modeState.ChangedModeThisFrame)
            {
                // lead into Attempt Mode:
                // reshuffle stations (if not already done in PreAttempt (i.e. entered from ResetSystem)
                if (layoutState.NeedsReshuffling)
                {
                    Log.Msg("[AttemptLeadInSystem] shuffling stations");

                    LayoutUtility.ShuffleStations(layoutState);
                    layoutState.NeedsReshuffling = false;
                }

                // generate wafer
                Log.Msg("[AttemptLeadInSystem] TODO: generating wafer");

                // reset sequence
                Log.Msg("[AttemptLeadInSystem] Resetting sequence");
                FabricationLevel level;
                if (contractState.ContractAssets) {
                    level = contractState.ContractAssets.FabricationLevel;
                } else if (DebugFlags.LaunchedFromThisScene) {
                    level = Find.State<FabricationTransitionState>().DEBUG_FabricationLevel;
                } else {
                    Assert.Fail("Level is not present!");
                    level = null;
                }
                SequenceUtility.ResetSequence(sequenceState, level, SequenceVisualsState);

                // show visual
                RobotUtility.UpdateStatus(robotState, RobotStatus.Holding);

                // initiate countdown
                Log.Msg("[AttemptLeadInSystem] running countdown");
                countdownState.CountdownRequestedThisFrame = true;
            }
        }
    }
}
