using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.UI;
using FieldDay.UI.Animation;
using FieldDay.UI.Widgets;
using UnityEngine;

namespace SpaceFab.UI {
    public sealed class HoverButtonStyle : GuiButton.Style {
        public RectTransform Shift;
        public CanvasRenderer[] DisabledTint;
        public float HoverYShift;
        public float DownYShift;
        public float DisabledYShift;

        public override void OnClick(GuiButton button) {
            //PopAnim.Play(button.LayoutOffset, PopAnim.Default);
        }

        public override void UpdateInteractionState(GuiWidgetInteractableState state, GuiWidget source, GuiWidgetUpdateFlags flags) {            
            float y = 0;
            Color tint = Color.white;

            switch(state) {
                case GuiWidgetInteractableState.Down: {
                    y = DownYShift;
                    break;
                }
                case GuiWidgetInteractableState.Hover: {
                    y = HoverYShift;
                    break;
                }
                case GuiWidgetInteractableState.Disabled: {
                    tint = ColorBank.DarkGray;
                    y = DisabledYShift;
                    break;
                }
            }

            Vector3 anchorPos = Shift.anchoredPosition3D;
            anchorPos.y = y;
            Shift.anchoredPosition = anchorPos;

            foreach(var tintable in DisabledTint) {
                tintable.SetColor(tint);
            }
        }
    }
}