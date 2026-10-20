using BeauUtil;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Data;
using FieldDay.SharedState;
using SpaceFab.Materials;
using SpaceFab.Save;
using SpaceFab.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab
{
    public class PlayerProgressState : SharedStateComponent, ISaveStateChunkObject, IRegistrationCallbacks, ISaveStatePostLoad
    {
        #region Save State

        [NonSerialized] public StringHash32 RecentlyCompletedContract;

        [NonSerialized] public bool BigBatteryUnlocked;
        [NonSerialized] public bool ThermalChamberUnlocked;
        [NonSerialized] public bool DopingChamberUnlocked;
        [NonSerialized] public bool SpecialPropertiesUnlocked;

        // Tracks whether the one-shot wiki initial-unlocks pass has
        // already run for this save. OverarchingStartupSequenceSystem
        // applies WikiInitialUnlocksConfig.InitialUnlockedPages once
        // when this is false, then sets it true.
        [NonSerialized] public bool InitialUnlocksApplied;

        [NonSerialized] public int ElapsedCycles;
        [NonSerialized] public int Funds;

        [NonSerialized] public uint CompletedContractBuffer;
        [NonSerialized] public MaterialPropertyRecord[] MaterialPropertyBuffer;

        // Wiki pages the player has unlocked. Populated by WikiUtility.UnlockPage; read by
        // WikiAvailabilityUtility and WikiUtility's lock queries to decide which tabs/pages
        // are exposed. Account-scoped, so it persists across minigames.
        [NonSerialized] public HashSet<StringHash32> UnlockedWikiPages;

        #endregion // Save State

        [NonSerialized] public Dictionary<StringHash32, MaterialPropertyRecord> MaterialProperties;
        [NonSerialized] public HashSet<StringHash32> CompletedContractIds;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            CompletedContractIds = new HashSet<StringHash32>();
            MaterialProperties = new Dictionary<StringHash32, MaterialPropertyRecord>();
            UnlockedWikiPages ??= new HashSet<StringHash32>();
            SpacefabGame.SaveBuffer.RegisterHandler("PlayerProgressState", this);
            SpacefabGame.SaveBuffer.RegisterPostLoad(this);
        }

        #region Interfaces

        // ISaveStateChunkObject

        public void Read(object self, ref ByteReader reader, SaveStateChunkConsts consts)
        {
            UnlockedWikiPages ??= new HashSet<StringHash32>();
            UnlockedWikiPages.Clear();

            RecentlyCompletedContract = reader.Read<StringHash32>();

            int count = reader.Read<int>();
            for (int i = 0; i < count; i++)
            {
                UnlockedWikiPages.Add(reader.Read<StringHash32>());
            }
            ElapsedCycles = reader.Read<int>();
            Funds = reader.Read<int>();
            PlayerProgressUtility.UnpackCompletedContracts(this, reader.Read<uint>());
            PlayerProgressUtility.UnpackMaterialProperties(this, ref reader);
            // Appended at the tail; SaveVersion is -1 (in flux), so no
            // version gate. When SaveVersion is fixed, move into a
            // versioned slot.
            BigBatteryUnlocked = reader.Read<bool>();
            if (consts.Version >= 1) {
                ThermalChamberUnlocked = reader.Read<bool>();
                DopingChamberUnlocked = reader.Read<bool>();
            } else {
                ThermalChamberUnlocked = false;
                DopingChamberUnlocked = false;
            }

            if (consts.Version >= 2) {
                SpecialPropertiesUnlocked = reader.Read<bool>();
            } else {
                SpecialPropertiesUnlocked = false;
            }

            InitialUnlocksApplied = reader.Read<bool>();
            
        }

        public void Write(object self, ref ByteWriter writer, SaveStateChunkConsts consts)
        {
            writer.Write(RecentlyCompletedContract);

            int count = UnlockedWikiPages?.Count ?? 0;
            writer.Write(count);
            if (UnlockedWikiPages != null)
            {
                foreach (StringHash32 id in UnlockedWikiPages)
                {
                    writer.Write(id);
                }
            }
            writer.Write(ElapsedCycles);
            writer.Write(Funds);
            writer.Write(PlayerProgressUtility.PackCompletedContracts(this));
            PlayerProgressUtility.PackMaterialProperties(this, ref writer);
            writer.Write(BigBatteryUnlocked);
            writer.Write(ThermalChamberUnlocked);
            writer.Write(DopingChamberUnlocked);
            writer.Write(SpecialPropertiesUnlocked);
            writer.Write(InitialUnlocksApplied);
        }

        void ISaveStatePostLoad.PostLoad(SaveStateChunkConsts consts) {
            var chapterState = Find.State<ChapterState>();
            if (consts.Version < 1) {
                if (chapterState.ChapterIndex >= 1) {
                    ThermalChamberUnlocked = true;
                }
                if (chapterState.ChapterIndex >= 2) {
                    DopingChamberUnlocked = true;
                }
            }
            if (consts.Version < 2) {
                // UPGRADE
                if (chapterState.ChapterIndex >= 8) {
                    SpecialPropertiesUnlocked = true;
                }
            }
        }

        #endregion // Interfaces
    }

    public static class PlayerProgressUtility
    {
        public static bool HasCompletedContract(PlayerProgressState progressState, StringHash32 contractId)
        {
            if (progressState.CompletedContractIds.Contains(contractId))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Packs CompletedContractIds into a bitmask using ContractOrderAsset for stable indices.
        /// </summary>
        public static uint PackCompletedContracts(PlayerProgressState state)
        {
            var contractOrder = Find.GlobalAsset<ContractManifest>();
            state.CompletedContractBuffer = 0;
            for(int i = 0; i < contractOrder.Contracts.Length; i++) {
                StringHash32 contractId = contractOrder.Contracts[i];
                if (!contractId.IsEmpty && state.CompletedContractIds.Contains(contractId)) {
                    state.CompletedContractBuffer |= (1u << i);
                }
            }
            return state.CompletedContractBuffer;
        }

        /// <summary>
        /// Unpacks a saved bitmask back into CompletedContractIds using ContractOrderAsset.
        /// </summary>
        public static void UnpackCompletedContracts(PlayerProgressState state, uint mask)
        {
            var contractOrder = Find.GlobalAsset<ContractManifest>();
            state.CompletedContractIds.Clear();
            for (int i = 0; i < contractOrder.Contracts.Length; i++)
            {
                if ((mask & (1u << i)) != 0)
                {
                    state.CompletedContractIds.Add(contractOrder.Contracts[i]);
                }
            }
        }

        /// <summary>
        /// Returns true if the given persistent property has been confirmed for the material.
        /// For dynamic labels (PDopantFor / NDopantFor), contextMaterialId names the X
        /// in "P-Type Dopant for X". For static labels, contextMaterialId is ignored.
        /// Observation-only labels always return false.
        /// </summary>
        public static bool HasConfirmed(PlayerProgressState state, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            if (!state.MaterialProperties.TryGetValue(materialId, out var record))
            {
                return false;
            }
            return MaterialPropertyRecordUtility.Has(record, label, contextMaterialId);
        }

        /// <summary>
        /// Marks the given persistent property as confirmed for the material.
        /// Observation-only labels are silently ignored. For dynamic labels,
        /// contextMaterialId is required and resolved through MaterialOrderAsset.
        /// Idempotent (OR-mask semantics).
        /// </summary>
        public static void Confirm(PlayerProgressState state, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            state.MaterialProperties.TryGetValue(materialId, out var record);
            if (MaterialPropertyRecordUtility.TrySet(ref record, label, contextMaterialId))
            {
                state.MaterialProperties[materialId] = record;
            }
        }

        /// <summary>
        /// OR-merges a whole record of confirmed properties onto the material. Used to
        /// apply a pre-built property set - a baked knowledge snapshot, a restored save -
        /// in one step instead of replaying Confirm per label. Additive: bits already
        /// confirmed are never cleared. Returns true if anything new was confirmed.
        /// </summary>
        public static bool ConfirmAll(PlayerProgressState state, StringHash32 materialId, in MaterialPropertyRecord properties) {
            state.MaterialProperties.TryGetValue(materialId, out var record);
            MaterialPropertyRecord merged = record;
            MaterialPropertyRecordUtility.Merge(ref merged, properties);
            if (MaterialPropertyRecordUtility.AreEqual(record, merged)) {
                return false;
            }

            state.MaterialProperties[materialId] = merged;
            return true;
        }

        /// <summary>
        /// Stages MaterialProperties into MaterialPropertyBuffer (in MaterialOrderAsset order)
        /// and writes the buffer to the save stream.
        /// </summary>
        public static void PackMaterialProperties(PlayerProgressState state, ref ByteWriter writer)
        {
            var materialOrder = Find.GlobalAsset<MaterialOrderAsset>();
            int count = materialOrder.Count;
            if (state.MaterialPropertyBuffer == null || state.MaterialPropertyBuffer.Length != count)
            {
                state.MaterialPropertyBuffer = new MaterialPropertyRecord[count];
            }
            for (int i = 0; i < count; i++)
            {
                state.MaterialProperties.TryGetValue(materialOrder.GetId(i), out var record);
                state.MaterialPropertyBuffer[i] = record;
            }
            writer.WriteBuffer(state.MaterialPropertyBuffer);
        }

        /// <summary>
        /// Reads MaterialPropertyBuffer from the save stream and rebuilds MaterialProperties,
        /// skipping all-zero entries to keep the dictionary sparse.
        /// </summary>
        public static void UnpackMaterialProperties(PlayerProgressState state, ref ByteReader reader)
        {
            var materialOrder = Find.GlobalAsset<MaterialOrderAsset>();
            int count = materialOrder.Count;
            if (state.MaterialPropertyBuffer == null || state.MaterialPropertyBuffer.Length != count)
            {
                state.MaterialPropertyBuffer = new MaterialPropertyRecord[count];
            }
            reader.ReadBuffer(state.MaterialPropertyBuffer);
            state.MaterialProperties.Clear();
            for (int i = 0; i < count; i++)
            {
                var record = state.MaterialPropertyBuffer[i];
                if (!MaterialPropertyRecordUtility.IsEmpty(record))
                {
                    state.MaterialProperties[materialOrder.GetId(i)] = record;
                }
            }
        }
    }
}
