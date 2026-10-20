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
    public enum EtchMicrogamePhase
    {
        Idle,
        Entering,
        Active,
        Exiting
    }

    /// <summary>
    /// Holds in-flight data for the Plasma Etcher ("Etch-a-sketch") microgame: the beam's
    /// position over the pattern, the per-cell correct/incorrect tally, and lifecycle flags
    /// consumed by EtchMicrogameSystem.
    /// </summary>
    public class EtchMicrogameState : SharedStateComponent
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // EtchMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;
        [NonSerialized] public bool InputAccepted;
        public GameObject EtchUI;
        public EtchMicrogamePhase Phase;

        [NonSerialized] public EtchPatternData Pattern;

        public LineRenderer PlayerBeam;
        [NonSerialized] public List<Vector2> PlayerPoints = new();
        [NonSerialized] public List<Vector2> CachedPreviewPoints;
        [NonSerialized] public Vector2 Direction;

        [NonSerialized] public int PreviewVisibleCount;
        [NonSerialized] public float PreviewProgress;
    }

    /// <summary>
    /// Paired utility for EtchMicrogameState. Drives the Plasma Etcher microgame's lifecycle hooks
    /// invoked from EtchMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class EtchMicrogameUtility
    {
        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.ETCH_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(out EtchMicrogameState state, out SequenceState sequence);
            if (state.Pattern != null)
            {
                GameObject.Destroy(state.Pattern.gameObject);
                state.Pattern = null;
            }   

            // Setup pattern
            // int patternIndex = sequence.Level.PatternIndex;
            int patternIndex = sequence.Level.GetPatternIndex(sequence);

            Find.GlobalAsset(out MicrogameStationConfig config);
            state.Pattern = GameObject.Instantiate(config.EtchPatterns[patternIndex], state.EtchUI.transform).GetComponent<EtchPatternData>();
            
            // Cache preview points
            state.CachedPreviewPoints = new();
            for (int i = 0; i < state.Pattern.PreviewBeam.positionCount; i++)
            {
                state.CachedPreviewPoints.Add(state.Pattern.PreviewBeam.GetPosition(i));
            }

            state.Phase = EtchMicrogamePhase.Entering;
            state.IsActive = true;
            state.InputAccepted = false;
            state.EtchUI.SetActive(true);

            state.Direction = Vector2.right;
  
            state.PreviewProgress = 0f;
            state.PreviewVisibleCount = 0;
            state.Pattern.PreviewBeam.positionCount = 0;
            
            state.PlayerPoints.Clear();
            state.PlayerBeam.positionCount = 0;
        }

        public static void EnterComplete()
        {
            Find.State(out EtchMicrogameState state);

            // state.Phase = EtchMicrogamePhase.Active;
            state.InputAccepted = true;

            if (state.PreviewVisibleCount >= state.CachedPreviewPoints.Count)
            {
                state.Phase = EtchMicrogamePhase.Active;
            }

            Vector2 start = state.CachedPreviewPoints[0];
            state.PlayerPoints.Add(start);
            state.PlayerBeam.positionCount = 1;
            state.PlayerBeam.SetPosition(0, start);
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // Also hides the microgame UI here (rather than at ExitComplete) so the step-completion
        // recap doesn't play over the still-visible etch panel.
        // On cancel, nothing is recorded and UI hide is deferred to ExitComplete (existing flow).
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(out EtchMicrogameState state, out MicrogameCanvasState canvasState);

            state.Phase = EtchMicrogamePhase.Exiting;
            if (!completedNormally) { return; }

            MicrogameUtility.CommitStepPrecision(ComputePrecision());

            GameObject.Destroy(state.Pattern.gameObject);
            state.Pattern = null;

            state.EtchUI.SetActive(false);
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
            Find.State(out EtchMicrogameState state);
            state.IsActive = false;
            state.Phase = EtchMicrogamePhase.Idle;

            state.PlayerPoints.Clear();
            state.PlayerBeam.positionCount = 0;

            if (state.Pattern != null)
            {
                GameObject.Destroy(state.Pattern.gameObject);
                state.Pattern = null;
            }
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Etch error is unsigned (distance from the target path), so raw equals the gate precision.
        public static float GetRawResultPrecision()
        {
            Find.State(out EtchMicrogameState state);

            if (state.PlayerPoints.Count == 0) { return 0f; }

            float playerToPreview = AverageDistance(state.PlayerPoints, state.Pattern.PreviewBeam);
            float previewToPlayer = AverageDistance(state.CachedPreviewPoints, state.PlayerBeam);
            float previewToPlayerWeight = 0.7f; // weight on missing target points is higher than extra points off-target

            return playerToPreview * (1 - previewToPlayerWeight) + previewToPlayer * previewToPlayerWeight;
        }

        // Etch-a-sketch-specific precision math: fraction of target-pattern cells the beam
        // correctly traversed, minus cells incorrectly traversed. Scaffold returns 0.
        private static float ComputePrecision()
        {
            Find.State(out EtchMicrogameState state);

            if (state.PlayerPoints.Count == 0) { return 0f; }

            float playerToPreview = AverageDistance(state.PlayerPoints, state.Pattern.PreviewBeam);
            float previewToPlayer = AverageDistance(state.CachedPreviewPoints, state.PlayerBeam);
            float previewToPlayerWeight = 0.7f; // weight on missing target points is higher than extra points off-target

            float totalError = (playerToPreview * (1 - previewToPlayerWeight) + previewToPlayer * previewToPlayerWeight) * 0.5f;
            float precision = 1f - totalError;

            return Mathf.Clamp01(precision);
        }

        private static float AverageDistance(List<Vector2> points, LineRenderer targetLine)
        {
            if (points.Count == 0)
                return 1f;

            float total = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                total += DistanceToPreview(points[i], targetLine);
            }

            return total / points.Count;
        }

        private static float DistanceToPreview(Vector2 point, LineRenderer line)
        {
            int count = line.positionCount;
            if (count <= 0)
                return 0f;

            if (count == 1)
                return Vector2.Distance(point, line.GetPosition(0));

            float bestSqr = float.MaxValue;
            Vector2 prev = line.GetPosition(0);

            for (int i = 1; i < count; i++)
            {
                Vector2 next = line.GetPosition(i);
                float sqr = PointToSegmentDistanceSqr(point, prev, next);
                if (sqr < bestSqr)
                    bestSqr = sqr;
                prev = next;
            }

            return Mathf.Sqrt(bestSqr);
        }

        private static float PointToSegmentDistanceSqr(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abSqr = Vector2.Dot(ab, ab);

            if (abSqr <= 0.000001f)
                return (p - a).sqrMagnitude;

            float t = Vector2.Dot(p - a, ab) / abSqr;
            t = Mathf.Clamp01(t);

            Vector2 projection = a + t * ab;
            return (p - projection).sqrMagnitude;
        }
    }
}
