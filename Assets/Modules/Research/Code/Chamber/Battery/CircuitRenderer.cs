using FieldDay.Components;
using FieldDay.Rendering;
using System;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Visual rig for the Battery chamber's circuit: the flow tube, the bulb
    /// with its on/off sprites, and the bulb shines that rotate while the
    /// circuit is active. CircuitAnimationSystem advances the flow frame and
    /// rotates the shines; chamber systems drive light strength + flow speed
    /// via CircuitUtility.
    /// </summary>
    public class CircuitRenderer : BatchedComponent
    {
        [System.Serializable]
        public class FlowSegment
        {
            public float Length;
            public Vector2[] Points;
        }

        public Electron[] Electrons;
        public FlowSegment[] FlowSegments;
        public float TotalLength; // total length of circuit loop

        public SpriteRenderer Bulb;
        public Sprite BulbOffSprite;
        public Sprite BulbOnSprite;

        public SpriteRenderer[] BulbShines;
        public float AnimSpeedMultiplier = 1f;

        public ParticleSystem HighMobilityParticles;
        public ParticleSystem LightEmittingParticles;

        // Magnitude drives flow speed. Set by CircuitUtility.SetFlowStrength.
        [NonSerialized] public float CircuitCurrent;

        private void Awake() {
            // if (!GameLoop.IsBooted()) {
            //     GameLoop.QueueOnBoot(Init);
            // } else {
                InitCircuitFlow();
            //}
        }

        // Initializes the circuit flow by calculating path lengths and evenly
        // distributing electrons.
        private void InitCircuitFlow()
        {
            TotalLength = 0f;
            // Calculate individual segment lengths and total path length
            foreach (FlowSegment segment in FlowSegments)
            {
                float segmentLength = 0f;
                for (int i = 1; i < segment.Points.Length; i++)
                {
                    segmentLength += Vector2.Distance(segment.Points[i-1], segment.Points[i]);
                }
                segment.Length = segmentLength;
                TotalLength += segmentLength;
            }

            // Evenly distribute electrons along the calculated path
            for (int i = 0; i < Electrons.Length; i++)
            {
                Electrons[i].TravelDistance = i * TotalLength / Electrons.Length;
                for (int j = 0; j < Electrons[i].FlowSegmentIndex; j++)
                    Electrons[i].TravelDistance -= FlowSegments[j].Length;

                Electrons[i].transform.position = CircuitUtility.GetPositionOnSegment(this, Electrons[i]);
            }

            CircuitCurrent = 0f;
        }
    }

    /// <summary>
    /// Drives the bulb on/off + shine alpha and the flow tube alpha + speed.
    /// Pure setters; the animation system reads CircuitSpriteSpeed and
    /// advances frames + rotates shines on its own.
    /// </summary>
    public static class CircuitUtility
    {
        // Bulb on if |strength| crosses a threshold; shine alpha scales as
        // strength squared so weak currents dim the shines toward zero faster
        // than the bulb fade.
        public static void SetLightStrength(CircuitRenderer circuit, float strength)
        {
            if (circuit == null) return;

            float magnitude = Mathf.Abs(strength);
            if (circuit.Bulb != null)
            {
                circuit.Bulb.sprite = magnitude > 0.1f ? circuit.BulbOnSprite : circuit.BulbOffSprite;
            }

            if (circuit.BulbShines != null)
            {
                float shineAlpha = magnitude * magnitude;
                for (int i = 0; i < circuit.BulbShines.Length; i++)
                {
                    SpriteRenderer shine = circuit.BulbShines[i];
                    if (shine == null) continue;
                    shine.enabled = magnitude > 0f;
                    Color c = shine.color;
                    c.a = shineAlpha;
                    shine.color = c;
                }
            }
        }

        // Sets the flow tube's density. The animation system reads CircuitSpriteSpeed each frame.
        public static void SetFlowStrength(CircuitRenderer circuit, float strength, bool isHighMobility = false, bool isLightEmitting = false)
        {
            if (circuit == null) return;
            circuit.CircuitCurrent = strength;

            foreach (Electron electron in circuit.Electrons)
            {
                electron.gameObject.SetActive(strength > 0);
            }

            if (circuit.HighMobilityParticles) {
                if (isHighMobility && strength > 0) {
                    if (!circuit.HighMobilityParticles.isPlaying) {
                        circuit.HighMobilityParticles.Play();
                    }
                    circuit.HighMobilityParticles.SetEmissionMultiplier(strength * 32);
                } else {
                    circuit.HighMobilityParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (circuit.LightEmittingParticles) {
                if (isLightEmitting && strength > 0) {
                    if (!circuit.LightEmittingParticles.isPlaying) {
                        circuit.LightEmittingParticles.Play();
                    }
                    circuit.LightEmittingParticles.SetEmissionMultiplier(strength * 10);
                } else {
                    circuit.LightEmittingParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                
            }
        }

        public static Vector2 GetPositionOnSegment(CircuitRenderer circuit, Electron electron)
        {
            var segment = circuit.FlowSegments[electron.FlowSegmentIndex];
            float travelDistance = electron.TravelDistance;
            for (int i = 1; i < segment.Points.Length; i++)
            {
                float length = Vector2.Distance(segment.Points[i-1], segment.Points[i]);

                if (travelDistance <= length)
                    return Vector2.Lerp(segment.Points[i-1], segment.Points[i], travelDistance / length);
                
                travelDistance -= length;
            }
            
            return segment.Points[^1]; // safety fallback; unreachable in correct flow
        }
    }
}
