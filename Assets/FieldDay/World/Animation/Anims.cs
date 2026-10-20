using System;
using System.Runtime.CompilerServices;
using BeauUtil;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace FieldDay.Animation {
    static public class Anims {
        static public bool IsPlaying(AnimHandle handle) {
            return Game.Animation.IsAnimationRunning(handle);
        }

        static public void Cancel(AnimHandle handle) {
            Game.Animation.CancelAnimation(handle);
        }

        static public void Cancel(ref AnimHandle handle) {
            Game.Animation.CancelAnimation(ref handle);
        }

        static public void Replace(ref AnimHandle handle, ILiteAnimator anim, object target, LiteAnimatorState state, GameLoopPhase phase = GameLoopPhase.Update) {
            Game.Animation.CancelAnimation(ref handle);
            handle = Game.Animation.AddLiteAnimator(anim, target, state, phase);
        }

        static public void Replace(ref AnimHandle handle, ILiteAnimator anim, object target, float duration, GameLoopPhase phase = GameLoopPhase.Update) {
            Game.Animation.CancelAnimation(ref handle);
            handle = Game.Animation.AddLiteAnimator(anim, target, duration, phase);
        }

        static public void Replace<T>(ref AnimHandle handle, ILiteAnimator<T> anim, T target, LiteAnimatorState state, GameLoopPhase phase = GameLoopPhase.Update) where T : class {
            Game.Animation.CancelAnimation(ref handle);
            handle = Game.Animation.AddLiteAnimator(anim, target, state, phase);
        }

        static public void Replace<T>(ref AnimHandle handle, ILiteAnimator<T> anim, T target, float duration, GameLoopPhase phase = GameLoopPhase.Update) where T : class {
            Game.Animation.CancelAnimation(ref handle);
            handle = Game.Animation.AddLiteAnimator(anim, target, duration, phase);
        }

        static public AnimHandle Play(ILiteAnimator anim, object target, LiteAnimatorState state, GameLoopPhase phase = GameLoopPhase.Update) {
            return Game.Animation.AddLiteAnimator(anim, target, state, phase);
        }

        static public AnimHandle Play(ILiteAnimator anim, object target, float duration, GameLoopPhase phase = GameLoopPhase.Update) {
            return Game.Animation.AddLiteAnimator(anim, target, duration, phase);
        }

        static public AnimHandle Play<T>(ILiteAnimator<T> anim, T target, LiteAnimatorState state, GameLoopPhase phase = GameLoopPhase.Update) where T : class {
            return Game.Animation.AddLiteAnimator(anim, target, state, phase);
        }

        static public AnimHandle Play<T>(ILiteAnimator<T> anim, T target, float duration, GameLoopPhase phase = GameLoopPhase.Update) where T : class {
            return Game.Animation.AddLiteAnimator(anim, target, duration, phase);
        }
    }
}