using FieldDay;
using FieldDay.Assets;
using SpaceFab.Materials;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// Which of an ObservationSpritePair's three sprites a chip wears.
    /// Empty is a placeholder the player hasn't filled in; Filled is a
    /// known value; Confirmed is a known value the game has verified, and
    /// carries the green checkmark.
    /// </summary>
    public enum ChipFillState {
        Empty,
        Filled,
        Confirmed,
    }

    /// <summary>
    /// Global lookup the observation chip widget reads at render time.
    /// Holds a per-ObservationType (empty, filled) sprite pair plus the
    /// two label-text colors that are shared across every observation
    /// type. Authoring stays in the asset; the chip prefab itself
    /// carries only inspector refs to its Image / TMP_Text / overlay
    /// GameObject. Adding a new observation sprite set means editing
    /// this asset, not every chip prefab in the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceFab/Research/Observation Chip Visuals")]
    public class ResearchObservationChipAssets : GlobalAsset
    {
        [Serializable]
        public struct ObservationStyle
        {
            public ObservationType ObservationType;
            public Sprite Background;
            public Sprite Outline;
            public ColorPalette2 BaseColorPalette;
            public float Height; // height of chip
            public float IconHeight;
            public Sprite DefaultIcon;
        }

        [Serializable]
        public struct ObservationSpriteOverride {
            public MaterialPropertyLabel Label;
            public Sprite Icon;
        }

        [SerializeField] private ObservationStyle[] m_ObservationSprites;
        [SerializeField] private ObservationSpriteOverride[] m_IconOverrides;

        public ColorPalette2 UnselectablePalette;
        public Color SelectedBlend;

        [Header("Property Chips")]
        public ColorPalette2 UnidentifiedPropertyPalette;
        public Color UnidentifiedPropertyIconTint;
        public Sprite UnknownPropertyIcon;

        [Header("Empty Chip")]
        public Sprite EmptyBackground;
        public Sprite EmptyOutline;
        public ColorPalette2 EmptyColorPalette;

        private ObservationStyle[] m_StyleLookup;
        private Sprite[] m_IconLookup;

        public override void Mount()
        {
            m_StyleLookup = new ObservationStyle[(int) ObservationType.Component + 1];
            foreach(var component in m_ObservationSprites) {
                m_StyleLookup[(int)component.ObservationType] = component;
            }

            m_IconLookup = new Sprite[(int) MaterialPropertyLabel.HighMobilitySemiconductor + 1];
            foreach(var iconOverride in m_IconOverrides) {
                m_IconLookup[(int)iconOverride.Label] = iconOverride.Icon;
            }

            for(int i = 0; i < m_IconLookup.Length; i++) {
                if (m_IconLookup[i]) {
                    continue;
                }
                ObservationType obsType = MaterialObservationChamberLookup.GetChamberType((MaterialPropertyLabel) i);
                Sprite icon = m_StyleLookup[(int)obsType].DefaultIcon;
                m_IconLookup[i] = icon;
            }
        }

        public override void Unmount()
        {
            m_StyleLookup = null;
            m_IconLookup = null;
        }

        /// <summary>
        /// Retrieves the shape sprites for the given observation type.
        /// </summary>
        public ObservationChipShape GetShape(ObservationType observationType) {
            if (m_StyleLookup != null) {
                var style = m_StyleLookup[(int)observationType];
                ObservationChipShape shape;
                shape.Fill = style.Background;
                shape.Outline = style.Outline;
                shape.Height = style.Height;
                shape.IconHeight = style.IconHeight;
                shape.Palette = style.BaseColorPalette;
                return shape;
            } else {
                return default;
            }
        }

        /// <summary>
        /// Retrieves the shape sprites for the given observation type.
        /// </summary>
        public ObservationChipShape GetEmptyShape(ObservationType observationType) {
            if (m_StyleLookup != null) {
                var style = m_StyleLookup[(int)observationType];
                ObservationChipShape shape;
                shape.Fill = EmptyBackground;
                shape.Outline = EmptyOutline;
                shape.Height = style.Height;
                shape.IconHeight = style.IconHeight;
                shape.Palette = EmptyColorPalette;
                return shape;
            } else {
                return default;
            }
        }

        /// <summary>
        /// Retrieves the default color palette for the 
        /// </summary>
        public ColorPalette2 GetColorPalette(ObservationType observationType) {
            if (m_StyleLookup != null) {
                var style = m_StyleLookup[(int)observationType];
                return style.BaseColorPalette;
            }
            return default;
        }

        /// <summary>
        /// Attempts to retrieve the default icon for the given property type.
        /// </summary>
        public bool TryGetIcon(ObservationType observationType, out Sprite sprite)  {
            if (m_StyleLookup != null) {
                sprite = m_StyleLookup[(int)observationType].DefaultIcon;
            } else {
                sprite = null;
            }
            return sprite != null;
        }

        /// <summary>
        /// Attempts to retrieve the icon for the given property label.
        /// </summary>
        public bool TryGetIcon(MaterialPropertyLabel propertyLabel, out Sprite sprite) {
            if (m_IconLookup != null) {
                sprite = m_IconLookup[(int)propertyLabel];
            } else {
                sprite = null;
            }

            return sprite != null;
        }
    }

    public struct ObservationChipShape {
        public Sprite Fill;
        public Sprite Outline;
        public ColorPalette2 Palette;
        public float Height;
        public float IconHeight;
    }
}
