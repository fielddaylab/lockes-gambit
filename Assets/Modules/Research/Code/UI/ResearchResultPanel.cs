using System;
using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Research {
    public sealed class ResearchResultPanel : SharedPanel {
        public GuiButton BackButton;
        public GuiButton NextButton;
        public SceneReference NextScene;
        public ContractRequirementTable Table;

        public Image Background;
        public Image HeadingBackground;
        public TMP_Text HeadingText;

        protected override void Awake() {
            base.Awake();

            BackButton.OnClick.AddListener(Cancel);
            NextButton.OnClick.AddListener(Commit);
        }

        private void Start() {
            Hide();
        }

        private void Commit() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.Confirmed;
        }

        private void Cancel() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.None;
            Hide();
        }

        // Safety net for the pushed priority: OnDisable fires both on Hide() (SetActive(false)) and
        // on scene-teardown deactivation, and runs before OnDestroy invalidates the input layer, so
        // PopPriority's not-destroyed assert still holds. This is what catches the Commit-then-exit
        // path where Hide() never runs.
        private void OnDisable() {
            ReleasePriority();
        }

        // Pushes GUI input priority for this panel, at most once.
        private void AcquirePriority() {
            Input.TryPushPriority();
        }

        // Pops the priority pushed by AcquirePriority, if held. Skipped during shutdown (the GuiMgr
        // stack is being torn down anyway and the layer may already be invalid).
        private void ReleasePriority() {
            Input.TryPopPriority();
        }

        public override void Show() {
            base.Show();

            AcquirePriority();
            Input.SetInputOverride(null);

            Find.State(out ChapterState chapterState, out ContractState contractState, out ResearchMinigameState researchState, out PlayerProgressState progressState);

            ContractUIUtility.BuildRequirementData(Table, chapterState.ChapterIndex, contractState.ContractDefinition);
            ContractUIUtility.InitializeVisuals(Table);
            ContractUIUtility.UpdateVisualsWithCompletion(Table, researchState.SandboxProperties);
            Positioning.SetHeightDelta((RectTransform)Table.transform.parent, Table.Sizer.LastSize.y + 48);

            if (Background == null || HeadingBackground == null || HeadingText == null) { return; }

            Find.GlobalAsset(out ResultDisplayConfig config);
            bool success = contractState.ContractDefinition && ContractProgressUtility.IsContractSatisfied(progressState, researchState, contractState.ContractDefinition);
            Background.color = success ? config.SuccessBackgroundColor : config.FailureBackgroundColor;
            HeadingText.text = success ? "RESEARCH COMPLETE" : "RESEARCH INCOMPLETE";
            HeadingBackground.color = success ? config.SuccessHeaderColor : config.FailureHeaderColor;
        }

        public override void Hide() {
            base.Hide();

            // base.Hide() deactivates the GameObject, which fires OnDisable -> ReleasePriority; this
            // call is a guarded no-op in that case but keeps the Show/Hide pairing explicit.
            ReleasePriority();
            Input.SetInputOverride(false);
        }
    }
}