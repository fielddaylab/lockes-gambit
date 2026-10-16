using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI.Widgets;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Overarching
{
    public class ContractLayoutState : SharedStateComponent, IRegistrationCallbacks
    {
        [Header("Fader")]
        public CanvasGroup FaderGroup;

        [Header("Completion")]
        public CanvasGroup CompletionCanvasGroup;
        public RectTransform CompletedContractZone;
        public ContractUI CompletedContractUI;
        public Vector3 CompletedContractStartPos;

        [Header("Selection")]
        public CanvasGroup SelectionCanvasGroup;
        public RectTransform FocusedContractZone;
        public ContractUI SelectionContractUI;
        public GuiButton ConfirmContractButton;
        public DynamicButton NextContractButton;
        public DynamicButton PrevContractButton;

        [Header("Change")]
        public DynamicButton ViewCurrContractButton;
        public CanvasGroup ChangeCanvasGroup;
        public DynamicButton HideCurrContractButton;
        public GuiButton ChangeContractButton;
        public DynamicButton CancelChangeContractButton;
        public CanvasGroup DoubleConfirmCanvasGroup;
        public GuiButton DoubleConfirmContractButton;
        public GuiButton DoubleCancelContractButton;

        public Routine CompletionRoutine;
        public Routine SelectionRoutine;


        public void OnDeregister()
        {
            CompletionRoutine.Stop();
            SelectionRoutine.Stop();
        }

        public void OnRegister()
        {
            FaderGroup.alpha = 0;
            FaderGroup.blocksRaycasts = false;
            CompletionCanvasGroup.alpha = 0;
            CompletionCanvasGroup.blocksRaycasts = false;
            SelectionCanvasGroup.alpha = 0;
            SelectionCanvasGroup.blocksRaycasts = false;
            ChangeCanvasGroup.alpha = 0;
            ChangeCanvasGroup.blocksRaycasts = false;
            DoubleConfirmCanvasGroup.alpha = 0;
            DoubleConfirmCanvasGroup.blocksRaycasts = false;

            CompletedContractUI.gameObject.SetActive(false);
            ViewCurrContractButton.gameObject.SetActive(false);
            HideCurrContractButton.gameObject.SetActive(false);

            // Initialize next/prev buttons
            NextContractButton.onClick.AddListener(() => {
                Find.State(out ContractSelectState selectState);
                selectState.SelectedContractIndex = selectState.SelectedContractIndex + 1;
                selectState.SelectedContractIndexChanged = true;
            });

            PrevContractButton.onClick.AddListener(() => {
                Find.State(out ContractSelectState selectState);
                selectState.SelectedContractIndex = selectState.SelectedContractIndex - 1;
                selectState.SelectedContractIndexChanged = true;
            });

            // Initialize confirm contract button
            ConfirmContractButton.OnClick.AddListener(() => {
                    Find.State<ContractSelectState>().SelectionConfirmed = true;
                });

            // Initialize view current contract button
            ViewCurrContractButton.onClick.AddListener(() => {
                    GameLoop.ResumeUpdates(UpdateMasks.ContractSystemsMask);
                    Find.State<ContractChangeState>().Phase = ContractChangePhase.Starting;
                });

            // Initialize change contract button
            ChangeContractButton.OnClick.AddListener(() => {
                    Find.State<ContractChangeState>().Phase = ContractChangePhase.ContractSelectSystem;
                });

            // Initialize hide contract button
            HideCurrContractButton.onClick.AddListener(() => {
                    Find.State<ContractChangeState>().Phase = ContractChangePhase.Docking;
                });

            // Initialize cancel change contract button
            CancelChangeContractButton.onClick.AddListener(() => {
                    Find.State<ContractChangeState>().Phase = ContractChangePhase.Viewing;
                });

            // Initialize double confirm contract button
            DoubleConfirmContractButton.OnClick.AddListener(() => {
                    Find.State<ContractChangeState>().ChangeDoubleConfirmed = true;
                });

            DoubleCancelContractButton.OnClick.AddListener(() => {
                Find.State<ContractChangeState>().Phase = ContractChangePhase.DoubleCancelContract;
            });
        }
        public void SetViewCurrContractLabel(StringHash32 contractId)
        {
            var label = ViewCurrContractButton.GetComponentInChildren<TMP_Text>();
            label.text = contractId.IsEmpty ? string.Empty : ContractUtility.GetDefinition(contractId).Title();
        }
    }
}