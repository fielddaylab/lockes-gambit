using FieldDay;
using FieldDay.Systems;
using SpaceFab.Fabrication.StationControl;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// Consumes the recap-request flag set by SequenceUtility.AdvanceStep, plays the
    /// CompletionRecapUtility.PlayRecap coroutine, and gates the StationControl exit timer behind
    /// it via StationControlState.ExitTimerExternalHold. Runs on Update at order 5 (before
    /// StationControlSystem at order 10) so the per-frame hold is observed in the same frame.
    /// </summary>
    public class CompletionRecapSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 8, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<CompletionRecapState>()
                    .ReadShared<SequenceState>()
                    .ReadWriteShared<StationControlState>()
                    .ReadWriteShared<SequenceVisualsState>()
            );
        }

        // Two responsibilities, polled each frame:
        //   1. If a recap was just requested, start the PlayRecap routine.
        //   2. While the routine is running, re-arm StationControlState.ExitTimerExternalHold so
        //      the station-control exit timer stays parked. When the routine ends, hand off the
        //      top-panel transition by setting AdvanceRequested or CompletionRequested on
        //      SequenceVisualsState.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out CompletionRecapState recapState,
                out SequenceState sequenceState,
                out StationControlState stationState,
                out SequenceVisualsState visualsState
                );

            // 1. Start the recap if AdvanceStep raised the flag this frame. If the completed step
            //    moves the sequence to a new chunk, the layer instruction panel plays instead.
            if (recapState.RecapRequested)
            {
                recapState.RecapRequested = false;
                recapState.RecapInProgress = true;

                int justCompleted = recapState.RecapJustCompletedIndex;
                FabricationStep[] steps = sequenceState.Level != null ? sequenceState.Level.Sequence.Steps : null;
                var layerPanel = visualsState.LayerInstructionPanel;
                if (layerPanel != null && layerPanel.IsChunkChange(steps, justCompleted))
                {
                    recapState.RecapRoutine.Replace(layerPanel.ChunkChangeRoutine(steps, justCompleted));
                }
                else
                {
                    recapState.RecapRoutine.Replace(CompletionRecapUtility.PlayRecap(recapState, sequenceState, justCompleted));
                }
            }

            // 2. While the routine is alive, hold the exit timer. When the routine finishes,
            //    release the hold and hand off to the existing top-panel transition.
            if (recapState.RecapInProgress)
            {
                if (recapState.RecapRoutine.Exists())
                {
                    stationState.ExitTimerExternalHold = true;
                }
                else
                {
                    recapState.RecapInProgress = false;
                    stationState.ExitTimerExternalHold = false;
                    HandOffToTopPanel(recapState, sequenceState, visualsState);
                }
            }
        }

        // Fires the deferred top-panel swap. If the next step would be past the last, completion
        // routine plays (both top cards fade out); otherwise the normal swap routine plays. The
        // SequenceVisualsSystem in LateUpdate consumes whichever flag is set.
        static private void HandOffToTopPanel(CompletionRecapState recapState, SequenceState sequenceState, SequenceVisualsState visualsState)
        {
            int stepsLength = sequenceState.Level != null && sequenceState.Level.Sequence.Steps != null ? sequenceState.Level.Sequence.Steps.Length : 0;
            if (recapState.RecapJustCompletedIndex + 1 >= stepsLength)
            {
                visualsState.CompletionRequested = true;
            }
            else
            {
                visualsState.AdvanceRequested = true;
            }
        }
    }
}
