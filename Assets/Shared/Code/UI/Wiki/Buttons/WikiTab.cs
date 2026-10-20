using BeauRoutine;
using BeauUtil;
using BeauUtil.UI;
using BeauUtil.Variants;
using FieldDay;
using FieldDay.Animation;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Fabrication;
using SpaceFab.Onboarding;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI
{
    /// <summary>
    /// Inspector reference to the icon on a pooled wiki tab button, kept separate from the
    /// button's own background Image. Written by WikiVisualsUtility.RefreshTabStrip.
    /// </summary>
    public class WikiTab : GuiButton.Style
    {
        public Graphic Background;
        public Graphic Outline;
        public ElementTag Taggable;

        public AnimHandle Animation;

        public override void OnClick(GuiButton button) {
            
        }

        public override void UpdateInteractionState(GuiWidgetInteractableState state, GuiWidget source, GuiWidgetUpdateFlags flags) {
        }

        public override void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags changed, GuiWidget source, GuiWidgetUpdateFlags flags) {
            if ((changed & GuiWidgetStateFlags.IsToggleOn) != 0 || (flags & GuiWidgetUpdateFlags.Force) != 0) {
                bool isToggleOn = (state & GuiWidgetStateFlags.IsToggleOn) != 0;
                Outline.enabled = isToggleOn;

                Anims.Cancel(ref Animation);
                Game.Animation.CancelAnimation(ref Animation);
                if ((flags & GuiWidgetUpdateFlags.NoAnimation) != 0) {
                    Background.color = isToggleOn ? ToggleOnColor : ToggleOffColor;
                    Widget.LayoutOffset.Offset0 = new Vector2(isToggleOn ? TravelDistance : 0, 0);
                } else {
                    Animation = Anims.Play(isToggleOn ? ToggleOnAnimInstance : ToggleOffAnimInstance, this, 0);
                }
            }
        }

        static private readonly Color ToggleOnColor = Colors.RGBA(0xccb7a4FF);
        static private readonly Color ToggleOffColor = Colors.RGBA(0x988573FF);

        private const float TravelDistance = -10;

        static private readonly ToggleOnAnim ToggleOnAnimInstance = new ToggleOnAnim();
        static private readonly ToggleOffAnim ToggleOffAnimInstance = new ToggleOffAnim();

        private sealed class ToggleOnAnim : LiteAnimator<WikiTab> {
            public override void InitAnimation(WikiTab target, ref LiteAnimatorState state) {
                state.ResetTime(0.15f);
            }

            public override void ResetAnimation(WikiTab target, ref LiteAnimatorState state) { }

            public override void UpdateAnimation(WikiTab target, ref LiteAnimatorState state, float deltaTime) {
                float progress = state.PercentProgress;
                float curve = Curve.BackOut.Evaluate(progress);
                target.Widget.LayoutOffset.Offset0 = new Vector2(curve * TravelDistance, 0);
                target.Background.color = Color.Lerp(ToggleOffColor, ToggleOnColor, progress);
            }
        }

        private sealed class ToggleOffAnim : LiteAnimator<WikiTab> {
            public override void InitAnimation(WikiTab target, ref LiteAnimatorState state) {
                state.ResetTime(0.12f);
            }

            public override void ResetAnimation(WikiTab target, ref LiteAnimatorState state) { }

            public override void UpdateAnimation(WikiTab target, ref LiteAnimatorState state, float deltaTime) {
                float progress = state.PercentProgress;
                float curve = Curve.CubeOut.Evaluate(progress);
                target.Widget.LayoutOffset.Offset0 = new Vector2((1 - curve) * TravelDistance, 0);
                target.Background.color = Color.Lerp(ToggleOffColor, ToggleOnColor, 1 - progress);
            }
        }
    }

    static public partial class WikiLayoutUtility {
        
        /// <summary>
        /// Populates the appearance of a tab button.
        /// </summary>
        static public void PopulateTabButton(WikiTab button, WikiTabData tabData, int tabIndex) {
            button.Widget.CursorHint.TooltipHeader = tabData.Title;
            button.Widget.CursorHint.MarkDirty();
            button.Widget.SetVariantValue(tabIndex);
            button.Widget.ImageGraphic.sprite = tabData.Icon;
            button.Taggable.SetId(WikiElementTagUtility.TabId(tabData.Title));
        }
    }
}