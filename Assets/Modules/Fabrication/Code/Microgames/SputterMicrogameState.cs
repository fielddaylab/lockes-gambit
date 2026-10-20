using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace SpaceFab.Fabrication.Microgames
{
    public enum SputterMicrogamePhase
    {
        Idle,
        Entering,
        Active,
        Exiting
    }

    /// <summary>
    /// Holds in-flight data for the Sputter ("Spraypaint") microgame: the sputter head's position,
    /// the fill state of the etched target area, and lifecycle flags consumed by SputterMicrogameSystem.
    /// </summary>
    public class SputterMicrogameState : SharedStateComponent, IRegistrationCallbacks
    {
        // True while this microgame owns input/simulation. Set by EnterBegin, cleared by ExitComplete.
        // SputterMicrogameSystem reads this to gate its ProcessWork.
        [NonSerialized] public bool IsActive;
        [NonSerialized] public bool InputAccepted;
        public GameObject SputterUI;
        public SputterMicrogamePhase Phase;

        public Transform SputterHeadAnchor;
        public Transform FirePoint;
        public Transform ProjectileParent;
        public LineRenderer TrajectoryPreview;

        public SputterMicrogameProjectile ProjectilePrefab;
        [NonSerialized] public SputterPatternData SputterPattern;
        public SpriteRenderer sputerHitImg;

        public bool IsTrajectoryDisplayed = false;
        public float FlashTime = 0.18f;

        public void OnRegister()
        {
            
        }

        public void OnDeregister()
        {

        }
    }

    /// <summary>
    /// Paired utility for SputterMicrogameState. Drives the Sputter microgame's lifecycle hooks
    /// invoked from SputterMicrogame (the Unity-side IMicrogame component).
    /// </summary>
    public static class SputterMicrogameUtility
    {
        // Tunables for the particle shower spawned when a pulse reflects off the target.
        private const int ShowerParticleCount = 8;
        private const float ShowerSpreadAngle = 25f;
        private const float ShowerParticleSpeed = 5f;
        private const float ShowerAngleJitter = 2f;

        // Spawns a burst of particles at the reflection point, evenly spread around the reflected
        // angle (plus a little jitter per particle so the fan doesn't look mechanically uniform).
        public static void SpawnParticleShower(Vector2 origin, float baseAngle)
        {
            Find.State(out SputterMicrogameState state);

            float step = ShowerParticleCount > 1 ? ShowerSpreadAngle / (ShowerParticleCount - 1) : 0f;
            float startAngle = baseAngle - ShowerSpreadAngle * 0.5f;

            for (int i = 0; i < ShowerParticleCount; i++)
            {
                float angle = startAngle + step * i + UnityEngine.Random.Range(-ShowerAngleJitter * 0.5f, ShowerAngleJitter * 0.5f);
                SputterMicrogameProjectile particle = GameObject.Instantiate(state.ProjectilePrefab, state.ProjectileParent);
                particle.transform.position = origin;
                particle.InitializeAsShowerParticle(angle, ShowerParticleSpeed);
            }
        }

        // determines if microgame can be started based on if this step is next
        public static bool CanActivate()
        {
            Find.State(out SequenceState state);
            return SequenceUtility.CheckNextStep(state, FabricationConsts.SPUTTER_STATION_ID);
        }

        public static void EnterBegin()
        {
            Find.State(out SputterMicrogameState state, out SequenceState sequence);

            state.SputterUI.SetActive(true);

            // Set pattern
            // int patternIndex = sequence.Level.PatternIndex;
            int patternIndex = sequence.Level.GetPatternIndex(sequence);

            Find.GlobalAsset(out MicrogameStationConfig config);
            state.SputterPattern = GameObject.Instantiate(config.SputterPatterns[patternIndex], state.SputterUI.transform).GetComponent<SputterPatternData>();
            state.SputterPattern.SetPatternData(state.ProjectilePrefab.Sprite.bounds.size.x);

            state.Phase = SputterMicrogamePhase.Entering;
            state.IsActive = true;
            state.InputAccepted = false;

            state.TrajectoryPreview.enabled = false;
            state.sputerHitImg.enabled = false;
        }

        public static void EnterComplete()
        {
            Find.State(out SputterMicrogameState state);

            state.Phase = SputterMicrogamePhase.Active;
            state.InputAccepted = true;
        }

        // On normal completion, compute precision and commit it to the wafer at the current step.
        // On cancel, nothing is recorded.
        public static void ExitBegin(bool completedNormally)
        {
            Find.State(
                out SputterMicrogameState state,
                out MicrogameCanvasState canvasState
            );
            
            state.Phase = SputterMicrogamePhase.Exiting;
            if (!completedNormally) { return; }

            GameObject.Destroy(state.SputterPattern.gameObject);

            state.SputterUI.SetActive(false);
            MicrogameCanvasUtility.HideStationInstructions(canvasState);

            MicrogameUtility.CommitStepPrecision(ComputePrecision());
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
            Find.State(out SputterMicrogameState state, out MicrogameCanvasState canvasState);

            state.IsActive = false;
            state.Phase = SputterMicrogamePhase.Idle;

            for (int i = 0; i < state.ProjectileParent.childCount; i++)
            {
                GameObject.Destroy(state.ProjectileParent.GetChild(i).gameObject);
            }
        }

        // Side-effect-free precision query for the precision gate, read before ExitBegin commits.
        public static float GetResultPrecision()
        {
            return ComputePrecision();
        }

        // Sputter error is unsigned (fill percent), so raw equals the gate precision.
        public static float GetRawResultPrecision()
        {
            return ComputePrecision();
        }

        // Spraypaint-specific precision math: percent of the etched target area that got filled
        // by the sputter head. Scaffold returns 0.
        private static float ComputePrecision()
        {
            Find.State(out SputterMicrogameState state);

            if (state.SputterPattern.m_TotalSlots == 0) { return 0f; }

            float precision = (float)state.SputterPattern.m_FilledSlots / state.SputterPattern.m_TotalSlots;
            return Mathf.Clamp01(precision);
        }
    }
}
