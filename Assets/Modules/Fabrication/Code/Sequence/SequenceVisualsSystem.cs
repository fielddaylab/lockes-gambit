using BeauRoutine;
using FieldDay;
using FieldDay.Systems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// Polls SequenceVisualsState for reset / advance / completion signals and drives the on-screen
    /// step cards through SequenceVisualsUtility. Routine collisions resolve via Routine.Replace,
    /// so a new advance interrupts and supersedes any in-flight stamp-and-swipe. Runs on LateUpdate
    /// at order 0 under AttemptMask, after SequenceSystem (Update, order 15) has set the flags.
    /// </summary>
    public class SequenceVisualsSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 0, UpdateMasks.AttemptLeadInMask | UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<SequenceVisualsState>()
                    .ReadShared<SequenceState>()
            );
        }

        // Consumes the dirty flags set by SequenceUtility this frame. Reset takes priority over
        // advance and completion (since a reset invalidates any pending mid-routine animation).
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out SequenceVisualsState visualsState,
                out SequenceState sequenceState
                );

            // Reset wins: rebuild both cards and clear all pending flags.
            if (visualsState.ResetRequested) {
                SequenceVisualsUtility.RebuildAllCards(visualsState, sequenceState);
                if (visualsState.LayerInstructionPanel != null) {
                    FabricationSequence sequence = sequenceState.Level != null ? sequenceState.Level.Sequence : null;
                    visualsState.LayerInstructionPanel.Initialize(sequence, sequenceState.CurrentStepIndex);
                }
                visualsState.ResetRequested = false;
                visualsState.AdvanceRequested = false;
                visualsState.CompletionRequested = false;
                return;
            }

            // Non-final step advance: stamp + swipe + pre-load the new next card.
            if (visualsState.AdvanceRequested) {
                // SequenceUtility.AdvanceStep has already incremented CurrentStepIndex, so the step
                // that just completed is at CurrentStepIndex - 1.
                int justCompletedIndex = sequenceState.CurrentStepIndex - 1;
                visualsState.TransitionRoutine.Replace(SequenceVisualsUtility.AdvanceRoutine(visualsState, sequenceState, justCompletedIndex));
                visualsState.AdvanceRequested = false;
            }

            if (visualsState.MoveAwayRequested)
            {
                visualsState.TransitionRoutine.Replace(SequenceVisualsUtility.MoveOffscreenRoutine(visualsState));
                visualsState.MoveAwayRequested = false;
            }

            // Final step completion: stamp + swipe, no promotion.
            if (visualsState.CompletionRequested) {
                visualsState.TransitionRoutine.Replace(SequenceVisualsUtility.CompletionRoutine(visualsState));
                visualsState.CompletionRequested = false;
            }
        }
    }
}
