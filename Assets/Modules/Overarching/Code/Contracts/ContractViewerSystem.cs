using System;
using System.Collections;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using FieldDay.UI;
using SpaceFab.Save;
using SpaceFab.UI;

namespace SpaceFab.Overarching {
    public sealed class ContractViewerSystem : SystemComponent {
        public unsafe override void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&Update,
                new SysUpdate(GameLoopPhase.Update, -10, UpdateMasks.OverarchingMask),
                new SysPermissions()
                    .ReadWriteShared<ContractViewState>()
                    .ReadWriteShared<OverarchingContractLayout>()
            );
        }

        static private void Update(float dt) {
            Find.State(out ContractViewState view, out OverarchingContractLayout layout);

            ProcessQueuedMode(view, layout);
            if (view.CurrentMode != OverarchingContractMode.Closed) {
                ProcessQueuedContractIndex(view, layout);
                ProcessQueuedAction(view, layout);
            }
        }

        static private void ProcessQueuedMode(ContractViewState view, OverarchingContractLayout layout) {
            if (view.QueuedMode == view.CurrentMode) {
                return;
            }

            OverarchingContractMode prevMode = view.CurrentMode;
            view.CurrentMode = view.QueuedMode;
            Assert.True(view.QueuedMode == OverarchingContractMode.Closed || prevMode == OverarchingContractMode.Closed, "Must transition between closed and active modes");

            view.QueuedAction = ContractViewQueuedAction.None;
            view.QueuedContractScroll = 0;

            if (view.QueuedMode == OverarchingContractMode.Closed) {
                layout.AnimationRoutine.Replace(layout, layout.FadeOut(prevMode, view.ChangedContract));
                view.ChangedContract = false;
                view.QueuedContractIndex = -1;
            } else {
                view.ChangedContract = false;

                layout.Prepare(view.CurrentMode);

                Find.State(out ChapterState chapterState);
                switch (view.CurrentMode) {
                    case OverarchingContractMode.Change: {
                        view.QueuedContractIndex = chapterState.LastSelectedContractIndex;
                        break;
                    }
                    case OverarchingContractMode.SelectNew: {
                        view.QueuedContractIndex = view.AvailableContractIndices[0];
                        view.ProcessRoutine.Replace(view, AnimateSelectNew(view, layout));
                        break;
                    }
                }

                layout.AnimationRoutine.Replace(layout, layout.FadeIn());

                SpacefabGame.Events.Dispatch(GameEvents.OpenContractView);
            }
        }

        #region Contracts

        static private void ProcessQueuedContractIndex(ContractViewState view, OverarchingContractLayout layout) {
            if (view.QueuedContractScroll != 0) {
                int localIndex = GetFilteredIndex(view, view.CurrentContractIndex);
                localIndex = Math.Max(0, Math.Min(localIndex + view.QueuedContractScroll, view.AvailableContractIndices.Count - 1));
                view.QueuedContractIndex = view.AvailableContractIndices[localIndex];
                view.QueuedContractScroll = 0;
            }

            if (view.QueuedContractIndex >= 0) {
                UpdateContractView(view, layout, view.QueuedContractIndex);
                if (view.CurrentContractIndex >= 0) {
                    DitherFade.DissolveIn(layout.Contract.ContentsMask);
                }
                view.CurrentContractIndex = view.QueuedContractIndex;
                view.QueuedContractIndex = -1;
                view.QueuedContractScroll = 0;

                view.QueuedAction = ContractViewQueuedAction.None;
            }
        }

        static private int GetFilteredIndex(ContractViewState viewState, int absoluteIndex) {
            return viewState.AvailableContractIndices.IndexOf(absoluteIndex);
        }

        static private void UpdateContractView(ContractViewState view, OverarchingContractLayout layout, int contractIndex) {
            Find.State(out ChapterState chapterState, out ContractState contractState);

            StringHash32 contractId = chapterState.ChapterDefinition.AvailableContracts[contractIndex];
            ContractDef definition = Find.NamedAsset<ContractDef>(contractId);

            ContractUtility.LoadContractUI(layout.Contract, chapterState.ChapterIndex, definition);

            bool isCurrentContract = chapterState.LastSelectedContractIndex == contractIndex;

            if (isCurrentContract) {
                layout.Contract.SelectContractButton.gameObject.SetActive(false);
                layout.Contract.SignatureImage.enabled = true;

                layout.Contract.ShowDesignCompleted(Find.State<MinigameSaveStates>().Design.FoundValidSolution);
            } else {
                layout.Contract.SelectContractButton.gameObject.SetActive(true);
                layout.Contract.SignatureImage.enabled = false;

                layout.Contract.ShowDesignCompleted(false);
            }

            int localIndex = GetFilteredIndex(view, contractIndex);

            layout.PrevContractButton.Interactable = localIndex > 0;
            layout.NextContractButton.Interactable = localIndex < view.AvailableContractIndices.Count - 1;
        }

        #endregion // Contracts

        #region Queued Actions

        static private void ProcessQueuedAction(ContractViewState view, OverarchingContractLayout layout) {
            switch (view.QueuedAction) {
                case ContractViewQueuedAction.Accept: {
                    if (view.CurrentMode == OverarchingContractMode.SelectNew) {
                        goto case ContractViewQueuedAction.ConfirmedAccept;
                    }
                    PresentChangePopup();
                    view.QueuedAction = ContractViewQueuedAction.None;
                    break;
                }
                case ContractViewQueuedAction.ConfirmedAccept: {
                    layout.Input.SetInputOverride(false);
                    ContractViewUtility.ApplyContract(view.CurrentContractIndex);
                    view.ProcessRoutine.Replace(view, AnimateContractAccept(view, layout));
                    view.QueuedAction = ContractViewQueuedAction.None;
                    break;
                }
            }
        }

        static private void PresentChangePopup() {
            PopupPrompt prompt = Find.Panel<PopupPrompt>();
            PopupRequestContent content = default;
            content.Header = "Change Contract?";
            content.Text = "You'll lose all progress in your current contract.";
            content.Flags = 0;
            content.ButtonA = new PopupRequestButton() {
                ResponseId = "no",
                Label = "No"
            };
            content.ButtonB = new PopupRequestButton() {
                ResponseId = "yes",
                Label = "Yes",
                Tint = new ColorPalette2() {
                    Background = ColorBank.Firebrick,
                    Content = ColorBank.Wheat
                }
            };
            content.Callback = HandlePopupChangeCallback;
            prompt.Populate(content);
            prompt.Show();
        }

        static private void HandlePopupChangeCallback(StringHash32 button) {
            Find.State(out ContractViewState view);
            if (button == "yes") {
                view.QueuedAction = ContractViewQueuedAction.ConfirmedAccept;
            } else {
                SpacefabGame.Events.Dispatch(GameEvents.CancelSelectContract, view.CurrentContractIndex.ToStringLookup());
            }
        }

        #endregion // Queued Actions

        #region Routines

        static private IEnumerator AnimateSelectNew(ContractViewState view, OverarchingContractLayout layout) {
            while(layout.AnimationRoutine.Exists()) {
                yield return null;
            }

            ScriptUtility.Trigger("OnContractSelectOpen");
        }

        static private IEnumerator AnimateContractAccept(ContractViewState view, OverarchingContractLayout layout) {
            Find.State(out ContractState contractState);
            while(contractState.LoadRoutine) {
                yield return null;
            }

            // TODO: sfx
            layout.Contract.SignatureImage.enabled = true;
            layout.Contract.SignatureImage.fillAmount = 0;
            yield return layout.Contract.SignatureImage.FillTo(1, 0.8f).Ease(Curve.Smooth);

            yield return 0.5f;

            view.ChangedContract = true;
            view.QueuedMode = OverarchingContractMode.Closed;

            SpacefabGame.Events.Dispatch(GameEvents.AcceptContract, view.CurrentContractIndex.ToStringLookup());
            ScriptUtility.Trigger("OnContractAccept");
        }

        #endregion // Routines
    }

    static public partial class ContractViewUtility {
    }
}