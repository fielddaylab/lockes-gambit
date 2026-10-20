using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI.Widgets;
using SpaceFab.Design;
using SpaceFab.Save;

namespace SpaceFab.Overarching {
    public sealed class OverarchingContractButtons : SharedStateComponent, IScenePreload {
        public GuiButton SubmitButton;
        public GuiButton ChangeButton;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Find.State(out ChapterState chapterState);
            ChangeButton.gameObject.SetActive(chapterState.LastSelectedContractIndex >= 0);
            SubmitButton.gameObject.SetActive(false);

            ChangeButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedMode = OverarchingContractMode.Change;
            });

            SubmitButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedMode = OverarchingContractMode.Complete;
            });

            return null;
        }
    }

    static public partial class OverarchingTransitions {
        static public void RefreshButtons(OverarchingContractButtons buttons, MinigameSaveStates saveStates) {
            bool allSolved = MinigameSaveUtility.AllSolved(saveStates);
            buttons.SubmitButton.gameObject.SetActive(allSolved);

            Find.State(out ContractState contracts);
            if (contracts.ContractId.IsEmpty) {
                buttons.ChangeButton.gameObject.SetActive(false);
            } else {
                buttons.ChangeButton.TextGraphic.SetText(contracts.ContractDefinition.Title());
                buttons.ChangeButton.gameObject.SetActive(true);
            }
        }
    }
}