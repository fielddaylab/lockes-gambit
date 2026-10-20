using System;
using BeauRoutine;
using FieldDay.Animation;
using UnityEngine;
using UnityEngine.UI;

namespace FieldDay.UI.Animation {
    public sealed class FlashAnim : LiteAnimator<Graphic> {

        private readonly float m_Duration;
        private readonly Curve m_Ease;

        public FlashAnim(float duration, Curve ease = Curve.Linear) {
            m_Duration = duration;
            m_Ease = ease;
        }

        static private void PrepareState(ref LiteAnimatorState state, Color color, float duration, float delay, Curve easing) {
            state.ResetTimeWithDelay(duration, delay);
            state.Registers.A.Color() = color;
            state.Easing = easing;
        }

        public override void InitAnimation(Graphic target, ref LiteAnimatorState state) {
            if (!state.IsDelayed()) {
                target.enabled = true;
                target.color = state.Registers.A.Color();
            }
        }

        public override void ResetAnimation(Graphic target, ref LiteAnimatorState state) {
            target.enabled = false;
        }

        public override void UpdateAnimation(Graphic target, ref LiteAnimatorState state, float deltaTime) {
            float percent = state.Easing.Evaluate(state.PercentProgress);
            Color newColor = state.Registers.A.Color();
            newColor.a *= (1f - percent);
            target.color = newColor;
            target.enabled = newColor.a > 0;
        }

        static public readonly FlashAnim Default = new FlashAnim(12 / 60f, Curve.Linear);
        static public readonly FlashAnim Quick = new FlashAnim(6 / 60f, Curve.Linear);

        static public AnimHandle Play(Graphic graphic, Color color, FlashAnim config, float delay = 0, GameLoopPhase phase = GameLoopPhase.Update) {
            LiteAnimatorState animState = new LiteAnimatorState();
            PrepareState(ref animState, color, config.m_Duration, delay, config.m_Ease);
            return Game.Animation.AddLiteAnimator(config, graphic, animState, phase);
        }
    }
}