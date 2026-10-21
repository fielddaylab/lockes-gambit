using System;
using System.Collections;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Debugging;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using FieldDay.UI;
using FieldDay.UI.Animation;
using SpaceFab.Save;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;

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
                    case OverarchingContractMode.Complete: {
                        view.QueuedContractIndex = chapterState.LastSelectedContractIndex;
                        view.ProcessRoutine.Replace(view, AnimateContractCompletion(view, layout));
                        break;
                    }
                }

                layout.AnimationRoutine.Replace(layout, layout.FadeIn(view.CurrentMode));

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

            bool isCurrentContract = chapterState.LastSelectedContractIndex == contractIndex;

            ContractUtility.LoadContractUI(layout.Contract, chapterState.ChapterIndex, definition);

            if (isCurrentContract) {
                layout.Contract.SelectContractButton.gameObject.SetActive(false);
                ContractUtility.ApplyContractProgressToUI(layout.Contract, definition);
            } else {
                layout.Contract.SelectContractButton.gameObject.SetActive(true);
                ContractUtility.ApplyNoProgressToUI(layout.Contract, definition);
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
                Label = "No",
                Tint = new ColorPalette2() {
                    Background = ColorBank.Wheat,
                    Content = ColorBank.Black
                }
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
            yield return 0.5f;
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

        static private IEnumerator AnimateContractCompletion(ContractViewState view, OverarchingContractLayout layout) {
            while (layout.AnimationRoutine.Exists()) {
                yield return null;
            }

            Find.State(out ChapterState chapterState, out ContractState contractState, out PlayerProgressState progressState, out MinigameSaveStates saveStates);

            yield return 0.1f;

            yield return layout.FadeInContextPanel();
            yield return 1;

            using (var t = TempVarTable.Alloc()) {
                t.Set("risk", MinigameSaveUtility.CurrentRisk(saveStates));
                var trigger = ScriptUtility.Trigger("BeforeContractEvaluation", t);
                do {
                    yield return null;
                } while (trigger.IsRunning());
            }
            yield return 0.25f;

            bool forceFailure = DebugInput.IsDown(KeyCode.F);
            yield return EvaluateRisk(layout, forceFailure);

            // TODO: animate time and money

            yield return layout.FadeOutContextPanel();

            yield return 1f;

            layout.Contract.ApprovedStampMask.SetAlpha(0);
            layout.Contract.ApprovedStamp.gameObject.SetActive(true);
            layout.Contract.ApprovedStamp.transform.localScale = new Vector3(2, 2, 2);
            yield return Routine.Combine(
                Tween.ZeroToOne(layout.Contract.ApprovedStampMask.SetAlpha, 0.2f),
                layout.Contract.ApprovedStamp.transform.ScaleTo(1, 0.3f, Axis.XYZ).Ease(Curve.CubeOut)
                );

            yield return 0.8f;

            yield return layout.Fader.FadeTo(0, 0.3f);
            yield return 0.5f;

            ContractUtility.ApplyCompletedContractState(contractState, progressState, saveStates);
            Find.State<ProgressMeterState>().NeedsRefresh = true;
            
            yield return 0.25f;

            yield return ProgressMeterUtility.ProcessGoals();

            var completedThread = ScriptUtility.Trigger("OnChapterEnd");
            do {
                yield return null;
            } while (completedThread.IsRunning());

            yield return 0.25f;

            ContractUtility.WipeContractState(contractState, progressState, chapterState, saveStates);
            ChapterUtility.LoadNextChapter(chapterState, progressState, contractState, saveStates);
        }

        static private IEnumerator EvaluateRisk(OverarchingContractLayout layout, bool forceFailure) {
            Find.GlobalAsset(out ChapterManifest chapterManifest);
            int[] hazardChances = chapterManifest.HazardChances;
            Find.State(out MinigameSaveStates minigameStates, out ChapterState chapterState, out ContractState contractState, out PlayerProgressState playerProgress);
            int time = MinigameSaveUtility.CurrentTime(minigameStates);
            int profit = contractState.ContractDefinition.Payout() - MinigameSaveUtility.CurrentCosts(minigameStates);
            int risk = MinigameSaveUtility.CurrentRisk(minigameStates);
            int visualRisk = risk;

            if (!forceFailure && risk > 0 && risk < 9) {
                int predictedCycles = playerProgress.ElapsedCycles + chapterState.ChapterDefinition.HazardTime;
                int predictedFunds = playerProgress.Funds + profit - chapterState.ChapterDefinition.HazardCost;
                if (predictedCycles >= chapterState.ChapterDefinition.CumulativeMaxTime - 2) {
                    risk = 0;
                    Log.Msg("[ContractViewerSystem] Predicted cycles is {0}, too close to cumulative max time {1} - zeroing out risk", predictedCycles, chapterState.ChapterDefinition.CumulativeMaxTime);
                } else if (predictedFunds <= chapterState.ChapterDefinition.CumulativeMinProfit + 2) {
                    risk = 0;
                    Log.Msg("[ContractViewerSystem] Predicted funds is {0}, too close to cumulative min funds {1} - zeroing out risk", predictedFunds, chapterState.ChapterDefinition.CumulativeMinProfit);
                }
            }

            bool encounteredHazard = false;
            int riskSlot = 0;
            if (risk > 0) {
                encounteredHazard = forceFailure || RNG.Instance.Next(100) < chapterManifest.HazardChances[risk - 1];
            } else {
                encounteredHazard = false;
            }

            if (encounteredHazard) {
                riskSlot = RNG.Instance.Next(visualRisk);
            } else {
                int leftover = layout.RiskPanel.Meter.MaxValue - visualRisk;
                riskSlot = visualRisk + RNG.Instance.Next(leftover);
            }

            int finalValue = (9 * 2) + riskSlot;

            if (RNG.Instance.Chance(0.6f)) {
                finalValue += 9;
            }
            if (visualRisk > 0 | RNG.Instance.Chance(0.5f)) {
                finalValue += 9;
            }

            RiskMeterStyle riskStyle = (RiskMeterStyle) layout.RiskPanel.Meter.Styler;

            riskStyle.Highlight.enabled = true;
            riskStyle.Highlight.SetAlpha(0);
            riskStyle.Highlight.rectTransform.localPosition = riskStyle.Slots[0].rectTransform.localPosition;
            yield return riskStyle.Highlight.FadeTo(1, 0.2f);

            yield return 0.1f;

            int highlightedSlot = 0;
            yield return Tween.Float(0, finalValue + RNG.Instance.NextFloat(0, 0.9f), (f) => {
                int nextSlot = (int) f % 9;
                if (highlightedSlot != nextSlot) {
                    highlightedSlot = nextSlot;
                    riskStyle.Highlight.rectTransform.localPosition = riskStyle.Slots[nextSlot].rectTransform.localPosition;
                    Sfx.Play(layout.RouletteTickSounds[nextSlot]);
                }
            }, 1f + finalValue * 0.12f).Ease(Curve.CubeOut);

            yield return 0.3f;

            riskStyle.Flash.rectTransform.localPosition = riskStyle.Highlight.rectTransform.localPosition;
            FlashAnim.Play(riskStyle.Flash, Color.white, FlashAnim.Default);

            if (encounteredHazard) {
                layout.Fader.GetComponentInChildren<Graphic>().SetColor(layout.HazardColor);
                // TODO: sfx
            } else {
                layout.Fader.GetComponentInChildren<Graphic>().SetColor(layout.SafeColor);
                // TODO: sfx
            }

            yield return 0.5f;

            if (encounteredHazard) {
                layout.RiskCostMeter.SetValue(chapterState.ChapterDefinition.HazardCost);
                layout.RiskTimeMeter.SetValue(chapterState.ChapterDefinition.HazardTime);
            }

            using (var t = TempVarTable.Alloc()) {
                t.Set("risk", visualRisk);
                t.Set("encounteredHazard", encounteredHazard);
                t.Set("timeLost", encounteredHazard ? chapterState.ChapterDefinition.HazardTime : 0);
                t.Set("moneyLost", encounteredHazard ? chapterState.ChapterDefinition.HazardCost : 0);
                var thread = ScriptUtility.Trigger("AfterContractEvaluation", t);
                do {
                    yield return null;
                } while (thread.IsRunning());
            }

            yield return riskStyle.Highlight.FadeTo(0, 0.2f);

            yield return 0.25f;
            yield return TimeMeterStyle.DrainIntoMeter(layout.RiskTimeMeter, layout.Contract.TimeIndicators);
            yield return MoneyMeterStyle.DrainIntoMeter(layout.RiskCostMeter, layout.Contract.RevenueIndicators);
        }

        #endregion // Routines
    }

    static public partial class ContractViewUtility {
    }
}