using FieldDay.Assets;
using SpaceFab.Fabrication.Sequence;
using System;
using UnityEngine;

namespace SpaceFab.Fabrication {
    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/Wafer Display Lookup")]
    public class WaferDisplayLookup : GlobalAsset {
        public WaferDisplayData[] WaferDisplayEntries;
	}

    // ehhh querying this could be faster if it was split into separate arrays per layer
    // whatever this is fine :)
    [Serializable]
    public struct WaferDisplayData {
        [Header("Last Step Done")]
        public SequenceChunk Layer;
        public SequenceStepID Step;
        public Sprite BaseSprite;
        public Sprite TintSprite;
        //public Color TintColor;
    }
}