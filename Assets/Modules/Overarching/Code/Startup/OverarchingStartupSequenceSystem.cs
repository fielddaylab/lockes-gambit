using BeauUtil;
using FieldDay;
using FieldDay.Music;
using FieldDay.Scripting;
using FieldDay.Systems;
using FieldDay.UI;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Overarching {
    /// <summary>
    /// Top-level orchestrator for entering the overarching scene from a cold start or after
    /// a minigame: loads the current chapter, optionally plays the contract-completion sequence,
    /// loads available contracts, runs select/confirm/load for the active contract, then resumes
    /// OverarchingMask. Runs on Update at order 0 under SetupMask. Sequence:
    ///   1. Load the present chapter
    ///   2. Load current available contracts (parallel with completion sequence)
    ///   2b. If coming from previous chapter: run completion sequence
    ///   3. If no contract selected: run select sequence
    ///   4. Load selected contract
    /// </summary>
    public class OverarchingStartupSequenceSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.SetupMask),
                new SysPermissions()
                    .ReadWriteShared<OverarchingStartupSequenceState>()
                    .ReadWriteShared<ContractCompletionState>()
                    .ReadWriteShared<ContractSelectState>()
                    .ReadShared<ChapterState>()
                    .ReadWriteShared<ContractConfirmState>()
                    .ReadShared<SharedUIState>()
                    .ReadWriteShared<PlayerProgressState>()
                    .ReadWriteShared<ProgressMeterState>()
                    .ReadWriteShared<ContractLayoutState>()
                    .ReadShared<ContractState>()
            );
        }

        // Dispatches to the handler for the current startup phase.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out OverarchingStartupSequenceState startupState,
                out ContractCompletionState completionState,
                out ContractSelectState selectState
                );
            Find.State(
                out ChapterState chapterState,
                out ContractConfirmState confirmState,
                out SharedUIState uiState
                );
            Find.State(
                out PlayerProgressState progressState,
                out ProgressMeterState meterState,
                out ContractState contractState,
                out ContractLayoutState layoutState
                );

            // Gate: run only if we haven't finished startup and the UI isn't mid-load
            if (!(startupState.Phase != OverarchingStartupSequencePhase.Completed /* && !uiState.IsLoading */)) {
                return;
            }

            switch (startupState.Phase) {
                case OverarchingStartupSequencePhase.LoadCurrChapter:
                    ProcessLoadCurrChapter(startupState, completionState, progressState);
                    break;
                case OverarchingStartupSequencePhase.ContractCompletionSystem:
                    ProcessContractCompletion(startupState, completionState);
                    break;
                case OverarchingStartupSequencePhase.LoadCurrAvailableContracts:
                    ProcessLoadCurrAvailableContracts(startupState, selectState, chapterState);
                    break;
                case OverarchingStartupSequencePhase.ContractSelectSystem:
                    ProcessContractSelectSystem(startupState, selectState, confirmState);
                    break;
                case OverarchingStartupSequencePhase.ContractConfirmSystem:
                    var prevConfirmPhase = confirmState.Phase;
                    ProcessContractConfirmSystem(startupState, confirmState);
                    break;
                case OverarchingStartupSequencePhase.LoadSelectedContract:
                    ProcessLoadSelectedContract(startupState, meterState, contractState, chapterState, layoutState);
                    break;
                default:
                    break;
            }
        }

        // Kicks off ChapterLoadSystem. When it completes, branches to contract-completion or straight to loading contracts.
        static private void ProcessLoadCurrChapter(OverarchingStartupSequenceState startupState, ContractCompletionState completionState, PlayerProgressState progressState) {
            // Decide whether to run the contract-completion sequence
            if (!progressState.RecentlyCompletedContract.IsEmpty) {
                startupState.Phase = OverarchingStartupSequencePhase.ContractCompletionSystem;
                completionState.Phase = ContractCompletionPhase.Waiting;
                progressState.RecentlyCompletedContract = default;
            }
            else {
                startupState.Phase = OverarchingStartupSequencePhase.LoadCurrAvailableContracts;
            }
            GameLoop.ResumeUpdates(UpdateMasks.ContractSystemsMask);
        }

        // Coordinates with ContractCompletionSystem: trigger it on Waiting, continue on Completed.
        static private void ProcessContractCompletion(OverarchingStartupSequenceState startupState, ContractCompletionState completionState) {
            if (completionState.Phase == ContractCompletionPhase.Waiting) {
                // begin ContractCompletionSystem
                Debug.Log("[OverarchingStartupSequenceSystem] Begin ContractCompletionSystem");
                completionState.Phase = ContractCompletionPhase.BeginLoadFromPrevChapter;
            }
            else {
                if (completionState.Phase == ContractCompletionPhase.Completed) {
                    // next: load available contracts
                    startupState.Phase = OverarchingStartupSequencePhase.LoadCurrAvailableContracts;
                }
            }
        }

        // Starts loading available contracts, then either opens contract selection or jumps to loading the known-selected contract.
        static private void ProcessLoadCurrAvailableContracts(OverarchingStartupSequenceState startupState, ContractSelectState selectState, ChapterState chapterState) {
            // start load available contracts
            //Debug.Log("[OverarchingStartupSequenceSystem] ship menu displayed");
            //SpacefabGame.Events.Dispatch(GameEvents.ShipMenuDisplayed);

            // If no contract is selected yet, defer to selection; otherwise jump to loading the selected contract.
            if (chapterState.LastSelectedContractIndex == -1) {
                startupState.Phase = OverarchingStartupSequencePhase.ContractSelectSystem;
                selectState.Phase = ContractSelectPhase.Waiting;
                SpacefabGame.Events.Dispatch(GameEvents.OpenContractView);
            }
            else {
                // load selected contract
                startupState.Phase = OverarchingStartupSequencePhase.LoadSelectedContract;
            }
        }

        // Coordinates with ContractSelectSystem after available-contracts load finishes: trigger, then advance to confirm on Completed.
        static private void ProcessContractSelectSystem(OverarchingStartupSequenceState startupState, ContractSelectState selectState, ContractConfirmState confirmState) {
            // wait for LoadAvailableContracts routine to complete
            
            if (selectState.Phase == ContractSelectPhase.Waiting) {
                // begin ContractSelectSystem
                Debug.Log("[OverarchingStartupSequenceSystem] Begin ContractSelectSystem");
                selectState.Phase = ContractSelectPhase.Loading;
            }
            else {
                if (selectState.Phase == ContractSelectPhase.Completed) {
                    //  confirm selected contract
                    ScriptUtility.Trigger("OnContractSelected");
                    confirmState.Phase = ContractConfirmPhase.Waiting;
                    startupState.Phase = OverarchingStartupSequencePhase.ContractConfirmSystem;
                }
            }
        }

        // Coordinates with ContractConfirmSystem: trigger on Waiting, advance to load on Completed.
        static private void ProcessContractConfirmSystem(OverarchingStartupSequenceState startupState, ContractConfirmState confirmState) {
            if (confirmState.Phase == ContractConfirmPhase.Waiting) {
                // begin contract confirmation
                Debug.Log("[OverarchingStartupSequenceSystem] Begin ContractConfirmSystem");
                confirmState.Phase = ContractConfirmPhase.Confirming;
            }
            else {
                if (confirmState.Phase == ContractConfirmPhase.Completed) {
                    // load selected contract
                    startupState.Phase = OverarchingStartupSequencePhase.LoadSelectedContract;
                }
            }
        }

        // Coordinates with ContractLoadSystem: trigger, wait for completion, then finalize startup.
        static private void ProcessLoadSelectedContract(OverarchingStartupSequenceState startupState, ProgressMeterState meterState, ContractState contractState, ChapterState chapterState, ContractLayoutState layoutState) {
            ContractUtility.LoadContractData(contractState, ChapterUtility.GetSelectedContractId(chapterState));
            if (!chapterState.LoadRoutine) {
                // refresh progress meter to update funds and cycles
                meterState.NeedsRefresh = true;
                Complete(startupState, contractState, layoutState);
                GameLoop.SuspendUpdates(UpdateMasks.ContractSystemsMask);
            }
        }

        // Marks startup complete and resumes the overarching scene's normal update mask.
        static private void Complete(OverarchingStartupSequenceState startupState, ContractState contractState, ContractLayoutState layoutState) {
            startupState.Phase = OverarchingStartupSequencePhase.Completed;
            SpacefabGame.Events.Dispatch(GameEvents.ShipMenuDisplayed);
            GameLoop.ResumeUpdates(UpdateMasks.OverarchingMask);

            // Reveal the view-contract button now that an active contract is settled — either just
            // selected or carried over from the last visit. This phase is only reached after any
            // previous-chapter completion sequence has finished, so the button never appears
            // alongside the completed-contract presentation.
            layoutState.ViewCurrContractButton.gameObject.SetActive(!contractState.ContractId.IsEmpty);
            layoutState.SetViewCurrContractLabel(contractState.ContractId);

            // Fire the Leaf trigger now that the overarching scene is fully loaded and interactive,
            // letting narrative scripts respond to entering the scene (e.g. gating a node on
            // IsSolutionFoundFor for completed minigames).
            OverarchingSubmitButtonUtility.Refresh(Find.State<OverarchingSubmitChapterSequenceState>(), Find.State<MinigameSaveStates>());
            ScriptUtility.Trigger(ScriptTriggers.OnOverarchingLoaded);
            Debug.Log("[OverarchingStartupSequenceSystem] Overarching Startup Sequence Completed");
        }
    }
}
