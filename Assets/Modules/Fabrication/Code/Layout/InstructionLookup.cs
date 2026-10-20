using BeauUtil;
using FieldDay.Assets;
using SpaceFab.Fabrication.Stations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Layout
{
    /// <summary>
    /// Key type image the microgame will display
    /// </summary>
    public enum KeyImage
    {
        Space,
        LRArrows,
        FullArrows,
        FullArrowsMinusMiddle,
        Mouse,
        ADKeysAndSpace
    }

    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/InstructionLookup")]
    public class InstructionLookup : GlobalAsset
    {
        public InstructionSet FurnaceInstructions;

        public InstructionSet ResistInstructions;

        public InstructionSet PhotolithographyInstructions;

        public InstructionSet EtchInstructions;

        public InstructionSet IonInstructions;

        [Space(5)]
        [Header("Key Images")]
        public KeyImageEntry[] KeyImages;
    }

    /// <summary>
    /// Pairs a KeyImage with the sprite shown when an InstructionSet selects that key.
    /// Authored on InstructionLookup; resolved at runtime via InstructionLookupUtility.
    /// </summary>
    [System.Serializable]
    public struct KeyImageEntry
    {
        public KeyImage Key;
        public Sprite Sprite;
    }

    /// <summary>
    /// Instruction set class for use in displaying the correct image and instruction
    /// </summary>
    [System.Serializable]
    public class InstructionSet
    {
        public KeyImage UIKey;
        public string Instruction;
        public string Subtitle;
    }

    /// <summary>
    /// Utility class for InstructionsLookup
    /// </summary>
    public static class InstructionLookupUtility
    {
        /// <summary>
        /// Returns corresponding instruction set for the interfacer as described by the lookup
        /// </summary>
        public static InstructionSet LookupInstructions(SerializedHash32 stationID, InstructionLookup lookup)
        {
            if (stationID == FabricationConsts.FURNACE_STATION_ID) return lookup.FurnaceInstructions;
            else if (stationID == FabricationConsts.RESIST_STATION_ID) return lookup.ResistInstructions;
            else if (stationID == FabricationConsts.PHOTOLITHOGRAPHY_STATION_ID) return lookup.PhotolithographyInstructions;
            else if (stationID == FabricationConsts.ETCH_STATION_ID) return lookup.EtchInstructions;
            else if (stationID == FabricationConsts.ION_STATION_ID) return lookup.IonInstructions;
            else return null;
        }

        /// <summary>
        /// Returns the sprite authored for the given key image, or null if the lookup has no entry.
        /// </summary>
        public static Sprite LookupKeyImage(KeyImage key, InstructionLookup lookup)
        {
            if (lookup == null || lookup.KeyImages == null) return null;
            for (int i = 0; i < lookup.KeyImages.Length; i++)
            {
                if (lookup.KeyImages[i].Key == key) return lookup.KeyImages[i].Sprite;
            }
            return null;
        }
    }
}