using FieldDay.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class RiskMeterStyle : GuiCounter.Style {
        public Image[] Slots;

        [Header("Config")]
        public Sprite EmptySlot;
        public Sprite FullSlot;

        [Header("Optional")]
        public Image Highlight;
        public Image Flash;
        
        public override void Populate(in int data, GuiWidget source, GuiWidgetUpdateFlags flags) {
            for(int i = 0; i < data; i++) {
                Slots[i].sprite = FullSlot;
            }
            for (int i = data; i < Slots.Length; i++) {
                Slots[i].sprite = EmptySlot;
            }
        }
    }
}