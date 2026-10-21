using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Data;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Design;
using SpaceFab.Overarching;
using SpaceFab.Save;
using System;
using System.Collections;
using UnityEngine;

namespace SpaceFab
{
    public class ContractState : SharedStateComponent, IRegistrationCallbacks, ISceneLoadDependency
    {
        [NonSerialized] public StringHash32 ContractId;
        [NonSerialized] public ContractDef ContractDefinition;
        [NonSerialized] public ContractAssetSet ContractAssets;
        [NonSerialized] public UniqueId16 ContractScriptHandle;

        [NonSerialized] public Routine LoadRoutine;

        public bool IsLoaded(SceneLoadFence fence) {
            return !LoadRoutine;
        }

        public void OnDeregister() {
            Game.Scenes.DeregisterLoadDependency(this);
        }

        public void OnRegister()
        {
            Game.Scenes.RegisterLoadDependency(this);
        }
    }

    public enum ContractLoadAction {
        Default = 0,
        ResetContractProgress
    }

    public static partial class ContractUtility {
        #region Data Load/Unload

        static public bool LoadContractData(ContractState contractState, int contractIndex, ContractLoadAction loadAction) {
            if (contractIndex < 0) {
                return UnloadContractData(contractState);
            }
            return LoadContractData(contractState, GetDefinition(contractIndex), loadAction);
        }

        static public bool LoadContractData(ContractState contractState, StringHash32 contractId, ContractLoadAction loadAction) {
            if (contractId.IsEmpty) {
                return UnloadContractData(contractState);
            }
            return LoadContractData(contractState, GetDefinition(contractId), loadAction);
        }

        static public bool LoadContractData(ContractState contractState, ContractDef definition, ContractLoadAction loadAction) {
            if (contractState.ContractId == definition.AssetId) {
                return false;
            }

            UnloadContractData(contractState);
            contractState.ContractId = definition.AssetId;
            contractState.LoadRoutine.Replace(contractState, LoadContractProcess(contractState, definition, loadAction));
            return true;
        }

        static private IEnumerator LoadContractProcess(ContractState contractState, ContractDef definition, ContractLoadAction loadAction) {
            Game.Assets.LoadStreamedPackage(definition.StreamedPack);
            while (Game.Assets.IsLoadingStreamedPackage(definition.StreamedPack)) {
                yield return null;
            }
            ContractAssetSet assetSet = Find.NamedAsset<ContractAssetSet>(definition.AssetSet);

            contractState.ContractDefinition = definition;
            contractState.ContractAssets = assetSet;
            contractState.ContractScriptHandle = ScriptDBUtility.Load(assetSet.Script);

            switch (loadAction) {
                case ContractLoadAction.ResetContractProgress: {
                    ResetProgress(contractState, Find.State<PlayerProgressState>());
                    break;
                }
            }
        }

        static public bool UnloadContractData(ContractState contractState) {
            if (contractState.ContractId.IsEmpty) {
                return false;
            }

            ScriptDBUtility.Unload(contractState.ContractScriptHandle);
            Game.Assets.UnloadStreamedPackage(contractState.ContractDefinition.StreamedPack);

            contractState.LoadRoutine.Stop();
            contractState.ContractDefinition = null;
            contractState.ContractAssets = null;
            contractState.ContractScriptHandle = default;
            contractState.ContractId = default;
            return true;
        }

        #endregion // Data Load/Unload

        static public bool LoadCurrentContract(ContractState contractState, ChapterState chapterState, ContractLoadAction loadAction) {
            return LoadContractData(contractState, ChapterUtility.GetSelectedContractId(chapterState), loadAction);
        }

        #region Player Progress

        static public void ResetProgress(ContractState contractState, PlayerProgressState playerProgress) {
            // Extract assets into game states
            var contractAssets = contractState.ContractAssets;
            // design level starts as initial config by default
            var minigameSaveState = Find.State<MinigameSaveStates>();
            MinigameSaveUtility.ClearMinigameState(minigameSaveState);

            // Seed every Design level under the contract. Each level gets its own grid + input
            // toggle defaults, all marked unsolved. The player works through them in order; the
            // active level is derived (first unsolved) at minigame entry.
            LevelData[] designLevels = contractAssets.DesignLevels;
            DesignSaveUtility.AllocLevels(minigameSaveState.Design, designLevels.Length);
            for (int i = 0; i < designLevels.Length; i++) {
                GridStackConfig gridConfig = designLevels[i].GetGridConfig();
                GridStackUtility.LoadConfig(ref minigameSaveState.Design.GridStacks[i], gridConfig);
                // Mirror the grid seed for toggle-input mode: walk the config's Input cells and copy
                // each DefaultInputState into the save state. Runtime ImportState reads from here.
                InputToggleUtility.SeedDefaultsFromConfig(ref minigameSaveState.Design.InputToggles[i], gridConfig);
            }

            // Pre-arm Research's FoundValidSolution when the player's existing knowledge already
            // covers every property requirement on the accepted contract. Unlike Design or
            // Fabrication, Research's "valid solution" is purely a knowledge-coverage check, so
            // it can be satisfied before the minigame is ever entered. The flag is read from
            // save by ResearchStateUtility.ImportState when the Research scene loads.
            if (ContractProgressUtility.IsContractSatisfied(playerProgress, contractState.ContractDefinition)) {
                minigameSaveState.Research.FoundValidSolution = true;
            }

            // If the selected contract unlocks the big battery, unlock the battery.
            if (contractState.ContractDefinition.UnlocksBigBattery()) {
                playerProgress.BigBatteryUnlocked = true;
            }
            if (contractState.ContractDefinition.UnlocksThermalChamber()) {
                playerProgress.ThermalChamberUnlocked = true;
            }
            if (contractState.ContractDefinition.UnlocksDopingChamber()) {
                playerProgress.DopingChamberUnlocked = true;
            }
            if (Find.State<ChapterState>().ChapterDefinition.UnlockSpecialProperties) {
                playerProgress.SpecialPropertiesUnlocked = true;
            }

            if (Game.SharedState.TryGet(out MinigameZonesState zonesState)) {
                MinigameZonesUtility.UpdateStatusFromSave(zonesState, minigameSaveState, Find.GlobalAsset<MinigameDependencyGraph>());
            }

            SaveUtility.Save(SaveSlot.Main);
        }

        static public void ApplyCompletedContractState(ContractState contractState, PlayerProgressState progressState, MinigameSaveStates saveStates) {
            // save elapsed cycles and funds
            progressState.ElapsedCycles += MinigameSaveUtility.CurrentTime(saveStates);

            int contractPayout = 0;
            if (contractState.ContractDefinition) {
                contractPayout = contractState.ContractDefinition.Payout();
            }
            progressState.Funds += contractPayout - MinigameSaveUtility.CurrentCosts(saveStates);
        }

        #endregion // Player Progress
    }
}