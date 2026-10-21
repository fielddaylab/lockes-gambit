using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using Leaf.Runtime;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.Sequence;
using SpaceFab.Fabrication.Stations;
using System;

namespace SpaceFab.Fabrication.StationControl {
    /// <summary>
    /// Phase of the station-control state machine. Governs what input is accepted each frame.
    /// </summary>
    public enum StationControlPhase {
        Traveling,          // Robot moving between slots; movement live, Activate ignored.
        AtStation,          // Robot parked at a slot; movement live, Activate accepted.
        EnteringMicrogame,  // Intro animation running; all input blocked.
        InMicrogame,        // Microgame owns input; Cancel accepted at any sub-phase.
        ResolvingCompletion,// Microgame finished; awaiting the Leaf precision gate's verdict. Only entered
                            // when a gate node is still running (the no-gate / synchronous cases resolve
                            // inline). All input blocked; no exit animation has started yet.
        AwaitingRetry,      // Gate verdict was Retry (below precision); microgame frozen with the restart
                            // panel up. All input blocked until RestartMicrogame.
        ExitingMicrogame,   // Outro animation running; all input blocked.
        Stunned,            // Wrong-station penalty; all input blocked; returns to PostStunPhase when done.
    }

    /// <summary>
    /// Outcome of the post-completion Leaf precision gate. Pending until a gate node (or the no-gate
    /// fallback) resolves it.
    /// </summary>
    public enum MicrogameExitVerdict {
        Pending,    // Awaiting a verdict.
        Proceed,    // Precision met (or no gate present); exit normally.
        Retry,      // Precision below threshold; pause for restart.
    }

    /// <summary>
    /// Tracks the robot's within-Attempt interaction phase. Decides which inputs are legal each frame
    /// and flips MicrogameMask on/off when entering/leaving InMicrogame. Does not mirror slot position —
    /// consumers read MovementState directly.
    /// </summary>
    public class StationControlState : SharedStateComponent, IRegistrationCallbacks {
        [NonSerialized] public StationControlPhase Phase;

        // The station currently being interacted with. Non-null in AtStation / EnteringMicrogame /
        // InMicrogame / ExitingMicrogame; null otherwise.
        [NonSerialized] public MicrogameStationInterfacer ActiveInterfacer;

        // Accumulates wall time within timed phases (EnteringMicrogame, ExitingMicrogame, Stunned).
        public float PhaseTimer;

        // Inspector-editable global durations.
        public float EnterMicrogameDuration = 0.5f;
        public float ExitMicrogameDuration = 0.5f;
        public float StunDuration = 1.5f;
        public float CompletionHoldDuration = 0.5f;

        // One-frame flags, cleared by StationControlFlagRefreshSystem in LateUpdate.
        [NonSerialized] public bool MicrogameCompletedThisFrame;
        [NonSerialized] public bool CancelRequestedThisFrame;

        // One-frame flag raised when a completion is accepted (verdict Proceed) and the machine commits to
        // exiting. SequenceSystem advances on this rather than raw completion, so a failed precision gate
        // does not advance the sequence. Cleared by StationControlFlagRefreshSystem in LateUpdate.
        [NonSerialized] public bool MicrogamePassedThisFrame;

        // Result precision [0,1] of the just-completed microgame, cached the frame it signals completion
        // (read from IMicrogame.GetResultPrecision before OnExitBegin). Read by the Leaf precision gate.
        [NonSerialized] public float LastMicrogamePrecision;

        // Signed precision of the just-completed microgame (IMicrogame.GetRawResultPrecision): 1 = perfect,
        // < 1 = overshoot (above target), > 1 = undershoot (below target). Used to pick a direction-specific
        // retry-popup message.
        [NonSerialized] public float LastRawMicrogamePrecision;

        // Verdict of the post-completion precision gate. Reset to Pending each completion, set by the Leaf
        // RequireMicrogamePrecision member (or the no-gate fallback), consumed when resolving the exit.
        [NonSerialized] public MicrogameExitVerdict CompletionVerdict;

        // Handle to the OnFabMicrogameCompleted trigger thread, kept so ResolvingCompletion can tell a
        // still-running gate node from one that finished (or never existed) without setting a verdict.
        [NonSerialized] public LeafThreadHandle CompletionScriptHandle;

        // True while a post-microgame process animation is playing. Raised either by the
        // microgame itself via MicrogameStationInterfacerUtility.SignalProcessAnimationStarted
        // (parallel mode, during InMicrogame), or by BeginExit(true) (sequential mode, on
        // successful completion). Lowered when the microgame's IsProcessAnimationComplete()
        // returns true, when the player presses FabricationConsts.Skip during ExitingMicrogame,
        // or when BeginExit(false) is called (cancel drops any in-flight animation). While true
        // during ExitingMicrogame, PhaseTimer does NOT advance — the exit timer waits behind the
        // animation. During InMicrogame the flag is informational only; the microgame still owns
        // input.
        [NonSerialized] public bool ProcessAnimationInProgress;

        // Phase to resume after Stunned clears. Set when TriggerStun is invoked.
        [NonSerialized] public StationControlPhase PostStunPhase;

        // External "park the exit timer" flag, re-armed each frame by systems that need to hold
        // ExitingMicrogame open beyond the microgame's own ProcessAnimationInProgress contract
        // (e.g., CompletionRecapSystem while the step-completion recap is playing). Independent
        // of ProcessAnimationInProgress so StationControlSystem's clearing logic for that flag
        // doesn't fight with external holds.
        [NonSerialized] public bool ExitTimerExternalHold;

        public void OnRegister() {
            Phase = StationControlPhase.AtStation;
            ActiveInterfacer = null;
            PhaseTimer = 0f;
            MicrogameCompletedThisFrame = false;
            CancelRequestedThisFrame = false;
            MicrogamePassedThisFrame = false;
            LastMicrogamePrecision = 0f;
            LastRawMicrogamePrecision = 1f;
            CompletionVerdict = MicrogameExitVerdict.Pending;
            CompletionScriptHandle = default;
            ProcessAnimationInProgress = false;
            PostStunPhase = StationControlPhase.Traveling;
        }

        public void OnDeregister() {
        }
    }

    /// <summary>
    /// Queries and commands for StationControlState. The command methods are the Leaf integration surface
    /// (add [LeafMember] attributes when Leaf scripting is wired in).
    /// </summary>
    public static class StationControlUtility {
        // ---- Queries (consumed by MovementSystem / WorldInteractSystem for input gating) ----

        // True when movement input should be processed (Traveling or AtStation).
        public static bool AllowsMovement(StationControlState stationState) {
            return stationState.Phase == StationControlPhase.Traveling || stationState.Phase == StationControlPhase.AtStation;
        }

        // True when an Activate press should be forwarded to RequestActivate (AtStation only).
        public static bool AllowsActivate(StationControlState stationState) {
            return stationState.Phase == StationControlPhase.AtStation;
        }

        // True when a Cancel press should be forwarded to RequestCancel (InMicrogame only).
        public static bool AllowsCancel(StationControlState stationState) {
            return stationState.Phase == StationControlPhase.InMicrogame;
        }

        // True when a Skip press should be forwarded to RequestSkipProcessAnimation. Honored
        // regardless of whether the animation started in parallel or sequentially — once it's
        // blocking the exit, it's skippable.
        public static bool AllowsSkipProcessAnimation(StationControlState stationState) {
            return stationState.Phase == StationControlPhase.ExitingMicrogame && stationState.ProcessAnimationInProgress;
        }

        // True when the state machine considers a microgame to currently own input.
        public static bool IsMicrogameOwned(StationControlState stationState) {
            return stationState.Phase == StationControlPhase.InMicrogame;
        }

        // ---- Commands (Leaf-integration surface) ----

        // Called by WorldInteractSystem when the player presses Activate at a station.
        // Routes to either EnteringMicrogame (if CanActivateNow) or Stunned (otherwise).
        public static void RequestActivate(SequenceState sequenceState, StationControlState stationState, RobotState robotState, RobotVisualsState visualsState, MicrogameStationInterfacer interfacer) {
            if (stationState.Phase != StationControlPhase.AtStation) {
                Log.Msg("[StationControlUtility] RequestActivate ignored; Phase is {0}, not AtStation", stationState.Phase);
                return;
            }
            // Bad-target branch: no interfacer, no microgame component, or the microgame refuses activation right now.
            if (interfacer == null || interfacer.Microgame == null || !interfacer.Microgame.CanActivateNow()) {
                //Game.Events.Dispatch(GameEvents.FabWrongStationAttempt);
                
                SpacefabGame.Events.Dispatch(GameEvents.FabWrongStationAttempt, EvtArgs.Box((interfacer.Id.Source(), sequenceState.Level.Sequence.Steps[sequenceState.CurrentStepIndex].StepId.ToString())));

                TriggerStun(stationState, robotState, visualsState, StationControlPhase.AtStation);
                return;
            }
            stationState.ActiveInterfacer = interfacer;
            MicrogameStationInterfacerUtility.BeginEnter(interfacer);
            OnboardingStateUtility.SetEnabledGroup(Find.State<OnboardingLayoutState>().ControlsGroup, false);
            stationState.PhaseTimer = 0f;
            stationState.Phase = StationControlPhase.EnteringMicrogame;
            Log.Msg("[StationControlUtility] RequestActivate accepted; AtStation -> EnteringMicrogame");
            SpacefabGame.Events.Dispatch(GameEvents.FabStationEnterBegin, EvtArgs.Create(sequenceState.Level.Sequence.Steps[sequenceState.CurrentStepIndex].StepId.ToString()));
        }

        // Called by WorldInteractSystem when the player presses Cancel. Sets the one-frame flag;
        // ProcessInMicrogame picks it up the same frame and drives the BeginExit / phase transition.
        public static void RequestCancel(StationControlState stationState) {
            if (stationState.Phase != StationControlPhase.InMicrogame) {
                Log.Msg("[StationControlUtility] RequestCancel ignored; Phase is {0}, not InMicrogame", stationState.Phase);
                return;
            }
            stationState.CancelRequestedThisFrame = true;
            Log.Msg("[StationControlUtility] RequestCancel accepted; CancelRequestedThisFrame raised");
        }

        // Called by MicrogameStationInterfacerBridgeSystem when a microgame's CompletedThisFrame flag is observed.
        public static void NotifyMicrogameCompleted(StationControlState stationState) {
            if (stationState.Phase != StationControlPhase.InMicrogame) {
                return;
            }
            stationState.MicrogameCompletedThisFrame = true;
            Log.Msg("[StationControlUtility] microgame signaled completion; MicrogameCompletedThisFrame raised");
        }

        // Called by MicrogameStationInterfacerBridgeSystem when a microgame's ProcessAnimationStartedThisFrame
        // flag is observed. Only raises ProcessAnimationInProgress while in InMicrogame — a stray signal after
        // exit can't re-arm the flag.
        public static void NotifyProcessAnimationStarted(StationControlState stationState) {
            if (stationState.Phase != StationControlPhase.InMicrogame) {
                return;
            }
            stationState.ProcessAnimationInProgress = true;
            Log.Msg("[StationControlUtility] microgame signaled process animation start; ProcessAnimationInProgress raised");
        }

        // Called by WorldInteractSystem when the player presses Skip during ExitingMicrogame to
        // dismiss the process animation. Lowers ProcessAnimationInProgress so the exit timer can
        // resume. No-op if the animation isn't actually running.
        public static void RequestSkipProcessAnimation(StationControlState stationState) {
            if (stationState.Phase == StationControlPhase.ExitingMicrogame && stationState.ProcessAnimationInProgress) {
                stationState.ProcessAnimationInProgress = false;
                Log.Msg("[StationControlUtility] RequestSkipProcessAnimation accepted; ProcessAnimationInProgress cleared");
            }
        }

        // Sets the post-completion precision-gate verdict. Reached (indirectly, via the Leaf
        // RequireMicrogamePrecision member) in response to OnFabMicrogameCompleted. First write wins for a
        // given completion — CompletionVerdict is reset to Pending at the start of each completion, so a
        // stray call outside the resolution window (verdict already Proceed/Retry) is ignored.
        public static void SetCompletionVerdict(StationControlState stationState, MicrogameExitVerdict verdict) {
            if (verdict == MicrogameExitVerdict.Pending) {
                return;
            }
            if (stationState.CompletionVerdict != MicrogameExitVerdict.Pending) {
                return;
            }
            stationState.CompletionVerdict = verdict;
            Log.Msg("[StationControlUtility] completion verdict set to {0}", verdict);

            // On a retry verdict, raise the interrupt request so TutorialInterruptSystem opens the retry
            // popup and pauses the timer.
            if (verdict == MicrogameExitVerdict.Retry && Game.SharedState.Has<TutorialInterruptState>()) {
                Find.State<TutorialInterruptState>().TutorialInterruptRequested = true;
            }
        }

        // Restarts a microgame paused in AwaitingRetry, resetting it to a fresh active play state (as if
        // just entered, with no intro replay). Resumes MicrogameMask, re-runs the interfacer enter
        // lifecycle, and returns to InMicrogame. No-op outside AwaitingRetry.
        public static void RestartMicrogame(StationControlState stationState) {
            if (stationState.Phase != StationControlPhase.AwaitingRetry) {
                Log.Msg("[StationControlUtility] RestartMicrogame ignored; Phase is {0}, not AwaitingRetry", stationState.Phase);
                return;
            }
            GameLoop.ResumeUpdates(UpdateMasks.MicrogameMask);
            MicrogameStationInterfacerUtility.Reenter(stationState.ActiveInterfacer);
            stationState.Phase = StationControlPhase.InMicrogame;
            stationState.PhaseTimer = 0f;
            stationState.CompletionVerdict = MicrogameExitVerdict.Pending;
            Log.Msg("[StationControlUtility] RestartMicrogame; AwaitingRetry -> InMicrogame");
            Game.Events.Dispatch(GameEvents.FabMicrogameRestarted);
        }

        // Forces transition into Stunned for StunDuration seconds. After the stun, machine returns to returnPhase.
        // Applies the robot stun (idempotent via RobotUtility.ApplyStun) so callers don't need to thread that separately.
        public static void TriggerStun(StationControlState stationState, RobotState robotState, RobotVisualsState visualsState, StationControlPhase returnPhase) {
            stationState.PostStunPhase = returnPhase;
            stationState.PhaseTimer = 0f;
            stationState.Phase = StationControlPhase.Stunned;
            RobotUtility.ApplyStun(robotState, visualsState);
            OnboardingStateUtility.SetEnabledGroup(Find.State<OnboardingLayoutState>().ControlsGroup, false);
            Log.Msg("[StationControlUtility] TriggerStun; -> Stunned, PostStunPhase = {0}", returnPhase);

            ScriptUtility.Trigger(FabricationScriptTriggers.OnStunned);

            Game.Events.Dispatch(GameEvents.FabStunBegin);
        }
    }
}
