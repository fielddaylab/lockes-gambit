using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Design;
using SpaceFab.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceFab.Overarching
{
    public enum ContractSelectPhase
    {
        Waiting,
        Loading,
        PresentAvailableContracts,
        SelectContract,
        Completed
    }

    /// <summary>
    /// Tracks the contract-selection UI flow: the current phase, which of the chapter's
    /// still-uncompleted contracts the player is browsing, and whether they have confirmed.
    /// </summary>
    public class ContractSelectState : SharedStateComponent, IRegistrationCallbacks
    {
        public ContractSelectPhase Phase;
        // Index into AvailableContractIndices, NOT into ChapterDef.AvailableContracts.
        // Translate with ContractSelectUtility.ToRawIndex before touching anything that
        // expects a raw chapter index (LastSelectedContractIndex, ApplyContractByIndex, analytics).
        public int SelectedContractIndex;
        public bool SelectedContractIndexChanged;
        public bool SelectionConfirmed;
        public TMP_Text ContractTitleText;

        // Raw indices into ChapterDefinition.AvailableContracts for the contracts the player
        // has not yet completed. Rebuilt by ContractSelectUtility.RebuildAvailableContracts
        // each time the selection UI opens.
        [NonSerialized] public RingBuffer<int> AvailableContractIndices;

        public void OnRegister() {
            AvailableContractIndices = new RingBuffer<int>(4, RingBufferMode.Expand);
        }

        public void OnDeregister() {
        }
    }

    /// <summary>
    /// Builds and queries the chapter's available-contract list, and runs the routine that
    /// presents it. The completed-contract filter lives here and nowhere else.
    /// </summary>
    public static class ContractSelectUtility
    {
        #region Available Contracts

        // Refills AvailableContractIndices with the raw indices of every contract in the current
        // chapter the player has not already completed. Idempotent - safe to call from any flow
        // that is about to read the list.
        public static void RebuildAvailableContracts(ContractSelectState selectState, ChapterState chapterState, PlayerProgressState progressState) {
            selectState.AvailableContractIndices.Clear();

            StringHash32[] chapterContracts = chapterState.ChapterDefinition.AvailableContracts;
            for (int i = 0; i < chapterContracts.Length; i++) {
                if (!PlayerProgressUtility.HasCompletedContract(progressState, chapterContracts[i])) {
                    selectState.AvailableContractIndices.PushBack(i);
                }
            }

            // A chapter whose contracts are all already completed has nothing to offer - that is
            // a chapter-definition authoring error, not a runtime state to recover from.
            Assert.True(selectState.AvailableContractIndices.Count > 0, "Chapter '{0}' has no uncompleted contracts available", chapterState.ChapterId);
        }

        public static int AvailableCount(ContractSelectState selectState) {
            return selectState.AvailableContractIndices.Count;
        }

        // Maps a filtered selection index back to its index in ChapterDef.AvailableContracts.
        public static int ToRawIndex(ContractSelectState selectState, int filteredIndex) {
            Assert.True(filteredIndex >= 0 && filteredIndex < selectState.AvailableContractIndices.Count, "Filtered contract index {0} out of range", filteredIndex);
            return selectState.AvailableContractIndices[filteredIndex];
        }

        // Maps a raw ChapterDef.AvailableContracts index into filtered space.
        // Returns -1 when the contract is not on offer (already completed, or no index set).
        public static int ToFilteredIndex(ContractSelectState selectState, int rawIndex) {
            for (int i = 0, count = selectState.AvailableContractIndices.Count; i < count; i++) {
                if (selectState.AvailableContractIndices[i] == rawIndex) {
                    return i;
                }
            }
            return -1;
        }

        public static StringHash32 GetContractId(ContractSelectState selectState, ChapterState chapterState, int filteredIndex) {
            return chapterState.ChapterDefinition.AvailableContracts[ToRawIndex(selectState, filteredIndex)];
        }

        #endregion // Available Contracts

        public static IEnumerator PresentAvailableRoutine(ContractSelectState selectState, ContractLayoutState layoutState, ChapterState chapterState, PlayerProgressState playerProgress)
        {
            layoutState.FaderGroup.alpha = 1;
            layoutState.FaderGroup.blocksRaycasts = true;
            layoutState.ConfirmContractButton.gameObject.SetActive(false);

            yield return 0.5f;

            // Reopen on the contract the player last accepted so the selection index and the
            // panel agree — the change-contract flow compares the two to detect an actual swap.
            // LastSelectedContractIndex is a raw chapter index, so translate it into the filtered
            // list; fall back to the first offer when nothing is accepted yet (-1) or the accepted
            // contract is no longer on offer.
            int index = ContractSelectUtility.ToFilteredIndex(selectState, chapterState.LastSelectedContractIndex);
            if (index < 0) {
                index = 0;
            }

            selectState.SelectedContractIndex = index;
            selectState.SelectionConfirmed = false;
            selectState.SelectedContractIndexChanged = true;

            layoutState.ChangeContractButton.gameObject.SetActive(false);

            layoutState.SelectionCanvasGroup.alpha = 0;

            ContractDef contract = ContractUtility.GetDefinition(ContractSelectUtility.GetContractId(selectState, chapterState, index));
            ContractUtility.LoadContractUI(layoutState.SelectionContractUI, chapterState.ChapterIndex, contract);
            
            layoutState.SelectionContractUI.gameObject.SetActive(true);
            layoutState.SelectionCanvasGroup.blocksRaycasts = true;

            yield return Routine.Combine(
                layoutState.SelectionCanvasGroup.FadeTo(1, 1f)
                );

            yield return 0.5f;

            ScriptUtility.Trigger("OnContractSelectOpen");
        }
    }
}