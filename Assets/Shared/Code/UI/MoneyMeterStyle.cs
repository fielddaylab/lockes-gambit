using System;
using System.Collections;
using FieldDay.UI.Animation;
using FieldDay.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class MoneyMeterStyle : GuiCounter.Style {
        public Image[] Slots;
        // public Image[] FlashOverlays;

        [Header("Config")]
        public Sprite EmptySlot;
        public Sprite FullSlot;
        public Sprite ExpectedSlot;

        [NonSerialized] public int ExpectedSlots;

        public void SetExpectedSlots(int expected) {
            ExpectedSlots = expected;
        }

        public override void Populate(in int data, GuiWidget source, GuiWidgetUpdateFlags flags) {
            bool hasEmpty = EmptySlot;
            bool hasExpected = ExpectedSlot;
            
            for(int i = 0; i < data; i++) {
                Slots[i].sprite = FullSlot;
                Slots[i].enabled = true;
            }

            if (hasExpected) {
                for (int i = data; i < ExpectedSlots; i++) {
                    Slots[i].enabled = true;
                    Slots[i].sprite = ExpectedSlot;
                }
            }

            for (int i = Math.Max(ExpectedSlots, data); i < Slots.Length; i++) {
                Slots[i].enabled = hasEmpty;
                Slots[i].sprite = EmptySlot;
            }

            // if ((flags & GuiWidgetUpdateFlags.Initialization) == 0
            //     && (flags & GuiWidgetUpdateFlags.IsIncrease) != 0
            //     && data > 0 && data <= FlashOverlays.Length) {
            //     FlashAnim.Play(FlashOverlays[data - 1], Color.white, FlashAnim.Default);
            // }
        }

        static public IEnumerator DrainIntoMeter(GuiCounter src, GuiCounter dst) {
            while (src.Value > 0) {
                src.SetValue(src.Value - 1);
                dst.SetValue(dst.Value - 1);
                yield return 0.2f;
            }
        }
    }
}