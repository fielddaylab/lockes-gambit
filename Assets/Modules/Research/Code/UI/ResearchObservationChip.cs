using BeauRoutine;
using FieldDay;
using FieldDay.HID;
using FieldDay.UI;
using SpaceFab.Materials;
using SpaceFab.Onboarding;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SpaceFab.Research.ResearchObservationChipAssets;

namespace SpaceFab.Research {
    /// <summary>
    /// One chip in the hypothesis or observations panel. Pure view: a
    /// background Image, a TMP label, an optional CursorHint for chips
    /// that respond to clicks (sample-panel slots and chip-picker
    /// entries), and an optional LockedOverlay shown when an auto-
    /// populated slot cannot be removed.
    ///
    /// All visual data — sprites per ObservationType + the two label
    /// colors — lives on a global ResearchObservationChipAsset that this
    /// chip reads at every SetState call. Nothing is baked into the
    /// prefab.
    /// </summary>
    public class ResearchObservationChip : MonoBehaviour {
        public TMP_Text LabelText;
        public Image Icon;

        public Image[] BackgroundGraphics;
        public Image[] OutlineGraphics;
        public Graphic[] BackgroundTint;
        public Graphic[] ContentTint;

        public CursorHint Click;

        // Onboarding highlight handle, left unassigned on the prefab. Chips are pool-allocated
        // and reused across pages, so neither the tag nor its id can be baked in: the wiki's
        // page-load utilities attach one on demand via WikiElementTagUtility, stamp a per-page
        // id, and clear it again on free. Allocators that don't tutorialize their chips (the
        // Research picker and sample panel) never touch this, so those chips stay untagged and
        // out of ElementTagLookup entirely. Assign it in the inspector to point the highlight at
        // a different host than the chip root.
        public ElementTag Tag;

        // Label color applied by the most recent SetState, so the
        // disabled visual can restore it when re-enabled.
        [NonSerialized] private ColorPalette2 m_LastAssignedPalette;
        [NonSerialized] private Color m_LastAssignedIconColor;
        [NonSerialized] private bool m_TintIcon;

        public void SetProperty(string text, ChipFillState fillState, bool locked, MaterialPropertyLabel propertyLabel, bool useEmptyDashedSprite = false) {
            ResearchObservationChipAssets assets = Find.GlobalAsset<ResearchObservationChipAssets>();
            bool empty = fillState == ChipFillState.Empty;

            ObservationType observationType = MaterialObservationChamberLookup.GetChamberType(propertyLabel);
            ApplyShape(assets, observationType, fillState, useEmptyDashedSprite);
            ApplyProperty(assets, propertyLabel);

            LabelText.text = string.IsNullOrEmpty(text) ? string.Empty : text;

            SetPickerChipDisabledVisual(!empty && locked);
        }

        // Applies the chip's appearance from the global visual asset.
        // observationType selects the sprite set; fillState selects within
        // it and drives the label color; text == null hides the label.
        // useEmptyDashedSprite swaps the per-type empty sprite for the
        // shared dashed-outline EmptySlotSprite when the state is Empty —
        // used by sample-panel slot chips to indicate "the hypothesis
        // requires a value here, but the player hasn't picked one yet."
        public void SetState(string text, ChipFillState fillState, bool locked, ObservationType observationType, bool useEmptyDashedSprite = false) {
            ResearchObservationChipAssets assets = Find.GlobalAsset<ResearchObservationChipAssets>();
            bool empty = fillState == ChipFillState.Empty;

            ApplyShape(assets, observationType, fillState, useEmptyDashedSprite);

            LabelText.text = string.IsNullOrEmpty(text) ? string.Empty : text;

            SetPickerChipDisabledVisual(!empty && locked);
        }

        // Applies the "already selected in the sample panel" greyed-out
        // look. Click gating is deliberately NOT done here — a greyed
        // wiki chip stays clickable, and clicking it removes the
        // selection; any inertness is the registering caller's
        // responsibility. Restoring uses the label color cached by the
        // last SetState, so callers that override the label color after
        // SetState (wiki characteristics chips) must not toggle this.
        public void SetPickerChipDisabledVisual(bool disabled) {
            ResearchObservationChipAssets asset = Find.GlobalAsset<ResearchObservationChipAssets>();
            ColorPalette2F palette = m_LastAssignedPalette;
            Color iconTint = m_LastAssignedIconColor;
            if (disabled) {
                palette.Content *= asset.SelectedBlend;
                palette.Background *= asset.SelectedBlend;
                iconTint *= asset.SelectedBlend;
            }

            if (!m_TintIcon) {
                Icon.color = iconTint;
            }

            UpdatePalette(palette);
        }

        public void ApplyUnselectableStyle() {
            ResearchObservationChipAssets asset = Find.GlobalAsset<ResearchObservationChipAssets>();
            ColorPalette2 palette = asset.UnselectablePalette;
            UpdatePalette(palette);
        }

        public void SetAsSelected() {

        }

        private void UpdatePalette(ColorPalette2F palette) {
            for(int i = 0; i < BackgroundTint.Length; i++) {
                BackgroundTint[i].color = palette.Background;
            }
            for (int i = 0; i < ContentTint.Length; i++) {
                ContentTint[i].color = palette.Content;
            }

            if (m_TintIcon) {
                Icon.SetColor(palette.Content);
            }
        }

        public void ApplyShape(ResearchObservationChipAssets assets, ObservationType observationType, ChipFillState fillState, bool useEmptyDashedSprite) {
            var shape = assets.GetShape(observationType);
            bool isVisible = true;
            Color iconColor = Color.white;

            if (fillState == ChipFillState.Empty) {
                isVisible = useEmptyDashedSprite;
                shape = assets.GetEmptyShape(observationType);
            } else if (observationType >= ObservationType.ConfirmedProperty && fillState != ChipFillState.Confirmed) {
                shape.Palette = assets.UnidentifiedPropertyPalette;
                iconColor = assets.UnidentifiedPropertyIconTint;
            }

            foreach (var graphic in BackgroundGraphics) {
                graphic.enabled = isVisible;
                graphic.sprite = shape.Fill;
            }
            foreach (var graphic in OutlineGraphics) {
                graphic.enabled = isVisible;
                graphic.sprite = shape.Outline;
            }

            Icon.enabled = isVisible;
            LabelText.enabled = isVisible;

            m_LastAssignedIconColor = Icon.color = iconColor;

            Positioning.SetHeightDelta((RectTransform)transform, shape.Height);
            Positioning.SetSizeDelta(Icon.rectTransform, shape.IconHeight, shape.IconHeight);
            
            m_LastAssignedPalette = shape.Palette;
            m_TintIcon = observationType < ObservationType.ConfirmedProperty;
            UpdatePalette(shape.Palette);
        }

        public void ApplyProperty(ResearchObservationChipAssets assets, MaterialPropertyLabel propertyType) {
            Icon.enabled = assets.TryGetIcon(propertyType, out Sprite sprite);
            Icon.sprite = sprite;
        }
    }
}
