using System;
using FieldDay;
using FieldDay.Systems;
using SpaceFab;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Advances the flow-sprite frame on every CircuitRenderer with a non-zero
    /// CircuitSpriteSpeed and rotates each renderer's bulb shines at fixed
    /// angular velocities. Runs every frame the chamber mask is active; the
    /// chamber system itself only writes CircuitSpriteSpeed when state changes.
    /// </summary>
    public class CircuitAnimationSystem : SystemComponent, IRegistrationCallbacks
    {
        // Per-shine rotation rates (degrees per second), applied to the
        // BulbShines array on each renderer. Three shines per bulb is the
        // expected layout; extras get a zero rate.
        [NotStateful] private static readonly float[] s_ShineRotationRates = new float[] { 24f, -30.2f, 17f };

        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.ResearchChamberMask),
                new SysPermissions().ReadWrite<CircuitRenderer>());
        }
        public void OnRegister()
        {
            
        }
        public void OnDeregister()
        {
            
        }

        private static void ProcessWork(float deltaTime)
        {
            foreach (CircuitRenderer circuit in Find.Components<CircuitRenderer>())
            {
                AdvanceFlow(circuit, deltaTime);
                //RotateShines(circuit, deltaTime);
            }
        }

        // Advances the positions of electrons over time and updates visual effects.
        private static void AdvanceFlow(CircuitRenderer circuit, float deltaTime)
        {
            if (circuit.CircuitCurrent == 0f) return;
            if (circuit.Electrons == null) return;

            foreach (Electron electron in circuit.Electrons)
            {
                var segment = circuit.FlowSegments[electron.FlowSegmentIndex];
                electron.TravelDistance += deltaTime * circuit.AnimSpeedMultiplier * circuit.CircuitCurrent;

                // Handle segment transition and wrapping
                if (electron.TravelDistance >= segment.Length)
                {
                    electron.FlowSegmentIndex++;
                    electron.TravelDistance -= segment.Length;
                    if (electron.FlowSegmentIndex >= circuit.FlowSegments.Length)
                        electron.FlowSegmentIndex = 0;
                }
                
                electron.transform.position = CircuitUtility.GetPositionOnSegment(circuit, electron);
                segment = circuit.FlowSegments[electron.FlowSegmentIndex];

                // Fade near endpoints
                float threshold = 0.1f;
                float multiplier = 1f / threshold;
                float distanceToEndpoint = Math.Min(electron.TravelDistance, segment.Length - electron.TravelDistance);
                if (distanceToEndpoint <= threshold)
                    electron.Sprite.color = new Color(1f, 1f, 1f, distanceToEndpoint * multiplier);
                else
                    electron.Sprite.color = Color.white;
            }
        }

        // Rotates each bulb shine at its fixed rate. Independent of flow; the
        // shines spin even when the circuit isn't carrying current, but their
        // alpha goes to zero so they're invisible.
        private static void RotateShines(CircuitRenderer circuit, float deltaTime)
        {
            if (circuit.BulbShines == null) return;
            for (int i = 0; i < circuit.BulbShines.Length; i++)
            {
                SpriteRenderer shine = circuit.BulbShines[i];
                if (shine == null) continue;
                float rate = i < s_ShineRotationRates.Length ? s_ShineRotationRates[i] : 0f;
                shine.transform.Rotate(0f, 0f, rate * deltaTime);
            }
        }
    }
}
