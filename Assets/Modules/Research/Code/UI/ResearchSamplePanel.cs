using BeauPools;
using FieldDay;
using FieldDay.Components;
using FieldDay.HID;
using FieldDay.Scripting;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using SpaceFab.UI;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// View component for the bottom Observations / sample panel. Pure
    /// view: inspector-assigned references plus transient runtime state
    /// (PickerOpen, the pool-mirrored PickerLabels / PickerClickHandlers
    /// lists). Click handlers route through ResearchUIInputUtility and
    /// SamplePanelInputUtility. Per-frame visual render is in
    /// SamplePanelVisualSystem.
    ///
    /// The picker overlay is dormant: AddObservationButton now opens the
    /// wiki to the active chamber's observation page, and nothing sets
    /// PickerOpen. The overlay fields and the ObservationPicker* code
    /// they drive are kept so the overlay can be restored without
    /// rebuilding it.
    /// </summary>
    public class ResearchSamplePanel : BatchedComponent, IRegistrationCallbacks {
        public GameObject EmptyState;
        public GameObject MainContent;

        public ResearchObservationChip[] SlotChips;
        public ResearchObservationChip HypothesisChip;

        public CursorHint AddObservationButton;
        public CursorHint AddPropertyButton;

        public GuiButton CompleteButton;

        public CursorHint VerifyButton;

        // Chamber buttons
        public GuiButton VoltageChamberButton;
        public GuiButton ThermalChamberButton;
        public GuiButton DopingChamberButton;
        public TMP_Text ChamberText;

        // Sample material view
        public Image SampleSprite;
        public TMP_Text SampleLabel;
        public Image SampleLabelBG;

        // Sample panel view for doping chamber
        public GameObject DopingGroup;
        // Dopant material view
        public Image SubstrateSprite;
        public TMP_Text SubstrateLabel;

        [Header("Verification")]
        public Image Lines;
        public GameObject LoadingBar;
        public Image FilledBar;
        public TMP_Text ResultText;
        public Image ResultIcon;

        // Whether the picker overlay is currently open. Per-instance
        // transient state; the visual system reads it to decide whether
        // to show the overlay.
        [NonSerialized] public bool PickerOpen;

        // Parallel to ResearchPools.ActivePickerChips: label bound to
        // each active picker chip, populated by
        // ObservationPickerLoadUtility on chamber load. Click handlers
        // index into this to know which label to emit.
        [NonSerialized] public List<MaterialPropertyLabel> PickerLabels;

        // Captured-index click handlers, parallel to ActivePickerChips.
        // Registered on chip Alloc, deregistered on Free. Lifecycle
        // managed by SamplePanelInputUtility / ObservationPickerLoadUtility.
        [NonSerialized] public List<Action> PickerClickHandlers;

        // Cached delegate references for slot click handlers so
        // OnDeregister can detach precisely.
        [NonSerialized] private Action[] m_SlotClickHandlers;

        public void OnRegister() {
            PickerLabels = new List<MaterialPropertyLabel>(8);
            PickerClickHandlers = new List<Action>(8);

            if (SlotChips != null) {
                m_SlotClickHandlers = new Action[SlotChips.Length];
                for (int i = 0; i < SlotChips.Length; i++) {
                    int captured = i;
                    m_SlotClickHandlers[i] = () => HandleSlotClick(captured);
                    if (SlotChips[i] != null && SlotChips[i].Click != null) {
                        SlotChips[i].Click.onClick.Register(m_SlotClickHandlers[i]);
                    }
                }
            }

            if (HypothesisChip != null) {
                HypothesisChip.Click.onClick.Register(HandlePropertySlotClick);
            }
            
            if (AddObservationButton != null) {
                AddObservationButton.onClick.Register(HandleAddObservation);
            }

            if (AddPropertyButton != null) {
                AddPropertyButton.onClick.Register(HandleAddProperty);
            }

            if (VerifyButton != null) {
                VerifyButton.onClick.Register(HandleSubmit);
            }

            if (VoltageChamberButton != null) {
                VoltageChamberButton.CursorHint.onClick.AddListener(() => HandleChamberSwitch(ActiveChamberKind.Voltage));
            }
            if (ThermalChamberButton != null) {
                ThermalChamberButton.CursorHint.onClick.AddListener(() => HandleChamberSwitch(ActiveChamberKind.Thermal));
            }
            if (DopingChamberButton != null) {
                DopingChamberButton.CursorHint.onClick.AddListener(() => HandleChamberSwitch(ActiveChamberKind.Doping));
            }
            if (CompleteButton != null) {
                CompleteButton.OnClick.AddListener(HandleCompleteMinigame);
            }

            if (ResultText != null) {
                ResultText.text = "";
            }

            SamplePanelInputUtility.ClosePicker(this);
        }

        public void OnDeregister() {
            if (Game.SharedState.Has<ResearchPools>())
            {
                SamplePanelInputUtility.FreeAllPickerChips(this, Find.State<ResearchPools>());
            }

            if (SlotChips != null && m_SlotClickHandlers != null) {
                for (int i = 0; i < SlotChips.Length; i++) {
                    if (SlotChips[i] != null && SlotChips[i].Click != null && m_SlotClickHandlers[i] != null) {
                        SlotChips[i].Click.onClick.Deregister(m_SlotClickHandlers[i]);
                    }
                }
            }
            if (HypothesisChip != null) {
                HypothesisChip.Click.onClick.Deregister(HandlePropertySlotClick);
            }

            if (AddObservationButton != null) {
                AddObservationButton.onClick.Deregister(HandleAddObservation);
            }

            if (AddPropertyButton != null) {
                AddPropertyButton.onClick.Deregister(HandleAddProperty);
            }

            if (VerifyButton != null) {
                VerifyButton.onClick.Deregister(HandleSubmit);
            }

            if (VoltageChamberButton != null) {
                VoltageChamberButton.CursorHint.onClick.RemoveListener(() => HandleChamberSwitch(ActiveChamberKind.Voltage));
            }
            if (ThermalChamberButton != null) {
                ThermalChamberButton.CursorHint.onClick.RemoveListener(() => HandleChamberSwitch(ActiveChamberKind.Thermal));
            }
            if (DopingChamberButton != null) {
                DopingChamberButton.CursorHint.onClick.RemoveListener(() => HandleChamberSwitch(ActiveChamberKind.Doping));
            }

            if (CompleteButton != null) {
                CompleteButton.OnClick.RemoveListener(HandleCompleteMinigame);
            }
        }

        private void HandleAddObservation() {
            ResearchUIInputUtility.RequestAddObservation(Find.State<ResearchUIInputState>());
            // Shortcut to the wiki page listing the active chamber's
            // observations. The player can also reach it by browsing the
            // wiki; adds work either way.
            ResearchWikiInputUtility.OpenObservationPageForActiveChamber(Find.State<ChamberInterfacerState>());
        }

        private void HandleAddProperty() {
            ResearchUIInputUtility.RequestAddObservation(Find.State<ResearchUIInputState>());
            // Shortcut to the wiki page listing the material properties.
            ResearchWikiInputUtility.OpenPropertyPage(Find.State<PlayerProgressState>());
        }

        // Picker chip click. Public so ObservationPickerLoadUtility can
        // bind a captured-index closure to each alloced chip's Click.
        public void HandlePickerChip(int index) {
            SamplePanelInputUtility.SubmitPickerSelection(this, Find.State<ResearchUIInputState>(), index);
        }

        private void HandleSlotClick(int index) {
            SamplePanelInputUtility.RequestSlotRemove(this, Find.State<ResearchUIInputState>(), Find.State<HypothesisViewModelState>(), index);
        }

        private void HandlePropertySlotClick()
        {
            SamplePanelInputUtility.RequestHypothesisSlotRemove(this, Find.State<ResearchUIInputState>(), Find.State<HypothesisViewModelState>());
        }

        private void HandleSubmit() {
            ResearchUIInputUtility.RequestSubmit(Find.State<ResearchUIInputState>());
        }

        private void HandleChamberSwitch(ActiveChamberKind kind)
        {
            ChamberInterfacerUtility.SetActiveChamber(Find.State<ChamberInterfacerState>(), kind);
        }

        public void HandleCompleteMinigame() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.Requested;
        }
    }

    /// <summary>
    /// Mutators paired with ResearchSamplePanel. Open/close picker,
    /// submit a picker selection, request a slot removal, free all
    /// pool-held picker chips — all funnel here so the panel keeps no
    /// logic of its own beyond click dispatch.
    /// </summary>
    public static class SamplePanelInputUtility {
        public static void OpenPicker(ResearchSamplePanel panel) {
            if (panel == null) return;
            panel.PickerOpen = true;
            ScriptUtility.Trigger(ResearchScriptTriggers.OnObservationPickerOpened);
        }

        public static void ClosePicker(ResearchSamplePanel panel) {
            if (panel == null) return;
            panel.PickerOpen = false;
        }

        // Picker chip click. Resolves the bound label, requests the add.
        // Out-of-range index is treated as a
        // close-only.
        public static void SubmitPickerSelection(ResearchSamplePanel panel, ResearchUIInputState inputState, int index) {
            if (panel == null) {
                return;
            }
            if (panel.PickerLabels == null || index < 0 || index >= panel.PickerLabels.Count) {
                ClosePicker(panel);
                return;
            }
            ResearchUIInputUtility.RequestPickerSelection(inputState, panel.PickerLabels[index]);
        }

        // Sample-slot click. Filtered: only filled, non-locked slots in
        // the viewmodel's slot view can be removed. Locked = auto-
        // populated via a confirmed ancestor sub-property; index past
        // SlotCount = empty placeholder.
        public static void RequestSlotRemove(ResearchSamplePanel panel, ResearchUIInputState inputState, HypothesisViewModelState viewModel, int index) {
            if (panel == null || viewModel == null) {
                return;
            }
            if (index < 0 || index >= viewModel.SlotCount) {
                return;
            }
            bool locked = (viewModel.SlotLockedMask & (1u << index)) != 0;
            if (locked) {
                return;
            }
            ResearchUIInputUtility.RequestRemoveObservation(inputState, index);
        }

        // Hypothesis-slot click. Removable whenever a hypothesis is
        // selected — a fulfilled one auto-clears and never displays.
        public static void RequestHypothesisSlotRemove(ResearchSamplePanel panel, ResearchUIInputState inputState, HypothesisViewModelState viewModel) {
            if (panel == null || viewModel == null) {
                return;
            }
            if (!viewModel.HypothesisSelected) {
                return;
            }
            ResearchUIInputUtility.RequestRemoveHypothesis(inputState);
        }

        // Returns every pool-held picker chip to the pool, deregistering
        // its captured-index click handler in lockstep. Called from
        // ResearchSamplePanel.OnDeregister and as the first step of
        // ObservationPickerLoadUtility.LoadFor (clean slate on chamber
        // load). No-op if pools or its active list is null (mid-tear-
        // down, or pre-Preload).
        public static void FreeAllPickerChips(ResearchSamplePanel panel, ResearchPools pools) {
            if (panel == null || pools == null || pools.ActivePickerChips == null) return;
            int n = pools.ActivePickerChips.Count;
            for (int i = n - 1; i >= 0; i--) {
                ResearchObservationChip chip = pools.ActivePickerChips[i];
                Action handler = panel.PickerClickHandlers != null && i < panel.PickerClickHandlers.Count
                    ? panel.PickerClickHandlers[i]
                    : null;
                if (chip != null && chip.Click != null && handler != null) {
                    chip.Click.onClick.Deregister(handler);
                }
                if (chip != null) {
                    Pool.TryFree(chip);
                }
            }
            pools.ActivePickerChips.Clear();
            if (panel.PickerClickHandlers != null) panel.PickerClickHandlers.Clear();
            if (panel.PickerLabels != null) panel.PickerLabels.Clear();
        }

        public static void LockChamberButton(ResearchSamplePanel panel, ActiveChamberKind chamberKind, ResearchUIAssets config) {
            GuiButton chamberButton = chamberKind switch {
                ActiveChamberKind.Thermal => panel.ThermalChamberButton,
                ActiveChamberKind.Doping => panel.DopingChamberButton,
                _ => null,
            };

            if (chamberButton == null) { return; }
            chamberButton.ImageGraphic.sprite = config.LockedChamberButton;
            chamberButton.CursorHint.enabled = false;
        }

        public static void UnlockChamberButton(ResearchSamplePanel panel, ActiveChamberKind chamberKind, ResearchUIAssets config) {
            switch (chamberKind) {
                case ActiveChamberKind.Thermal:
                    panel.ThermalChamberButton.ImageGraphic.sprite = config.ThermalButton;
                    panel.ThermalChamberButton.CursorHint.enabled = true;
                    break;
                case ActiveChamberKind.Doping:
                    panel.DopingChamberButton.ImageGraphic.sprite = config.DopingButton;
                    panel.DopingChamberButton.CursorHint.enabled = true;
                    break;
                default:
                    return;
            };
        }
    }
}
