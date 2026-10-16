using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
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
        public DitherFadeGroup ContractScrollButtonsFader;

        [Header("Positions")]
        public float ContractContextHeight;

        [NonSerialized] public Routine AnimationRoutine;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Canvas.enabled = false;
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            yield return null;

            PrevContractButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedContractViewIndex = view.ViewingContractIndex - 1;
            });
            NextContractButton.OnClick.Register(() => {
                Find.State(out ContractViewState view);
                view.QueuedContractViewIndex = view.ViewingContractIndex + 1;
            });

            Contract.CloseButton.OnClick.Register(() => {
                Find.State<ContractChangeState>().Phase = ContractChangePhase.Docking;
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
                    break;
                }
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