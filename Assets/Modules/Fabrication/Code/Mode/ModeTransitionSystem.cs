using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Movement;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.Sequence;
using SpaceFab.Fabrication.StationControl;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceFab.Fabrication {
    /// <summary>
    /// Facilitates transitions between Modes. Sets up and shuts down relevant systems.
    /// Runs on PreUpdate phase at order -10 (before the setup systems this transitions into). Currently a stub.
    /// </summary>
    public class ModeTransitionSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.PreUpdate, -10, UpdateMasks.PreAttemptMask | UpdateMasks.AttemptLeadInMask | UpdateMasks.AttemptMask | UpdateMasks.PostAttemptMask),
                new SysPermissions()
                    .ReadWriteShared<ModeState>()
                    .ReadShared<CountdownState>()
                    .ReadWriteShared<SequenceState>()
                    .ReadShared<InterruptState>()
                    .ReadWriteShared<MinigameRequestExitState>()
                    .ReadWriteShared<SequenceVisualsState>()
                    .ReadWriteShared<FabricationMinigameState>()
            );
        }

        // implements mode transition logic.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ModeState modeState,
                out CountdownState countdownState,
                out SequenceState sequenceState,
                out InterruptState interruptState
                );
            Find.State(
                out MinigameRequestExitState exitState,
                out SequenceVisualsState visualsState,
                out FabricationMinigameState fabState,
                out OnboardingLayoutState onboardState
                );
            Find.State(out StationControlState stationState);

            switch (modeState.CurrMode)
            {
                case LevelMode.PreAttempt:
                    ProcessPreAttempt(modeState, onboardState, sequenceState, visualsState, fabState);
                    break;
                case LevelMode.AttemptLeadIn:
                    ProcessAttemptLeadIn(modeState, countdownState, visualsState);
                    break;
                case LevelMode.Attempt:
                    ProcessAttempt(modeState, sequenceState, interruptState, visualsState, fabState, stationState);
                    break;
                case LevelMode.PostAttempt:
                    ProcessPostAttempt(modeState, sequenceState, interruptState, exitState, visualsState, fabState);
                    break;
                default:
                    break;
            }
        }

        #region Helpers

        static private void ProcessPreAttempt(ModeState modeState, OnboardingLayoutState onboardState, SequenceState sequenceState, SequenceVisualsState visualsState, FabricationMinigameState fabState)
        {
            // TODO: poll for move into Attempt (keyboard or button)
            if (Input.GetKeyDown(FabricationConsts.Activate) || onboardState.IsGeneratePressed)
            {
                OnboardingStateUtility.HideGenerateButton();
                Log.Msg("[ModeTransitionSystem] received input for new wafer -- entering Attempt Mode");
                TransitionToAttemptLeadIn(modeState, sequenceState, visualsState, fabState);
            }
        }

        static private void ProcessAttemptLeadIn(ModeState modeState, CountdownState countdownState, SequenceVisualsState visualsState)
        {
            // poll for leadin countdown completed
            if (countdownState.CountdownCompletedThisFrame)
            {
                // start timer
                Log.Msg("[ModeTransitionSystem] countdown completed. Moving to Attempt Mode");
                SpacefabGame.Events.Dispatch(GameEvents.FabTimeStart);

                // update robot visual
                RobotVisualsUtility.UpdateLayer(Find.State<RobotVisualsState>());
                MicrogameCanvasUtility.HideStationInstructions(Find.State<MicrogameCanvasState>());

                // show the layer instruction panel (rows were built during the lead-in reset)
                if (visualsState.LayerInstructionPanel != null) {
                    visualsState.LayerInstructionPanel.Show();
                }

                //trigger leaf script
                ScriptUtility.Trigger(FabricationScriptTriggers.OnAttempt);

                ModeUtility.SetNewMode(modeState, LevelMode.Attempt);
                GameLoop.SuspendUpdates(UpdateMasks.AttemptLeadInMask);
                GameLoop.ResumeUpdates(UpdateMasks.AttemptMask);
            }
        }

        static private void ProcessAttempt(ModeState modeState, SequenceState sequenceState, InterruptState interruptState, SequenceVisualsState visualsState, FabricationMinigameState fabState, StationControlState stationState)
        {
            // TODO: poll for move into post-attempt
            if (sequenceState.Status == SequenceStatus.Completed && stationState.Phase == StationControlPhase.AtStation) // extra wait for final process step
            {
                Log.Msg("[ModeTransitionSystem] Attempt completed. Moving to PostAttempt Mode");
                ModeUtility.SetNewMode(modeState, LevelMode.PostAttempt);
                Debug.Log("Fab minigame completed");
                SpacefabGame.Events.Dispatch(GameEvents.FabCompleted);

                GameLoop.SuspendUpdates(UpdateMasks.AttemptMask);
                GameLoop.ResumeUpdates(UpdateMasks.PostAttemptMask);
            }

            // TODO: poll for reset triggers
            if (interruptState.ResetRequestedThisFrame)
            {
                Log.Msg("[ModeTransitionSystem] Attempt reset. Moving to AttemptLeadIn Mode");
                ResetUtility.ResetAttempt();
                TransitionToAttemptLeadIn(modeState, sequenceState, visualsState, fabState);
            }

            // Checkpoint restore stays in Attempt mode (no countdown lead-in). Roll the sequence
            // back to the post-checkpoint step and flag the visuals layer to rebuild the cards
            // around the new CurrentStepIndex so the player sees the right "first step".
            if (interruptState.RestoreCheckpointRequestedThisFrame)
            {
                Log.Msg("[ModeTransitionSystem] Attempt reset to checkpoint. Staying in Attempt Mode");
                if (sequenceState.HasCheckpoint)
                {
                    SequenceUtility.RestoreCheckpoint(sequenceState);
                    visualsState.ResetRequested = true;
                }
            }
        }

        static private void ProcessPostAttempt(ModeState modeState, SequenceState sequenceState, InterruptState interruptState, MinigameRequestExitState exitState, SequenceVisualsState visualsState, FabricationMinigameState fabState)
        {
            // poll for reset triggers
            if (interruptState.ResetRequestedThisFrame)
            {
                Log.Msg("[ModeTransitionSystem] PostAttempt completed. Resetting to AttemptLeadIn Mode");
                ResetUtility.ResetAttempt();
                TransitionToAttemptLeadIn(modeState, sequenceState, visualsState, fabState);
            }

            // poll for finalize triggers
            if (interruptState.FinalizeAttemptRequestedThisFrame)
            {
                Log.Msg("[ModeTransitionSystem] PostAttempt completed. Finalizing and exiting level");
                // Continue (Finalize) on the results screen is the one moment the Fabrication
                // attempt is treated as a valid solution. The flag is propagated to
                // FabricationSaveState by FabricationStateUtility.ExportState on minigame exit.
                fabState.MarkFoundValidSolution();
                exitState.ExitRequestState = RequestState.Confirmed;
            }
        }

        // Moves into AttemptLeadIn mode (suspending PreAttemptMask, resuming AttemptLeadInMask),
        // arms the sequence (Idle -> Active so microgame-completion handlers stop bailing), and
        // flags the sequence visuals to rebuild around the current CurrentStepIndex. The cards
        // start hidden; this is where they first appear. Step-index resolution is delegated:
        // ResetSequence sets it to 0 for fresh/reset starts; for the checkpoint flow callers set
        // it to checkpoint+1 via RestoreCheckpoint before transitioning. RebuildAllCards reads
        // CurrentStepIndex when it consumes the ResetRequested flag.
        static private void TransitionToAttemptLeadIn(ModeState modeState, SequenceState sequenceState, SequenceVisualsState visualsState, FabricationMinigameState fabState)
        {
            // Move into AttemptLeadIn Mode -- will generate wafer, countdown to start
            ModeUtility.SetNewMode(modeState, LevelMode.AttemptLeadIn);
            GameLoop.SuspendUpdates(UpdateMasks.PostAttemptMask);
            GameLoop.SuspendUpdates(UpdateMasks.AttemptMask);
            GameLoop.SuspendUpdates(UpdateMasks.PreAttemptMask);
            GameLoop.ResumeUpdates(UpdateMasks.AttemptLeadInMask);

            // Arm the sequence. Status defaults to Idle on register and is only otherwise set by
            // ResetSequence / AdvanceStep / RestoreCheckpoint. Lead-in is the moment the player
            // starts working, so this is where we promote Idle to Active. Halted / Completed also
            // get reset here so a reset-from-PostAttempt re-arms cleanly.
            if (sequenceState.Status != SequenceStatus.Restoring) {
                sequenceState.Status = SequenceStatus.Active;
            }

            // Beginning a new attempt invalidates any prior "valid solution" verdict. The flag
            // only re-flips true if the player presses Continue on the results screen.
            fabState.ClearFoundValidSolution();

            visualsState.ResetRequested = true;
        }

        #endregion // Helpers
    }
}
