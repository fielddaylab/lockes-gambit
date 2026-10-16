using System;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Sequence;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    public enum IonMicrogamePhase
    {
        Idle,
        Entering,
        Filling,
        Exiting
    }

    /// <summary>
    /// Holds in-flight data for the Ion Implanter microgame, and lifecycle flags consumed by
    /// IonMicrogameSystem. Mechanics are not yet specified.
    /// </summary>
    public class IonMicrogameState : SharedStateComponent, IRegistrationCallbacks
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // IonMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;
        [NonSerialized] public bool InputAccepted;
        
        public GameObject IonUI;
        public Transform PatternParent;
        public Transform DropperAnchor;

        public float PointDensity = 10;
        public float FillRadius = 3;
        [NonSerialized] public IonPatternData IonPattern;

        public IonMicrogamePhase Phase;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            // Disable UI on start
            IonUI.SetActive(false);
        }
    }

    /// <summary>
    /// Paired utility for IonMicrogameState. Drives the Ion Implanter microgame's lifecycle hooks
    /// invoked from IonMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class IonMicrogameUtility
    {
        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.ION_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(
                out IonMicrogameState state,
                out MicrogameCanvasState canvasState,
                out SequenceState sequence);

            state.IsActive = true;
            
            // setup UI
            state.IonUI.SetActive(true);
            MicrogameCanvasUtility.ShowStationInstructions(canvasState, FabricationConsts.ION_STATION_ID);
            HintedCursor.Visibility = HintedCursor.VisiblityMode.Invisible;

            // setup IonPoints
            // int patternIndex = sequence.Level.PatternIndex;
            int patternIndex = sequence.Level.GetPatternIndex(sequence);
            MicrogameStationConfig stationConfig = MicrogameUtility.GetStationConfig();

            state.IonPattern = GameObject.Instantiate(stationConfig.IonPatterns[patternIndex], state.PatternParent).GetComponent<IonPatternData>();

            state.IonPattern.SetupRenderers(state.PointDensity, state.FillRadius);
            state.Phase = IonMicrogamePhase.Entering;
        }

        public static void EnterComplete()
        {
            // TODO: start accepting input.
            Find.State(out IonMicrogameState state);
            state.InputAccepted = true;
            state.Phase = IonMicrogamePhase.Filling;
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // Also hides the microgame UI here (rather than at ExitComplete) so the step-completion
        // recap doesn't play over the still-visible ion-implant panel.
        // On cancel, nothing is recorded and UI hide is deferred to ExitComplete (existing flow).
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(out IonMicrogameState state, out MicrogameCanvasState canvasState);
            state.Phase = IonMicrogamePhase.Exiting;

            // TODO: freeze dropper.
            if (!completedNormally) { return; }

            GameObject.Destroy(state.IonPattern.gameObject);

            HintedCursor.Visibility = HintedCursor.VisiblityMode.Always;
            MicrogameUtility.CommitStepPrecision(ComputePrecision());

            state.IonUI.SetActive(false);
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
            Find.State(out IonMicrogameState state);
            state.IsActive = false;
            state.Phase = IonMicrogamePhase.Idle;
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Ion error is unsigned, so raw equals the gate precision.
        public static float GetRawResultPrecision()
        {
            return ComputePrecision();
        }

        // Scaffold returns 0 until precision math is defined.
        private static float ComputePrecision()
        {
            // This microgame should not exit until the pattern is completely filled
            // in that sense precision should always be one, but this can be reviewed later
            return 1f;
        }
    }
}
