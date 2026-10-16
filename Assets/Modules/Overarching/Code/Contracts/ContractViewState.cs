using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using SpaceFab.Design;
using SpaceFab.Save;

namespace SpaceFab.Overarching {
    [PreloadOrder(100)]
    public sealed class ContractViewState : SharedStateComponent, IScenePreload {
        [NonSerialized] public int CurrentContractIndex = -1;

        [NonSerialized] public int QueuedContractIndex = -1;
        [NonSerialized] public int QueuedContractScroll = 0;

        [NonSerialized] public ContractViewQueuedAction QueuedAction = ContractViewQueuedAction.None;

        [NonSerialized] public OverarchingContractMode CurrentMode;
        [NonSerialized] public OverarchingContractMode QueuedMode;
        [NonSerialized] public bool ChangedContract;

        [NonSerialized] public RingBuffer<int> AvailableContractIndices = new RingBuffer<int>(8);

        [NonSerialized] public Routine ProcessRoutine;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Find.State(out PlayerProgressState playerProgress, out ChapterState chapterState);
            ContractViewUtility.RebuildAvailableContracts(this, playerProgress, chapterState);
            return null;
        }
    }

    public enum ContractViewQueuedAction {
        None,
        Accept,
        ConfirmedAccept
    }

    static public partial class ContractViewUtility {
        static public void ApplyContract(int contractIndex) {
            Find.State(out ChapterState chapterState, out ContractState contractState);
            chapterState.LastSelectedContractIndex = contractIndex;
            SpacefabGame.Events.Dispatch(GameEvents.StartSelectContract, contractIndex.ToStringLookup());
            ContractUtility.LoadContractData(contractState, contractIndex, ContractLoadAction.ResetContractProgress);
        }

        static public void RebuildAvailableContracts(ContractViewState view, PlayerProgressState playerProgress, ChapterState chapterState) {
            view.AvailableContractIndices.Clear();
            for(int i = 0; i < chapterState.ChapterDefinition.AvailableContracts.Length; i++) {
                if (!PlayerProgressUtility.HasCompletedContract(playerProgress, chapterState.ChapterDefinition.AvailableContracts[i])) {
                    view.AvailableContractIndices.PushBack(i);
                }
            }

            Assert.True(view.AvailableContractIndices.Count > 0, "No contracts available? How is that possible?");
        }
    }
}