using System;
using BeauRoutine;
using FieldDay;
using FieldDay.Animation;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Fabrication;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class ShipListRow : GuiWidget {
        public Image SpeedIcon;
        public Image ShipIcon;
        public Image[] ShipBody;
        public Image[] ShipOutline;
        public RectTransform[] Slots;
        public Image[] SlotMaterials;
        public TMP_Text ShipName;
        public Image Flash;

        [Header("Links")]
        public Image LineLayer;
        public SupplyShipBreakdownRow StatsLayer;
        public LayoutStyleInfo Style;

        [NonSerialized] public int ShipIndex;
        [NonSerialized] public Vector2 TargetPos;
        [NonSerialized] public AnimHandle Anim;

        protected override void OnDisable() {
            LineLayer.gameObject.SetActive(false);
            StatsLayer.gameObject.SetActive(false);
            
            base.OnDisable();
        }
    }

    static public partial class SupplyChainUtility {
        static public void PopulateShipInformation(ShipListRow row, SupplyShipAsset shipAsset, ShipListPanel panel) {
            bool isWide = shipAsset.Capacity > 2;
            float iconPos = 95;
            float bodySize = 130;

            if (isWide) {
                iconPos += 20;
                bodySize += 20;
            }

            Positioning.SetOffsetX(row.ShipIcon.rectTransform, iconPos);
            
            row.ShipIcon.sprite = shipAsset.Icon;
            row.ShipIcon.color = shipAsset.IconColor;
            foreach(var bodySprite in row.ShipBody) {
                Positioning.SetWidthDelta(bodySprite.rectTransform, bodySize);
                bodySprite.sprite = shipAsset.BodyImage;
            }
            foreach (var outlineSprite in row.ShipOutline) {
                Positioning.SetWidthDelta(outlineSprite.rectTransform, bodySize);
                outlineSprite.sprite = shipAsset.BodyOutline;
                outlineSprite.color = shipAsset.IconColor;
                outlineSprite.enabled = false;
            }

            ShipListPanel.SpeedIconConfig speedIcon = panel.SpeedIcons[shipAsset.Speed];
            row.SpeedIcon.sprite = speedIcon.Image;
            row.SpeedIcon.rectTransform.sizeDelta = speedIcon.Size;

            for (int i = 0; i < row.Slots.Length; i++) {
                row.Slots[i].gameObject.SetActive(i < shipAsset.Capacity);
            }

            for (int i = 0; i < row.SlotMaterials.Length; i++) {
                row.SlotMaterials[i].enabled = false;
            }

            row.CursorHint.TooltipHeader = shipAsset.DisplayName;

            row.LineLayer.color = shipAsset.LineColor;

            row.ShipName.SetText(shipAsset.DisplayName);
            row.ShipName.color = shipAsset.Colors.Content;
        }

        static public void SyncShipRowPositions(ShipListRow row) {
            Vector2 anchorPos = row.Rect.anchoredPosition;
            row.LineLayer.rectTransform.anchoredPosition = anchorPos;
            Positioning.SetOffsetY(row.StatsLayer.Rect, anchorPos.y);
        }

        static public void SetShipRowStatsActive(ShipListRow row, bool active) {
            if (active) {
                row.StatsLayer.gameObject.SetActive(true);
                row.Style.Style.MarginLower.y = 52;
                foreach(var outline in row.ShipOutline) {
                    outline.enabled = false;
                }
            } else {
                row.Style.Style.MarginLower.y = 0;
                row.StatsLayer.gameObject.SetActive(false);
                foreach (var outline in row.ShipOutline) {
                    outline.enabled = true;
                }
            }
        }
    }
}