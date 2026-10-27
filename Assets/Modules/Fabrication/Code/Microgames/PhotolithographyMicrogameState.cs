using BeauUtil.Debugger;
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
    public enum PhotolithographyMicrogamePhase
    {
        Idle,
        Entering,
        Active,
        Exiting
    }

    /// <summary>
    /// Holds in-flight data for the Photolithography ("Mask Drop") microgame: the photomask's
    /// current rotation and fall state, the target landing orientation, and lifecycle flags
    /// consumed by PhotolithographyMicrogameSystem.
    /// </summary>
    public class PhotolithographyMicrogameState : SharedStateComponent
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // PhotolithographyMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;
        public GameObject PhotolithographyUI;
        public PhotolithographyMicrogamePhase Phase;

        public GameObject Photomask;
        public SpriteRenderer PhotomaskSprite;
        public SpriteRenderer OutlineSprite;

        public float PhotomaskAngle;
        public float PhotomaskY;
        public float FallSpeed;

        [NonSerialized] public bool InputAccepted;
    }

    /// <summary>
    /// Paired utility for PhotolithographyMicrogameState. Drives the Photolithography microgame's
    /// lifecycle hooks invoked from PhotolithographyMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class PhotolithographyMicrogameUtility
    {
        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.PHOTOLITHOGRAPHY_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(out PhotolithographyMicrogameState state, out SequenceState sequence);

            // Set pattern sprites
            // int patternIndex = sequence.Level.PatternIndex;
            int patternIndex = sequence.Level.GetPatternIndex(sequence);

            MicrogameStationConfig stationConfig = MicrogameUtility.GetStationConfig();

            state.PhotomaskSprite.sprite = stationConfig.PhotolithographyMasks[patternIndex];
            state.OutlineSprite.sprite = stationConfig.PhotolithographyOutlines[patternIndex];

            // Set angle by chunk type
            switch (sequence.Level.Sequence.Steps[sequence.CurrentStepIndex].Chunk)
            {
                case SequenceChunk.Metal:
                    state.PhotomaskAngle = 120f;
                    break;
                case SequenceChunk.P:
                    state.PhotomaskAngle = 150f;
                    break;
                case SequenceChunk.N:
                    state.PhotomaskAngle = 200f;
                    break;
            }
            state.Photomask.transform.rotation = Quaternion.Euler(0f, 0f, state.PhotomaskAngle);
            state.PhotomaskY = 2.75f;

            state.Phase = PhotolithographyMicrogamePhase.Entering;
            state.IsActive = true;
            state.InputAccepted = false;
            state.PhotolithographyUI.SetActive(true);
        }

        public static void EnterComplete()
        {
            Find.State(out PhotolithographyMicrogameState state);

            state.Phase = PhotolithographyMicrogamePhase.Active;
            state.InputAccepted = true;
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // Also hides the microgame UI here (rather than at ExitComplete) so the step-completion
        // recap doesn't play over the still-visible photolithography panel.
        // On cancel, nothing is recorded and UI hide is deferred to ExitComplete (existing flow).
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(out PhotolithographyMicrogameState state, out MicrogameCanvasState canvasState);
            state.Phase = PhotolithographyMicrogamePhase.Exiting;
            if (!completedNormally) { return; }

            MicrogameUtility.CommitStepPrecision(ComputePrecision());

            state.PhotolithographyUI.SetActive(false);
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
            Find.State(out PhotolithographyMicrogameState state);
            state.IsActive = false;
            state.Phase = PhotolithographyMicrogamePhase.Idle;
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Signed form of the precision formula (no Abs/Clamp): < 1 means the mask landed clockwise of the
        // target angle, > 1 counter-clockwise. Mirrors ComputePrecision's signed angle delta.
        public static float GetRawResultPrecision()
        {
            Find.State(out PhotolithographyMicrogameState state);
            return 1f - (Mathf.DeltaAngle(state.PhotomaskAngle, 0f) / 180f);
        }

        // Mask-Drop-specific precision math: angle delta from target orientation at landing.
        // Scaffold returns 0.
        private static float ComputePrecision()
        {
            Find.State(out PhotolithographyMicrogameState state);

            float targetAngle = 0f;
            float delta = Mathf.Abs(Mathf.DeltaAngle(state.PhotomaskAngle, targetAngle));
            float precision = 1f - (delta / 180f);

            return Mathf.Clamp01(precision);
        }
    }
}
