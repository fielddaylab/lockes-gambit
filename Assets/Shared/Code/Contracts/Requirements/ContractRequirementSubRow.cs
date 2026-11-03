using System;
using System.Text;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab {
    public sealed class ContractRequirementSubRow : MonoBehaviour {
        public const int MaxSlots = 5;

        public CursorHint Cursor;

        [Header("Icon")]
        public Image Icon;

        [Header("Labels")]
        public TMP_Text Label;
        public TMP_Text SubLabel;
        public LayoutOffset[] SubLabelShift;

        [Header("Slots")]
        public Image[] Slots;
        public Graphic[] SlotConnections;

        [Header("Children")]
        public GameObject[] VirtualChildren;

        [NonSerialized] public MaterialPropertyLabel DisplayedProperty;
    }

    static public partial class ContractUIUtility {
        static public unsafe void AssignSubRowCount(ContractRequirementSubRow row, int materialCount) {
            Assert.True(row.SlotConnections.Length == 0 || row.SlotConnections.Length == row.Slots.Length);
            for(int i = 0; i < row.Slots.Length; i++) {
                row.Slots[i].enabled = i < materialCount;
            }
            for(int i = 0; i < row.SlotConnections.Length; i++) {
                row.SlotConnections[i].enabled = i < materialCount;
            }
        }

        static public void AssignSubRowLabel(ContractRequirementSubRow row, StringBuilder label, StringBuilder subLabel, Sprite icon, float subLabelShift) {
            row.Label.SetText(label);

            bool hasSubLabel = subLabel != null && subLabel.Length > 0;
            Assert.True(row.SubLabel || !hasSubLabel, "Sublabel provided but non present on prefab");

            if (row.SubLabel != null) {
                row.SubLabel.enabled = hasSubLabel;

                if (hasSubLabel) {
                    row.SubLabel.SetText(subLabel);
                    foreach (var shift in row.SubLabelShift) {
                        shift.Offset1 = new Vector2(0, subLabelShift);
                    }
                } else {
                    foreach (var shift in row.SubLabelShift) {
                        shift.Offset1 = new Vector2(0, 0);
                    }
                }
            }

            Assert.True(row.Icon || !icon, "Icon provided but none present on prefab");
            if (icon) {
                row.Icon.sprite = icon;
            }
        }
    }
}