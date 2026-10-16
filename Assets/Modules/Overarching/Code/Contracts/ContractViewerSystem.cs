using System;
using System.Collections;
using BeauUtil;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.Systems;
using SpaceFab.Save;
using SpaceFab.UI;

namespace SpaceFab.Overarching {
    public sealed class ContractViewerSystem : SystemComponent {
        public unsafe override void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&Update,
                new SysUpdate(GameLoopPhase.Update, -10, UpdateMasks.ContractSystemsMask),
                new SysPermissions()
                    .ReadWriteShared<ContractViewState>()
                    .ReadWriteShared<OverarchingContractLayout>()
            );
        }

        static private void Update(float dt) {

            Find.State(out ContractViewState view, out OverarchingContractLayout layout);

            UpdateQueuedContractView(view, layout);
        }

        static private void UpdateQueuedContractView(ContractViewState view, OverarchingContractLayout layout) {
            if (view.QueuedContractViewIndex >= 0) {
                if (view.QueuedContractViewIndex != view.ViewingContractIndex) {
                    UpdateContractView(view.CurrentMode, layout, view.QueuedContractViewIndex);
                    if (view.ViewingContractIndex >= 0) {
                        DitherFade.DissolveIn(layout.Contract.ContentsMask);
                    }
                    view.ViewingContractIndex = view.QueuedContractViewIndex;
                }

                view.QueuedContractViewIndex = -1;
            }
        }

        static private void UpdateContractView(OverarchingContractMode mode, OverarchingContractLayout layout, int contractIndex) {
            Find.State(out ChapterState chapterState, out ContractState contractState);

            StringHash32 contractId = chapterState.ChapterDefinition.AvailableContracts[contractIndex];
            ContractDef definition = Find.NamedAsset<ContractDef>(contractId);

            ContractUtility.LoadContractUI(layout.Contract, chapterState.ChapterIndex, definition);

            bool isCurrentContract = chapterState.LastSelectedContractIndex == contractIndex;

            if (isCurrentContract) {
                layout.Contract.SelectContractButton.gameObject.SetActive(false);
                layout.Contract.SignatureImage.enabled = true;

                layout.Contract.ShowDesignCompleted(Find.State<MinigameSaveStates>().Design.FoundValidSolution);
                layout.Contract.SelectContractButton.gameObject.SetActive(false);
            } else {
                layout.Contract.ShowDesignCompleted(false);
                layout.Contract.SelectContractButton.gameObject.SetActive(true);
            }

            layout.PrevContractButton.Interactable = contractIndex > 0;
            layout.NextContractButton.Interactable = contractIndex < chapterState.ChapterDefinition.AvailableContracts.Length - 1;
        }
    }

    static public partial class ContractSequenceUtility {
        
    }
}