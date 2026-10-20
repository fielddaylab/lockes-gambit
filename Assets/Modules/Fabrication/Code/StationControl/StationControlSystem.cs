using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using Leaf.Runtime;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Movement;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.Sequence;
using SpaceFab.Fabrication.Stations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.StationControl {
    /// <summary>
    /// Drives the station-control phase machine. Detects slot-arrival / slot-departure from MovementState's
    /// one-frame SlotChangedThisFrame flag, advances timer-based transitions (EnteringMicrogame, ExitingMicrogame,
    /// Stunned), and flips MicrogameMask when crossing into/out of InMicrogame. Runs on Update at order 10 under
    /// AttemptMask (after MovementSystem at order 0 and WorldInteractSystem at order 5, so same-frame
    /// RequestActivate / RequestCancel / RequestSkip flags are consumed before LateUpdate clears them).
    /// </summary>
    public class StationControlSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 10, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<StationControlState>()
                    .ReadShared<MovementState>()
                    .ReadWriteShared<RobotState>()
                    .ReadShared<LayoutState>()
                    .ReadWriteShared<RobotVisualsState>()
                    .ReadWriteShared<WaferState>()
            );
        }

        // Dispatches to the handler for the current station-control phase.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out StationControlState stationState,
                out MovementState movementState,
                out RobotState robotState,
                out LayoutState layoutState
                );
            Find.State(out RobotVisualsState visualsState);
            Find.State(out SequenceVisualsState sequenceState); 

            switch (stationState.Phase) {
                case StationControlPhase.Traveling:
                    ProcessTraveling(stationState, movementState, layoutState);
                    break;
                case StationControlPhase.AtStation:
                    ProcessAtStation(stationState, movementState);
                    break;
                case StationControlPhase.EnteringMicrogame:
                    ProcessEnteringMicrogame(stationState, sequenceState, deltaTime);
                    break;
                case StationControlPhase.InMicrogame:
                    ProcessInMicrogame(stationState);
                    break;
                case StationControlPhase.ResolvingCompletion:
                    ProcessResolvingCompletion(stationState);
                    break;
                case StationControlPhase.AwaitingRetry:
                    // Frozen, waiting on StationControlUtility.RestartMicrogame (driven by the restart
                    // panel / Leaf). Nothing to tick.
                    break;
                case StationControlPhase.ExitingMicrogame:
                    ProcessExitingMicrogame(stationState, deltaTime);
                    break;
                case StationControlPhase.Stunned:
                    ProcessStunned(stationState, robotState, visualsState, deltaTime);
                    break;
                default:
                    break;
            }
        }

        // Watches for slot arrival (SlotChangedThisFrame && !IsTraveling). On arrival, caches the slot's
        // assigned interfacer and transitions to AtStation. Dispatches FabStationArrived.
        static private void ProcessTraveling(StationControlState stationState, MovementState movementState, LayoutState layoutState) {
            if (!movementState.SlotChangedThisFrame) {
                return;
            }
            // SlotChangedThisFrame also fires at move-start (write to TRAVELING). The arrival edge is when
            // the new position is a real slot index.
            if (MovementUtility.IsTraveling(movementState)) {
                return;
            }
            stationState.ActiveInterfacer = layoutState.StationSlots[movementState.CurrSlotPosition].AssignedStationInterfacer;
            stationState.Phase = StationControlPhase.AtStation;
            Log.Msg("[StationControlSystem] arrived at slot {0}; Traveling -> AtStation", movementState.CurrSlotPosition);
            Game.Events.Dispatch(GameEvents.FabStationArrived);

            using (var table = TempVarTable.Alloc())
            {
                if (stationState.ActiveInterfacer != null)
                {
                    Debug.Log("ID: " + stationState.ActiveInterfacer.Id.Source().ToLower());
                    table.Set("station", stationState.ActiveInterfacer.Id.Source().ToLower());
                    stationState.CompletionScriptHandle = ScriptUtility.Trigger(FabricationScriptTriggers.OnArriveAtStation, table);
                }
            }
        }

        // Watches for slot departure (SlotChangedThisFrame && IsTraveling). On departure, clears
        // ActiveInterfacer and transitions to Traveling. Dispatches FabStationLeft.
        // Activate handling lives in StationControlUtility.RequestActivate (called from WorldInteractSystem).
        static private void ProcessAtStation(StationControlState stationState, MovementState movementState) {
            if (!movementState.SlotChangedThisFrame) {
                return;
            }
            if (!MovementUtility.IsTraveling(movementState)) {
                return;
            }
            stationState.ActiveInterfacer = null;
            stationState.Phase = StationControlPhase.Traveling;
            Log.Msg("[StationControlSystem] left station; AtStation -> Traveling");
            Game.Events.Dispatch(GameEvents.FabStationLeft);
        }

        // Accumulates PhaseTimer; at EnterMicrogameDuration, calls EnterComplete on the interfacer,
        // resumes MicrogameMask, dispatches FabMicrogameEntered, and transitions to InMicrogame.
        static private void ProcessEnteringMicrogame(StationControlState stationState, SequenceVisualsState sequenceState, float deltaTime) {
            stationState.PhaseTimer += deltaTime;
            if (stationState.PhaseTimer >= stationState.EnterMicrogameDuration) {
                MicrogameStationInterfacerUtility.EnterComplete(stationState.ActiveInterfacer);
                GameLoop.ResumeUpdates(UpdateMasks.MicrogameMask);
                Log.Msg("[StationControlSystem] enter timer elapsed; EnteringMicrogame -> InMicrogame; MicrogameMask resumed");
                Game.Events.Dispatch(GameEvents.FabMicrogameEntered);
                stationState.Phase = StationControlPhase.InMicrogame;
                stationState.PhaseTimer = 0f;

                ScriptUtility.Trigger(FabricationScriptTriggers.OnMicrogameEntered);

                sequenceState.MoveAwayRequested = true;
            }
        }

        // Watches for MicrogameCompletedThisFrame (normal finish) or CancelRequestedThisFrame (player
        // cancel). Completion takes priority if both fire the same frame.
        //   - Completion: suspends MicrogameMask (freezes the microgame, no exit animation yet), caches
        //     the result precision, and fires the OnFabMicrogameCompleted Leaf trigger so a precision gate
        //     can decide. The verdict is resolved inline via ResolveCompletion — there is no added delay
        //     when no gate node exists or the gate answers synchronously; only a still-running gate parks
        //     the machine in ResolvingCompletion.
        //   - Cancel: exits immediately with BeginExit(false) (no precision gate), as before.
        static private void ProcessInMicrogame(StationControlState stationState) {
            if (stationState.MicrogameCompletedThisFrame) {
                GameLoop.SuspendUpdates(UpdateMasks.MicrogameMask);
                bool hasMicrogame = stationState.ActiveInterfacer != null && stationState.ActiveInterfacer.Microgame != null;
                stationState.LastMicrogamePrecision = hasMicrogame ? stationState.ActiveInterfacer.Microgame.GetResultPrecision() : 1f;
                stationState.LastRawMicrogamePrecision = hasMicrogame ? stationState.ActiveInterfacer.Microgame.GetRawResultPrecision() : 1f;
                stationState.CompletionVerdict = MicrogameExitVerdict.Pending;
                using (var table = TempVarTable.Alloc()) {
                    if (stationState.ActiveInterfacer != null) {
                        table.Set("microgame", stationState.ActiveInterfacer.Id.Source().ToLower());
                    }
                    table.Set("precision", stationState.LastMicrogamePrecision);
                    table.Set("rawPrecision", stationState.LastRawMicrogamePrecision);
                    stationState.CompletionScriptHandle = ScriptUtility.Trigger(FabricationScriptTriggers.OnMicrogameCompleted, table);
                }
                Log.Msg("[StationControlSystem] microgame completed (precision {0}); resolving precision gate", stationState.LastMicrogamePrecision);
                ResolveCompletion(stationState);
                return;
            }

            if (stationState.CancelRequestedThisFrame) {
                GameLoop.SuspendUpdates(UpdateMasks.MicrogameMask);
                MicrogameStationInterfacerUtility.BeginExit(stationState.ActiveInterfacer, stationState, false);
                Log.Msg("[StationControlSystem] microgame cancelled; InMicrogame -> ExitingMicrogame; MicrogameMask suspended");
                Game.Events.Dispatch(GameEvents.FabMicrogameCancelled);
                stationState.Phase = StationControlPhase.ExitingMicrogame;
                stationState.PhaseTimer = 0f;
            }
        }

        // Re-runs the precision gate each frame while a gate node is still running without a verdict.
        static private void ProcessResolvingCompletion(StationControlState stationState) {
            ResolveCompletion(stationState);
        }

        // Resolves the post-completion precision gate against the current verdict:
        //   - Proceed (precision met) -> commit the normal exit.
        //   - Retry (precision below threshold) -> pause in AwaitingRetry and dispatch FabMicrogameRetryRequired.
        //   - Pending: if the gate thread is no longer running, no node gated this completion (or it
        //     finished without a verdict) -> Proceed exactly as the pre-gate behavior; otherwise wait in
        //     ResolvingCompletion for the still-running gate node. Safe to call repeatedly.
        static private void ResolveCompletion(StationControlState stationState) {
            switch (stationState.CompletionVerdict) {
                case MicrogameExitVerdict.Proceed:
                    ProceedToExit(stationState);
                    break;
                case MicrogameExitVerdict.Retry:
                    stationState.Phase = StationControlPhase.AwaitingRetry;
                    stationState.PhaseTimer = 0f;
                    Log.Msg("[StationControlSystem] precision gate failed; pausing -> AwaitingRetry");
                    Game.Events.Dispatch(GameEvents.FabMicrogameRetryRequired);
                    break;
                default: // Pending
                    if (!stationState.CompletionScriptHandle.IsRunning()) {
                        ProceedToExit(stationState);
                    } else if (stationState.Phase != StationControlPhase.ResolvingCompletion) {
                        stationState.Phase = StationControlPhase.ResolvingCompletion;
                        stationState.PhaseTimer = 0f;
                    }
                    break;
            }
        }

        // Commits a passed (or ungated) completion to the normal exit: BeginExit(true) commits the
        // precision and starts the exit/process animation, MicrogamePassedThisFrame is raised so
        // SequenceSystem advances, FabMicrogameCompleted is dispatched, and the machine moves to
        // ExitingMicrogame. MicrogameMask is already suspended by the completion handler.
        static private void ProceedToExit(StationControlState stationState) {
            MicrogameStationInterfacerUtility.BeginExit(stationState.ActiveInterfacer, stationState, true);
            stationState.MicrogamePassedThisFrame = true;
            Log.Msg("[StationControlSystem] microgame accepted; -> ExitingMicrogame");
            //Game.Events.Dispatch(GameEvents.FabMicrogameCompleted);
            stationState.Phase = StationControlPhase.ExitingMicrogame;
            stationState.PhaseTimer = 0f;
        }

        // Holds the exit timer while ProcessAnimationInProgress is true. Each frame, polls the
        // active interfacer's microgame via IsProcessAnimationComplete() and clears the flag
        // when it returns true. Once the flag is clear, accumulates PhaseTimer; at
        // ExitMicrogameDuration, calls ExitComplete on the interfacer, clears ActiveInterfacer,
        // dispatches FabStationExit, and transitions to AtStation. Cancel (BeginExit(false))
        // clears the flag eagerly so this method runs the existing fixed-duration timer with no
        // animation hold.
        static private void ProcessExitingMicrogame(StationControlState stationState, float deltaTime) {
            if (stationState.ProcessAnimationInProgress) {
                if (stationState.ActiveInterfacer != null
                    && stationState.ActiveInterfacer.Microgame != null
                    && stationState.ActiveInterfacer.Microgame.IsProcessAnimationComplete()) {
                    stationState.ProcessAnimationInProgress = false;
                    Log.Msg("[StationControlSystem] process animation complete; exit timer unblocked");
                } else {
                    // Hold the exit timer behind the animation.
                    return;
                }
            }
            // External hold (e.g. CompletionRecapSystem during the step-completion recap). The
            // hold is re-armed per-frame by the holder; once they stop setting it, the timer
            // resumes on the next frame.
            if (stationState.ExitTimerExternalHold) {
                return;
            }
            stationState.PhaseTimer += deltaTime;
            if (stationState.PhaseTimer >= stationState.ExitMicrogameDuration) {
                MicrogameStationInterfacerUtility.ExitComplete(stationState.ActiveInterfacer);
                stationState.ActiveInterfacer = null;
                Log.Msg("[StationControlSystem] exit timer elapsed; ExitingMicrogame -> AtStation");

                // trigger leaf script here
                ScriptUtility.Trigger(FabricationScriptTriggers.OnMicrogameExited);

                Game.Events.Dispatch(GameEvents.FabStationExit);
                stationState.Phase = StationControlPhase.AtStation;
                stationState.PhaseTimer = 0f;
            }
        }

        // Accumulates PhaseTimer; at StunDuration, calls RobotUtility.RemoveStun, dispatches FabStunEnd,
        // and transitions to PostStunPhase.
        static private void ProcessStunned(StationControlState stationState, RobotState robotState, RobotVisualsState visualsState, float deltaTime) {
            stationState.PhaseTimer += deltaTime;
            if (stationState.PhaseTimer >= stationState.StunDuration) {
                RobotUtility.RemoveStun(robotState, visualsState);
                Log.Msg("[StationControlSystem] stun timer elapsed; Stunned -> {0}", stationState.PostStunPhase);
                Game.Events.Dispatch(GameEvents.FabStunEnd);
                stationState.Phase = stationState.PostStunPhase;
                stationState.PhaseTimer = 0f;
            }
        }
    }
}
