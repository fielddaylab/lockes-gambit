using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    public enum ResistMicrogamePhase
    {
        Idle,       // awaiting start
        Entering,   // entering microgame
        Sweeping,   // sweep dropper back and forth
        Spreading,  // spread after player places resist
        Exiting     // cleanup
    }


    /// <summary>
    /// Holds in-flight data for the Photoresist ("Spin-Coat") microgame: the dropper's sweep
    /// position, the recorded drop offset from center, and lifecycle flags consumed by
    /// ResistMicrogameSystem.
    /// </summary>
    public class ResistMicrogameState : SharedStateComponent, IRegistrationCallbacks
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // ResistMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;

        // current x position of dropper along sweep. Updated each fixedupdate by ResistMicrogameSystem
        [NonSerialized] public float SweeperX;

        // x position where player dropped resist.
        [NonSerialized] public float DropX;

        // inspector controls for sweeper movement over chip
        public float CenterX, MaxOffset, SweepSpeed;

        // true after EnterComplete fires
        [NonSerialized] public bool InputAccepted;

        // 2D sprites
        public GameObject ResistUI;
        // moving sweeper anchor over the chip
        public Transform SweeperAnchor;
        // circle spreading over chip
        public Transform SpreadingGraphic;

        public ResistMicrogamePhase Phase;
        // time to animate spread, lower is slower
        public float SpreadingSpeed;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            // Disable UI on start
            ResistUI.SetActive(false);
            SpreadingGraphic.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Paired utility for ResistMicrogameState. Drives the Photoresist microgame's lifecycle hooks
    /// invoked from ResistMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class ResistMicrogameUtility
    {
        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.RESIST_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(out ResistMicrogameState state);
            state.IsActive = true;
            // TODO: play intro; spawn dropper; begin left-right sweep.
            state.ResistUI.SetActive(true);
            state.SpreadingGraphic.gameObject.SetActive(false);

            // init and show game UI
            state.Phase = ResistMicrogamePhase.Entering;

            // start playing intro, if applicable
        }

        public static void EnterComplete()
        {
            Find.State(out ResistMicrogameState state);

            // TODO: spawn dropper; begin left-right sweep.
            // TODO: start accepting Activate-press input.
            state.InputAccepted = true;
            state.Phase = ResistMicrogamePhase.Sweeping;
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // Also hides the microgame UI here (rather than at ExitComplete) so the step-completion
        // recap doesn't play over the still-visible resist (spin-coat) panel.
        // On cancel, nothing is recorded and UI hide is deferred to ExitComplete (existing flow).
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(out ResistMicrogameState state, out MicrogameCanvasState canvasState);
            state.Phase = ResistMicrogamePhase.Exiting;

            if (!completedNormally) { return; }

            state.SpreadingGraphic.transform.localScale = Vector3.zero;
            MicrogameUtility.CommitStepPrecision(ComputePrecision());

            state.ResistUI.SetActive(false);
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
            Find.State(out ResistMicrogameState state);
            state.IsActive = false;
            state.Phase = ResistMicrogamePhase.Idle;
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Signed form of the precision formula (no Abs/Clamp): < 1 means the drop landed right of center,
        // > 1 means left of center. Mirrors ComputePrecision's error term.
        public static float GetRawResultPrecision()
        {
            Find.State(out ResistMicrogameState state);
            return 1f - ((state.DropX - state.CenterX) / state.MaxOffset);
        }

        // Spin-Coat-specific precision math: distance between drop position and wafer center.
        private static float ComputePrecision()
        {
            Find.State(out ResistMicrogameState state);

            float precision = 1f - (Mathf.Abs(state.DropX - state.CenterX) / state.MaxOffset);
            precision = Mathf.Clamp(precision, 0f, 1f);

            return precision;
        }
    }
}
