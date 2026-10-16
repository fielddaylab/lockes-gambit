using System;
using BeauRoutine;
using BeauUtil;
using FieldDay.Animation;
using ScriptableBake;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    [RequireComponent(typeof(Mask), typeof(MaskableGraphic))]
    public sealed class DitherFadeGroup : MonoBehaviour, IBaked {
        [NonSerialized] public Mask Mask;
        [NonSerialized] public MaskableGraphic Graphic;

        [NonSerialized] public AnimHandle QuickAnim;

        public void SetAlpha(float alpha) {
            this.CacheComponent(ref Mask);
            this.CacheComponent(ref Graphic);

            bool enabled = alpha < 1;
            Mask.enabled = Graphic.enabled = enabled;

            Color graphicColor = Graphic.color;
            graphicColor.a = alpha;
            Graphic.color = graphicColor;
        }

        private void OnDisable() {
            Anims.Cancel(ref QuickAnim);
        }

#if UNITY_EDITOR

        int IBaked.Order { get { return 0; } }
        bool IBaked.Bake(BakeFlags flags, BakeContext context) {
            SetAlpha(1);
            return true;
        }

#endif // UNITY_EDITOR
    }

    static public class DitherFade {


        static private readonly DissolveInAnimation DissolveInAnimInstance = new DissolveInAnimation();

        private sealed class DissolveInAnimation : LiteAnimator<DitherFadeGroup> {
            public override void InitAnimation(DitherFadeGroup target, ref LiteAnimatorState state) {
                if (state.Duration <= 0) {
                    state.ResetTime(0.44f);
                }
                target.SetAlpha(0);
            }

            public override void ResetAnimation(DitherFadeGroup target, ref LiteAnimatorState state) {
            }

            public override void UpdateAnimation(DitherFadeGroup target, ref LiteAnimatorState state, float deltaTime) {
                target.SetAlpha(state.PercentProgress);
            }
        }

        static public void DissolveIn(DitherFadeGroup target, float duration = -1) {
            Anims.Replace(ref target.QuickAnim, DissolveInAnimInstance, target, duration);
        }
    }
}