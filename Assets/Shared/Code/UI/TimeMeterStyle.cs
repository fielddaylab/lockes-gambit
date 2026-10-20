using System;
using FieldDay.UI.Widgets;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class TimeMeterStyle : GuiCounter.Style {
        public Image[] Slots;

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
        }
    }
}