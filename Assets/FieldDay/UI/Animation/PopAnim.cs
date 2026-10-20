using System;
using BeauRoutine;
using FieldDay.Animation;
using UnityEngine;
using UnityEngine.UI;

namespace FieldDay.UI.Animation {
    public sealed class PopAnim : LiteAnimator<LayoutOffset> {

        private readonly Vector2 m_Offset;
        private readonly float m_Duration;
        private readonly Curve m_Ease;

        public PopAnim(Vector2 offset, float duration, Curve ease = Curve.Linear) {
            m_Offset = offset;
            m_Duration = duration;
            m_Ease = ease;
        }

        static private void PrepareState(ref LiteAnimatorState state, Vector2 offsetAmt, float duration, float delay, Curve easing) {
            state.ResetTimeWithDelay(duration, delay);
            state.Registers.A.Float2() = offsetAmt;
            state.Easing = easing;
        }

        public override void InitAnimation(LayoutOffset target, ref LiteAnimatorState state) {
            if (!state.IsDelayed()) {
                target.Offset3 = state.Registers.A.Float2();
            }
        }

        public override void ResetAnimation(LayoutOffset target, ref LiteAnimatorState state) {
            target.Offset3 = default;
        }

        public override void UpdateAnimation(LayoutOffset target, ref LiteAnimatorState state, float deltaTime) {
            float percent = state.Easing.Evaluate(state.PercentProgress);
            target.Offset3 = state.Registers.A.Float2() * (1f - percent);
        }

        static public readonly PopAnim Default = new PopAnim(new Vector2(0, -4), 8 / 60f, Curve.Linear);
        static public readonly PopAnim DefaultUp = new PopAnim(new Vector2(0, 4), 8 / 60f, Curve.Linear);

        static public AnimHandle Play(LayoutOffset offset, PopAnim config, float delay = 0, GameLoopPhase phase = GameLoopPhase.Update) {
            LiteAnimatorState animState = new LiteAnimatorState();
            PrepareState(ref animState, config.m_Offset, config.m_Duration, delay, config.m_Ease);
            return Game.Animation.AddLiteAnimator(config, offset, animState, phase);
        }
    }
}