using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Animation;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using SpaceFab.Onboarding;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public class WikiPageButton : GuiButton.Style {
        public Graphic Background;
        public Graphic Outline;
        public ElementTag Taggable;

        [Header("Material Page")]
        public GameObject MaterialLabelGroup;
        public TextMeshProUGUI MaterialLabel;
        public LayoutSizeGroup MaterialLabelSizer;

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
                    Widget.LayoutOffset.Offset0 = new Vector2(0, isToggleOn ? TravelDistance : 0);
                } else {
                    Animation = Anims.Play(isToggleOn ? ToggleOnAnimInstance : ToggleOffAnimInstance, this, 0);
                }
            }
        }

        static private readonly Color ToggleOnColor = Colors.RGBA(0x141414FF);
        static private readonly Color ToggleOffColor = Colors.RGBA(0x8a7765FF);

        private const float TravelDistance = 2;

        static private readonly ToggleOnAnim ToggleOnAnimInstance = new ToggleOnAnim();
        static private readonly ToggleOffAnim ToggleOffAnimInstance = new ToggleOffAnim();

        private sealed class ToggleOnAnim : LiteAnimator<WikiPageButton> {
            public override void InitAnimation(WikiPageButton target, ref LiteAnimatorState state) {
                state.ResetTime(0.15f);
            }

            public override void ResetAnimation(WikiPageButton target, ref LiteAnimatorState state) { }

            public override void UpdateAnimation(WikiPageButton target, ref LiteAnimatorState state, float deltaTime) {
                float progress = state.PercentProgress;
                float curve = Curve.BackOut.Evaluate(progress);
                target.Widget.LayoutOffset.Offset0 = new Vector2(0, curve * TravelDistance);
                target.Background.color = Color.Lerp(ToggleOffColor, ToggleOnColor, progress);
            }
        }

        private sealed class ToggleOffAnim : LiteAnimator<WikiPageButton> {
            public override void InitAnimation(WikiPageButton target, ref LiteAnimatorState state) {
                state.ResetTime(0.12f);
            }

            public override void ResetAnimation(WikiPageButton target, ref LiteAnimatorState state) { }

            public override void UpdateAnimation(WikiPageButton target, ref LiteAnimatorState state, float deltaTime) {
                float progress = state.PercentProgress;
                float curve = Curve.CubeOut.Evaluate(progress);
                target.Widget.LayoutOffset.Offset0 = new Vector2(0, (1 - curve) * TravelDistance);
                target.Background.color = Color.Lerp(ToggleOffColor, ToggleOnColor, 1 - progress);
            }
        }
    }

    static public partial class WikiLayoutUtility {
        static public void PopulatePageButton(WikiPageButton button, WikiPageData pageData, int pageIndex) {
            button.Widget.SetVariantValue(pageIndex);
            button.Widget.CursorHint.MarkDirty();

            Sprite pageIcon = pageData.Icon;

            if (pageData.IsMaterialPage) {
                MaterialAsset material = Find.NamedAsset<MaterialAsset>(pageData.MaterialId);
                button.MaterialLabel.SetText(material.ShortName);
                Positioning.ResizeToPreferred(button.MaterialLabel);
                button.MaterialLabelSizer.Sync();
                button.MaterialLabelGroup.SetActive(true);
                pageIcon = material.GemSprite;
                button.Widget.CursorHint.TooltipHeader = material.DisplayName;
                button.Taggable.SetId(WikiElementTagUtility.PageThumbId(material.ShortName));
            } else {
                button.MaterialLabelGroup.SetActive(false);
                button.Widget.CursorHint.TooltipHeader = pageData.Title;
                button.Taggable.SetId(WikiElementTagUtility.PageThumbId(pageData.Title));
            }

            button.Widget.ImageGraphic.sprite = pageIcon;
        }
    }
}