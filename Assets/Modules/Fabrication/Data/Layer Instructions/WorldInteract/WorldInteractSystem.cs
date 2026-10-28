using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.Stations;
using SpaceFab.Fabrication.StationControl;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SpaceFab.Fabrication.Sequence;

namespace SpaceFab.Fabrication.Movement {
    /// <summary>
    /// Routes world-interact input (Activate, Cancel) to the station-control state machine. Up/Activate
    /// enters a microgame at the robot's current station; Down/Cancel exits a microgame mid-play.
    /// Gated by WorldInteractState.WorldInteractEnabled (outer kill switch) and the station-control
    /// machine's AllowsActivate / AllowsCancel queries.
    /// Runs on any Update phase at order 5 under AttemptMask, before StationControlSystem at order 10 —
    /// so same-frame RequestActivate / RequestCancel / RequestSkip flags are consumed by the state
    /// machine this frame, not lost to the LateUpdate refresh.
    /// </summary>
    public class WorldInteractSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhaseMask.Update, 5, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadShared<WorldInteractState>()
                    .ReadShared<MovementState>()
                    .ReadShared<LayoutState>()
                    .ReadWriteShared<StationControlState>()
                    .ReadWriteShared<RobotState>()
                    .ReadWriteShared<RobotVisualsState>()
                    .ReadWriteShared<SequenceState>()
            );
        }

        // Reads world-interact keys and forwards Activate/Cancel to the station-control machine when allowed.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out WorldInteractState interactState,
                out MovementState movementState,
                out LayoutState layoutState,
                out StationControlState stationState
                );
            Find.State(
                out RobotState robotState,
                out RobotVisualsState visualsState,
                out SequenceState sequenceState
                );

            if (!interactState.WorldInteractEnabled) { return; }

            ProcessInputs(sequenceState, interactState, movementState, layoutState, stationState, robotState, visualsState);
        }

        // Dispatches Activate (Up / Space) and Cancel (Down) keypresses. Validity is checked via
        // WorldInteractUtility against the station-control state; the machine itself makes the final decision.
        static private void ProcessInputs(SequenceState sequenceState, WorldInteractState interactState, MovementState movementState, LayoutState layoutState, StationControlState stationState, RobotState robotState, RobotVisualsState visualsState) {
            if (Game.Input.IsKeyPressed(FabricationConsts.Up0) || Game.Input.IsKeyPressed(FabricationConsts.Up1) || Game.Input.IsKeyPressed(FabricationConsts.Activate)) {
                HandleActivate(sequenceState, interactState, movementState, layoutState, stationState, robotState, visualsState);
            }
            // Skip and Down0 share the physical key S, so the Skip branch must come before Cancel.
            // The phase guard ensures Skip only fires during ExitingMicrogame while a process
            // animation is blocking the exit; Cancel still owns the press during InMicrogame.
            else if (Game.Input.IsKeyPressed(FabricationConsts.Skip) && StationControlUtility.AllowsSkipProcessAnimation(stationState)) {
                HandleSkipProcessAnimation(stationState);
            }
            // else if (Game.Input.IsKeyPressed(FabricationConsts.Down0) || Game.Input.IsKeyPressed(FabricationConsts.Down1)) {
            //     HandleCancel(interactState, stationState);
            //     // TODO: Handle Close Results
            // }
        }

        // Looks up the interfacer at the current slot and asks the station-control machine to activate it.
        // No-op if the gate fails or the slot is invalid; a null interfacer is allowed through so the
        // machine can stun the robot for a wrong-station attempt.
        static private void HandleActivate(SequenceState sequenceState, WorldInteractState interactState, MovementState movementState, LayoutState layoutState, StationControlState stationState, RobotState robotState, RobotVisualsState visualsState) {
            if (!WorldInteractUtility.CanActivate(interactState, stationState)) 
            {
                return; 
            }

            int slotIndex = movementState.CurrSlotPosition;
            if (slotIndex < 0 || slotIndex >= layoutState.StationSlots.Length) { return; }

            MicrogameStationInterfacer interfacer = layoutState.StationSlots[slotIndex].AssignedStationInterfacer;
            Log.Msg("[WorldInteractSystem] Activate pressed at slot {0}; forwarding to RequestActivate", slotIndex);
            StationControlUtility.RequestActivate(sequenceState, stationState, robotState, visualsState, interfacer);
        }

        // Forwards a Cancel request to the station-control machine. Honored only during InMicrogame.
        static private void HandleCancel(WorldInteractState interactState, StationControlState stationState) {
            if (!WorldInteractUtility.CanCancel(interactState, stationState)) { return; }

            Log.Msg("[WorldInteractSystem] Cancel pressed; forwarding to RequestCancel");
            StationControlUtility.RequestCancel(stationState);
        }

        // Forwards a Skip request to the station-control machine. Honored only while a process
        // animation is blocking the exit (Phase == ExitingMicrogame && ProcessAnimationInProgress).
        // Phase gating happens at the call site via StationControlUtility.AllowsSkipProcessAnimation;
        // the outer WorldInteractEnabled kill switch is intentionally not consulted here, so a
        // disabled interact state can't strand the player behind the animation.
        static private void HandleSkipProcessAnimation(StationControlState stationState) {
            Log.Msg("[WorldInteractSystem] Skip pressed; forwarding to RequestSkipProcessAnimation");
            StationControlUtility.RequestSkipProcessAnimation(stationState);
        }
    }
}
