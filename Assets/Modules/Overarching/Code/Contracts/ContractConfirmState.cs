using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using SpaceFab.Design;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Overarching
{
    public enum ContractConfirmPhase
    {
        Waiting,
        Confirming,
        Completed
    }

    public class ContractConfirmState : SharedStateComponent, IRegistrationCallbacks
    {
        public ContractConfirmPhase Phase;

        public Routine ConfirmRoutine;

        public void OnDeregister()
        {
            ConfirmRoutine.Stop();
        }

        public void OnRegister()
        {
        }
    }

    public static class ContractConfirmUtility
    {
        public static IEnumerator ConfirmContractRoutine(ContractConfirmState confirmState, ContractSelectState selectState, ContractLayoutState layoutState, ChapterState chapterState, SharedUIState sharedUIState, PlayerProgressState playerProgress, ContractState contractState)
        {
            // Apply the selected contract's data (active contract id, loaded assets, seeded minigame save).
            // SelectedContractIndex is filtered; ApplyContractByIndex records a raw chapter index
            // into LastSelectedContractIndex, which is persisted and resolved back on load.
            yield return ApplyContractByIndex(chapterState, playerProgress, contractState,
                ContractSelectUtility.ToRawIndex(selectState, selectState.SelectedContractIndex));

            float fillAmount = 0;
            while (fillAmount < 1)
            {
                fillAmount += Time.deltaTime;
                layoutState.SelectionContractUI.SignatureImage.fillAmount = fillAmount;
                yield return null;
            }

            yield return 0.5f;

            yield return Routine.Combine(
                layoutState.SelectionCanvasGroup.FadeTo(0, 1f)
            );

            layoutState.SelectionCanvasGroup.blocksRaycasts = false;

            yield return 0.5f;

            layoutState.FaderGroup.alpha = 0;
            layoutState.FaderGroup.blocksRaycasts = false;

            confirmState.Phase = ContractConfirmPhase.Completed;
        }

        // Applies the contract at the given index into the chapter's available-contracts bundle: records it
        // as the active contract, loads its asset scene, and seeds the minigame save state from the
        // contract's assets. This is the data core shared by ConfirmContractRoutine and the debug
        // "set contract" tool — no UI, no phase transition.
        public static IEnumerator ApplyContractByIndex(ChapterState chapterState, PlayerProgressState playerProgress, ContractState contractState, int contractIndex)
        {
            chapterState.LastSelectedContractIndex = contractIndex;
            StringHash32 contractId = chapterState.ChapterDefinition.AvailableContracts[contractIndex];

            ContractUtility.LoadContractData(contractState, contractId);
            while(contractState.LoadRoutine) {
                yield return null;
            }

            // Extract assets into game states
            var contractAssets = contractState.ContractAssets;
            // design level starts as initial config by default
            var minigameSaveState = Find.State<MinigameSaveStates>();
            MinigameSaveUtility.ClearMinigameState(minigameSaveState);
            MinigameZonesUtility.UpdateStatusFromSave(Find.State<MinigameZonesState>(), minigameSaveState, Find.GlobalAsset<MinigameDependencyGraph>());

            // Seed every Design level under the contract. Each level gets its own grid + input
            // toggle defaults, all marked unsolved. The player works through them in order; the
            // active level is derived (first unsolved) at minigame entry.
            LevelData[] designLevels = contractAssets.DesignLevels;
            DesignSaveUtility.AllocLevels(minigameSaveState.Design, designLevels.Length);
            for (int i = 0; i < designLevels.Length; i++)
            {
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
            if (ContractProgressUtility.IsContractSatisfied(playerProgress, contractState.ContractDefinition))
            {
                minigameSaveState.Research.FoundValidSolution = true;
            }

            // If the selected contract unlocks the big battery, unlock the battery.
            if (contractState.ContractDefinition.UnlocksBigBattery())
            {
                playerProgress.BigBatteryUnlocked = true;
            }
            if (contractState.ContractDefinition.UnlocksThermalChamber())
            {
                playerProgress.ThermalChamberUnlocked = true;
            }
            if (contractState.ContractDefinition.UnlocksDopingChamber())
            {
                playerProgress.DopingChamberUnlocked = true;
            }
            if (chapterState.ChapterDefinition.UnlockSpecialProperties)
            {
                playerProgress.SpecialPropertiesUnlocked = true;
            }

            SaveUtility.Save(SaveSlot.Main);
        }
    }
}
