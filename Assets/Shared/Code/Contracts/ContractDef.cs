using BeauUtil;
using FieldDay.Assets;
using SpaceFab.Design;
using SpaceFab.Materials;
using System;
using UnityEngine;

namespace SpaceFab
{
    [CreateAssetMenu(menuName = "SpaceFab/Contract Asset")]
    public class ContractDef : NamedAsset
    {
        [SerializeField] private MaterialPropertyCheck[] m_requiredMaterialProperties;
        // Subset of m_requiredMaterialProperties (same asset references) that the Supply Chain
        // shopping list should ignore. Research still uses the full required set; only Supply
        // subtracts these. Match is by asset reference.
        [SerializeField] private MaterialPropertyCheck[] m_omitFromSupplyRequirements;
        [SerializeField] private string m_title;
        [SerializeField, Multiline] private string m_description;
        [SerializeField] private SerializedHash32 m_client;
        [SerializeField] private int m_payout;
        [SerializeField] private int m_expectedDuration;
        [SerializeField] private int m_expectedProfit;
        [SerializeField] private int m_minimumProfit;
        [SerializeField] private ContractClass m_contractType;
        [SerializeField] private ContractDifficulty m_difficulty;

        [SerializeField] private bool m_unlocksBigBattery; // whether Research unlocks the big battery
        [SerializeField] private bool m_unlocksThermalChamber;
        [SerializeField] private bool m_unlocksDopingChamber;

        [StreamedPackId] public StringHash32 StreamedPack;
        [AssetName(typeof(ContractAssetSet))] public StringHash32 AssetSet;

        public MaterialPropertyCheck[] RequiredMaterialProperties() { return m_requiredMaterialProperties; }
        public MaterialPropertyCheck[] OmitFromSupplyRequirements() { return m_omitFromSupplyRequirements; }
        // public StringHash32[] RequiredResearchMaterials() { return m_requiredResearchMaterials; }
        public string Title() { return m_title; }
        public string Description() { return m_description; }
        public StringHash32 ClientId() { return m_client; }
        public int Payout() { return m_payout; }
        public int ExpectedDuration() { return m_expectedDuration; }
        public int ExpectedProfit() { return m_expectedProfit; }
        public int MinimumProfit() { return m_minimumProfit; }
        public ContractClass ContractClass() { return m_contractType; }
        public ContractDifficulty Difficulty() { return m_difficulty; }
        public bool UnlocksBigBattery() { return m_unlocksBigBattery; }
        public bool UnlocksThermalChamber() { return m_unlocksThermalChamber; }
        public bool UnlocksDopingChamber() { return m_unlocksDopingChamber; }
    }

    public enum ContractClass : byte {
        Basic,
        Materials,
        Design
    }

    public enum ContractDifficulty : byte {
        Low,
        Medium,
        High
    }
}