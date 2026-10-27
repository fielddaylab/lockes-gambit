using BeauUtil;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Music;
using Leaf;
using SpaceFab.Design;
using SpaceFab.Materials;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace SpaceFab
{
    [CreateAssetMenu(menuName = "SpaceFab/Chapter Asset")]
    public class ChapterDef : NamedAsset
    {
        [Header("Contracts")]
        [AssetName(typeof(ContractDef))] public StringHash32[] AvailableContracts;
        [SerializeField, Range(0f, 1f)] public float[] GlitchChances;

        [Header("Materials")]
        [AssetName(typeof(MaterialAsset)), FormerlySerializedAs("m_availableMaterials")] public StringHash32[] AvailableMaterials;
        [AssetName(typeof(MaterialAsset)), FormerlySerializedAs("m_excludeFromResearch")] public StringHash32[] ExcludeFromResearch;

        [Header("Unlocks")]
        public bool UnlockSpecialProperties;

        [Header("Assets")]
        [StreamedPackId("*Comic*")] public StringHash32 ComicPackage;
        public LeafAsset Script;
        public Material SkyboxMaterial;
        public float SkyboxRotation;

        [Header("Playlists")]
        [AssetName(typeof(MusicAsset))] public StringHash32 MusicPlaylist;

        [Header("Hazards")]
        public string HazardHeader;
        [TextArea(2, 4)] public string HazardDescription;
        public int HazardCost;
        public int HazardTime;

        [Header("Metrics")]
        public int CumulativeMinProfit;
        public int CumulativeMaxTime;
    }
}