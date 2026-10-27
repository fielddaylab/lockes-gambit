using FieldDay;
using FieldDay.Systems;
using FieldDay.Scripting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using FieldDay.UI.Widgets;
using BeauRoutine;
using UnityEngine.UI;

namespace SpaceFab.Overarching {
    public class ContractScrollButtonStyle : GuiButton.Style {

        public float HoverShift;
        public float DownShift;
        public float DisabledShift;
        public Graphic Overlay;

        public override void OnClick(GuiButton button) {
            
        }

        public override void UpdateInteractionState(GuiWidgetInteractableState state, GuiWidget source, GuiWidgetUpdateFlags flags) {
            switch (state) {
                case GuiWidgetInteractableState.Normal: {
                    source.LayoutOffset.Offset0 = default;
                    break;
                }
                case GuiWidgetInteractableState.Hover: {
                    source.LayoutOffset.Offset0 = new Vector2(HoverShift, 0);
                    break;
                }
                case GuiWidgetInteractableState.Down: {
                    source.LayoutOffset.Offset0 = new Vector2(DownShift, 0);
                    break;
                }
            }
        }

        public override void UpdateState(GuiWidgetStateFlags state, GuiWidgetStateFlags changed, GuiWidget source, GuiWidgetUpdateFlags flags) {
            if ((state & GuiWidgetStateFlags.PauseInteractions) != 0) {
                Widget.ImageGraphic.SetAlpha(0.25f);
                Widget.LayoutOffset.Offset1 = new Vector2(DisabledShift, 0);
            } else {
                Widget.ImageGraphic.SetAlpha(1f);
                Widget.LayoutOffset.Offset1 = default;
            }
        }
    }
}
