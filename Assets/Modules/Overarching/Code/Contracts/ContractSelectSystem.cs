using FieldDay;
using FieldDay.Systems;
using FieldDay.Scripting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SpaceFab.Overarching {
    /// <summary>
    /// Runs the contract-selection UI flow: presents available contracts, tracks the selected
    /// index, keeps the confirm button in sync, and advances to Completed once the selection
    /// is confirmed. Runs on Update at order -10 under ContractSystemsMask.
    /// </summary>
    public class ContractSelectSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, -10, UpdateMasks.ContractSystemsMask),
                new SysPermissions()
                    .ReadWriteShared<ContractSelectState>()
                    .ReadWriteShared<ContractLayoutState>()
                    .ReadShared<ChapterState>()
                    .ReadShared<PlayerProgressState>()
            );
        }

        // Dispatches to the handler for the current select phase.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ContractSelectState selectState,
                out ContractLayoutState layoutState,
                out ChapterState chapterState,
                out PlayerProgressState progressState
                );

            switch (selectState.Phase) {
                case ContractSelectPhase.Loading:
                    ProcessLoading(selectState, layoutState, chapterState, progressState);
                    break;
                case ContractSelectPhase.PresentAvailableContracts:
                    ProcessPresentAvailableContracts(selectState, layoutState);
                    break;
                case ContractSelectPhase.SelectContract:
                    ProcessSelectContract(selectState, layoutState, chapterState);
                    break;
                default:
                    break;
            }
        }

        // Rebuilds the available-contract list, kicks off the present routine, and advances the phase.
        static private void ProcessLoading(ContractSelectState selectState, ContractLayoutState layoutState, ChapterState chapterState, PlayerProgressState progressState) {
            // Drop already-completed contracts before anything reads the list - everything
            // downstream browses and confirms against the filtered set.
            ContractSelectUtility.RebuildAvailableContracts(selectState, chapterState, progressState);

            layoutState.SelectionRoutine.Replace(ContractSelectUtility.PresentAvailableRoutine(selectState, layoutState, chapterState, progressState));
            selectState.Phase = ContractSelectPhase.PresentAvailableContracts;
        }

        // Waits for the presentation routine to finish, then allows contract selection.
        static private void ProcessPresentAvailableContracts(ContractSelectState selectState, ContractLayoutState layoutState) {
            if (!layoutState.SelectionRoutine.Exists()) {
                selectState.Phase = ContractSelectPhase.SelectContract;
            }
        }

        // Reflects the player's ongoing selection in the UI, refreshes contract data on change, and completes on confirm.
        static private void ProcessSelectContract(ContractSelectState selectState, ContractLayoutState layoutState, ChapterState chapterState) {
            layoutState.ConfirmContractButton.gameObject.SetActive(ScriptUtility.CurrentThreadCount == 0);

            // When the selected contract changes, refresh its detail UI
            if (selectState.SelectedContractIndexChanged) {
                layoutState.PrevContractButton.gameObject.SetActive(selectState.SelectedContractIndex > 0);
                layoutState.NextContractButton.gameObject.SetActive(selectState.SelectedContractIndex < ContractSelectUtility.AvailableCount(selectState) - 1);
                // Compare in raw index space - LastSelectedContractIndex is a raw chapter index,
                // and its -1 "nothing accepted" sentinel must never match a valid selection.
                layoutState.SelectionContractUI.SignatureImage.fillAmount = chapterState.LastSelectedContractIndex == ContractSelectUtility.ToRawIndex(selectState, selectState.SelectedContractIndex) ? 1 : 0;

                ContractUtility.LoadContractUI(layoutState.SelectionContractUI,
                    chapterState.ChapterIndex,
                    ContractUtility.GetDefinition(ContractSelectUtility.GetContractId(selectState, chapterState, selectState.SelectedContractIndex)));
                selectState.SelectedContractIndexChanged = false;
            }

            // Confirmed — advance the phase
            if (selectState.SelectionConfirmed) {
                Debug.Log("Confirm selection");
                // Log the raw chapter index so contract_id keeps the same meaning it had before
                // the list was filtered.
                SpacefabGame.Events.Dispatch(GameEvents.AcceptContract, ContractSelectUtility.ToRawIndex(selectState, selectState.SelectedContractIndex).ToString());
                ScriptUtility.Trigger("OnContractAccept");
                selectState.Phase = ContractSelectPhase.Completed;

                layoutState.SetViewCurrContractLabel(ContractSelectUtility.GetContractId(selectState, chapterState, selectState.SelectedContractIndex));
            }
        }
    }
}
