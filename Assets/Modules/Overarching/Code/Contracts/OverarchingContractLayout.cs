using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Overarching {
    public class OverarchingContractLayout : SharedStateComponent, IScenePreload {
        public Canvas Canvas;
        public CanvasInputLayer Input;

        [Header("Background")]
        public CanvasGroup Fader;

        [Header("Header")]
        public RectTransform HeaderPosition;
        public LayoutOffset HeaderPositionOffset;
        public TMP_Text HeaderText;

        [Header("Contract")]
        public ContractUI Contract;
        public GameObject ContractShadow;
        public LayoutOffset ContractPositionOffset;

        [Header("Browsing Controls")]
        public GuiButton PrevContractButton;
        public GuiButton NextContractButton;

        [Header("Animation")]
        public CanvasGroup ContractScrollButtonsFader;

        [Header("Positions")]
        public float ContractExpandDistance;
        public float ContractContextHeight;

        [Header("Completion")]
        public TMP_Text WarningHeader;
        public TMP_Text WarningText;
        public RiskInlayPanel RiskPanel;
        public Color SafeColor;
        public Color HazardColor;
        public GuiCounter RiskTimeMeter;
        public GuiCounter RiskCostMeter;
        [AudioEvent] public StringHash32[] RouletteTickSounds;

        [NonSerialized] public Routine AnimationRoutine;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Canvas.enabled = false;
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            yield return null;

            PrevContractButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedContractScroll = -1;
            });
            NextContractButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedContractScroll = 1;
            });

            Contract.CloseButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedMode = OverarchingContractMode.Closed;
            });
            Contract.SelectContractButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedAction = ContractViewQueuedAction.Accept;
                Find.State(out ContractState contractState);
                SpacefabGame.Events.Dispatch(GameEvents.SelectContract, contractState.ContractId.ToString());
            });
        }

        public void Prepare(OverarchingContractMode mode) {
            Canvas.enabled = true;
            Input.TryPushPriority();
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            ContractPositionOffset.ClearOffsets();
            HeaderPositionOffset.ClearOffsets();

            ContractPositionOffset.GetComponent<CanvasGroup>().alpha = 0;
            HeaderPositionOffset.GetComponent<CanvasGroup>().alpha = 0;

            Contract.ContextFader.alpha = 0;
            Contract.ContextSubCanvas.enabled = false;

            ContractScrollButtonsFader.alpha = 0;

            Find.State<OverarchingContractButtons>().ChangeButton.gameObject.SetActive(false);

            switch (mode) {
                case OverarchingContractMode.Change: {
                    Contract.CloseButton.gameObject.SetActive(true);
                    Contract.SelectContractButton.TextGraphic.SetText("CHANGE CONTRACT");
                    ContractShadow.gameObject.SetActive(false);
                    break;
                }
                case OverarchingContractMode.SelectNew: {
                    Contract.CloseButton.gameObject.SetActive(false);
                    Contract.SelectContractButton.TextGraphic.SetText("ACCEPT CONTRACT");
                    ContractShadow.gameObject.SetActive(false);
                    break;
                }
                case OverarchingContractMode.Complete: {
                    ContractShadow.gameObject.SetActive(true);
                    Contract.CloseButton.gameObject.SetActive(false);
                    Contract.SelectContractButton.gameObject.SetActive(false);
                    ContractScrollButtonsFader.gameObject.SetActive(false);

                    Find.State(out ChapterState chapterState, out MinigameSaveStates minigameStates);
                    RiskPanel.SetRisk(minigameStates.Supply.FinalizedRisk);
                    if (minigameStates.Supply.FinalizedRisk <= 0) {
                        RiskPanel.SetText(RiskPanel.DefaultNoRiskHeader, RiskPanel.DefaultNoRiskDescription);
                    } else {
                        string hazardDescription = chapterState.ChapterDefinition.HazardDescription;
                        if (string.IsNullOrEmpty(hazardDescription)) {
                            hazardDescription = RiskPanel.DefaultRiskDescription;
                        }
                        string hazardHeader = chapterState.ChapterDefinition.HazardHeader;
                        RiskPanel.SetText(hazardHeader, hazardDescription);
                    }

                    break;
                }
            }
        }

        public IEnumerator FadeIn(OverarchingContractMode mode) {

            if (mode == OverarchingContractMode.Complete) {
                ContractPositionOffset.Offset0 = new Vector2(0, -60);
                yield return Routine.Combine(
                    Fader.FadeTo(1, 0.3f),
                    ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.1f),
                    ContractPositionOffset.Offset0To(default, 0.4f).DelayBy(0.1f).Ease(Curve.CubeOut)
                );
            } else {
                ContractPositionOffset.Offset0 = new Vector2(0, -60);
                HeaderPositionOffset.Offset0 = new Vector2(0, 60);
                yield return Routine.Combine(
                    Fader.FadeTo(1, 0.3f),
                    ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.1f),
                    ContractPositionOffset.Offset0To(default, 0.4f).DelayBy(0.1f).Ease(Curve.CubeOut),
                    HeaderPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.15f),
                    HeaderPositionOffset.Offset0To(default, 0.4f).DelayBy(0.15f).Ease(Curve.CubeOut),
                    ContractScrollButtonsFader.FadeTo(1, 0.2f).DelayBy(0.35f)
                );
            }

            Input.SetInputOverride(null);
        }

        public IEnumerator FadeInContextPanel() {
            Contract.ContextFader.alpha = 0;
            Contract.ContextSubCanvas.enabled = true;
            Contract.ContextContentsMask.SetAlpha(0);
            Positioning.SetHeightDelta(Contract.ContextPanel, 32);

            yield return Routine.Combine(
                ContractPositionOffset.Offset1To(new Vector2(0, ContractExpandDistance), 0.3f).Ease(Curve.Smooth),
                Contract.ContextFader.FadeTo(1, 0.2f).DelayBy(0.1f),
                Contract.ContextPanel.SizeDeltaTo(ContractContextHeight, 0.3f, Axis.Y).Ease(Curve.CubeOut).DelayBy(0.1f),
                Tween.ZeroToOne(Contract.ContextContentsMask.SetAlpha, 0.2f).DelayBy(0.35f)
                );
        }

        public IEnumerator FadeOutContextPanel() {
            yield return Routine.Combine(
                ContractPositionOffset.Offset1To(default, 0.3f).Ease(Curve.Smooth).DelayBy(0.15f),
                Contract.ContextFader.FadeTo(0, 0.2f).DelayBy(0.25f),
                Contract.ContextPanel.SizeDeltaTo(32, 0.3f, Axis.Y).Ease(Curve.CubeInOut).DelayBy(0.25f),
                Tween.OneToZero(Contract.ContextContentsMask.SetAlpha, 0.2f)
                );

            Contract.ContextSubCanvas.enabled = false;
        }

        public IEnumerator FadeOut(OverarchingContractMode mode, bool hasChange) {
            Input.SetInputOverride(false);
            yield return Routine.Combine(
                Fader.FadeTo(0, 0.15f).DelayBy(0.1f),
                ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(0, 0.25f),
                HeaderPositionOffset.GetComponent<CanvasGroup>().FadeTo(0, 0.25f),
                ContractScrollButtonsFader.FadeTo(0, 0.25f)
                );

            Canvas.enabled = false;
            Input.TryPopPriority();

            if (mode == OverarchingContractMode.SelectNew || hasChange) {
                OverarchingTransitions.RefreshShipInteractions();
            } else if (mode == OverarchingContractMode.Change) {
                Find.State<OverarchingContractButtons>().ChangeButton.gameObject.SetActive(true);
            }
        }
    }

    public enum OverarchingContractMode {
        Closed,
        SelectNew,
        Change,
        Complete
    }
}