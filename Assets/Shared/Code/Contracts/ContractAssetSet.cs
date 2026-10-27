using BeauUtil;
using FieldDay.Assets;
using Leaf;
using ScriptableBake;
using SpaceFab.Design;
using SpaceFab.Fabrication;
using SpaceFab.Fabrication.Sequence;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceFab
{
    [CreateAssetMenu(menuName ="SpaceFab/Overarching/Contract Asset Set")]
    public class ContractAssetSet : NamedAsset
    {
        /// <summary>
        /// Global scripting data.
        /// </summary>
        public LeafAsset Script;

        // Ordered list of Design levels for this contract. The player works through them in
        // sequence; the contract is solved only once every level is solved. Index 0 is the first
        // level. Was a single LevelData before contracts gained multiple Design levels.
        public LevelData[] DesignLevels;

        // Sequence definition for the Fabrication minigame under this contract. Populated into
        // SequenceState on minigame entry via FabricationMinigameState.ImportState.
        public FabricationLevel FabricationLevel;

        [AssetName(typeof(MicrogameStationConfig))] public StringHash32 StationConfigId;

        // For adjustment of fab cycle bucket thresholds per contract asset.
        public float fabCoefficient = 1f; // set default to 1
    }
}
