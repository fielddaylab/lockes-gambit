using FieldDay;
using FieldDay.Audio;
using FieldDay.SharedState;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    public enum FurnaceMicrogamePhase
    {
        Idle,       // awaiting start
        Entering,   // entering microgame
        Active,     // accepting input, simulating heat
        Exiting     // cleanup
    }
    
    /// <summary>
    /// Holds in-flight data for the Furnace ("Thermometer") microgame: the current heat value,
    /// the target range for the active step's process, and lifecycle flags consumed by
    /// FurnaceMicrogameSystem.
    /// </summary>
    public class FurnaceMicrogameState : SharedStateComponent, IRegistrationCallbacks
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // FurnaceMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;

        // TODO: current heat value, target range center + half-width (varies per process: Oxidation /
        // N-Type Doping / P-Type Doping).
        // TargetRange determins position, TargetHalfWidth determines width for precision, MaxRange determines total meter range, Sensitivity determines rate heat increases, lower slower
        public float TargetRange, TargetHalfWidth, MaxRange, Sensitivity;
        [NonSerialized] public float CurrentValue;
        [NonSerialized] public float FinalHeat;

        [NonSerialized] public bool InputAccepted;
        [NonSerialized] public bool isSpacebarHeld;
        [NonSerialized] public bool IncreasingHeat;

        // 2d sprites
        public GameObject FurnaceUI;
        // indicators for internal values, rotate to show along meter
        public Transform TargetRangeAnchor, TargetArrowAnchor, MeterArrowAnchor;

        public FurnaceMicrogamePhase Phase;

        [NonSerialized] public AudioHandle HeatSfx;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            // Disable UI on start
            FurnaceUI.SetActive(false);
        }
    }

    /// <summary>
    /// Paired utility for FurnaceMicrogameState. Drives the Furnace microgame's lifecycle hooks
    /// invoked from FurnaceMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class FurnaceMicrogameUtility
    {
        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.FURNACE_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(out FurnaceMicrogameState state, out SequenceState sequence);
            state.IsActive = true;
            
            // TODO: play intro (station name flash, spawn heat dial UI).
            
            // Set up range position
            // TODO: set value for different wafer types
            switch (sequence.Level.Sequence.Steps[sequence.CurrentStepIndex].Chunk)
            {
                case SequenceChunk.Metal:
                    state.TargetRange = 5;
                    break;
                case SequenceChunk.P:
                    state.TargetRange = 3;
                    break;
                case SequenceChunk.N:
                    state.TargetRange = 9;
                    break;
            }

            float targetPercentage = state.TargetRange / state.MaxRange;
            float targetZRotation = -targetPercentage * 180;
            Vector3 targetRotation = new Vector3(0, 0, targetZRotation);
            state.TargetRangeAnchor.rotation = Quaternion.Euler(targetRotation);
            state.TargetArrowAnchor.rotation = Quaternion.Euler(targetRotation);
            state.MeterArrowAnchor.rotation = Quaternion.identity;

            // reset value
            state.CurrentValue = 0;
            state.IncreasingHeat = true;
            state.isSpacebarHeld = false;
            
            state.FurnaceUI.SetActive(true);
            state.Phase = FurnaceMicrogamePhase.Entering;
        }

        public static void EnterComplete()
        {
            Find.State(out FurnaceMicrogameState state);
            
            // start accepting Activate-hold input; begin heat simulation.
            state.InputAccepted = true;
            state.Phase = FurnaceMicrogamePhase.Active;
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // Also hides the microgame UI here (rather than at ExitComplete) so the step-completion
        // recap doesn't play over the still-visible furnace panel.
        // On cancel, nothing is recorded and UI hide is deferred to ExitComplete (existing flow).
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(out FurnaceMicrogameState state, out MicrogameCanvasState canvasState);

            // freeze heat simulation
            state.Phase = FurnaceMicrogamePhase.Exiting;

            if (!completedNormally) { return; }

            MicrogameUtility.CommitStepPrecision(ComputePrecision());

            state.FurnaceUI.SetActive(false);
            MicrogameCanvasUtility.HideStationInstructions(canvasState);
        }

        // TODO: track process animation state (parallel or sequential) and return true once the
        // animation has finished playing. Scaffold returns true so the exit gate doesn't stall
        // before per-microgame animations are authored.
        public static bool IsProcessAnimationComplete()
        {
            return true;
        }

        public static void ExitComplete()
        {
            Find.State(out FurnaceMicrogameState state);
            state.IsActive = false;
            state.MeterArrowAnchor.rotation = Quaternion.identity;
            state.Phase = FurnaceMicrogamePhase.Idle;
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Signed form of the precision formula (no Abs/Clamp): < 1 means FinalHeat overshot the target
        // (too hot), > 1 means it undershot (too cold). Mirrors ComputePrecision's error term.
        public static float GetRawResultPrecision()
        {
            Find.State(out FurnaceMicrogameState state);
            return 1f - ((state.FinalHeat - state.TargetRange) / state.MaxRange);
        }

        // Furnace-specific precision math: difference between final heat value and the target
        // range center. Scaffold returns 0.
        private static float ComputePrecision()
        {
            Find.State(out FurnaceMicrogameState state);

            float precision = 1f - (Mathf.Abs(state.FinalHeat - state.TargetRange) / state.MaxRange);
            precision = Mathf.Clamp(precision, 0f, 1f);

            return precision;
        }
    }
}
