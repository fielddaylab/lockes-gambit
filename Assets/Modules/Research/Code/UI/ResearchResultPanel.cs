using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Research {
    public sealed class ResearchResultPanel : SharedStateComponent, IScenePreload, ISceneLateInitialize {
        public ResultsPanelLayout PanelLayout;
        public SceneReference NextScene;
        public ContractRequirementTable Table;
        public RectTransform TableRoot;

        private void Commit() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.Confirmed;
        }

        private void Cancel() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.None;
            Hide();
        }

        public void Show() {
            Find.State(out ChapterState chapterState, out ContractState contractState, out ResearchMinigameState researchState, out PlayerProgressState progressState);

            ContractUIUtility.UpdateVisualsWithCompletion(contractState.CachedRequirements, Table, researchState.SandboxProperties);

            bool allCorrect = contractState.ContractDefinition && ContractProgressUtility.IsContractSatisfied(progressState, researchState, contractState.ContractDefinition);

            PanelLayout.HeaderText.SetText(allCorrect ? "Research Complete" : "Research Incomplete");
            PanelLayout.HeaderBackground.color = allCorrect ? PanelLayout.CompleteHeaderColor : PanelLayout.IncompleteHeaderColor;

            TableRoot.GetComponent<LayoutOffset>().ClearOffsets();

            PanelLayout.Prepare();
            PanelLayout.AnimationRoutine.Replace(this, DisplayRoutine(this));
        }

        public void Hide() {
            PanelLayout.AnimationRoutine.Replace(this, PanelLayout.FadeOut());
        }

        public IEnumerator<WorkSlicer.Result?> Preload() {
            PanelLayout.Contract.ContextBackButton.OnClick.Register(Cancel);
            PanelLayout.Contract.ContextContinueButton.OnClick.Register(Commit);
            yield return null;
        }

        public void LateInitialize() {
            Find.State(out ChapterState chapterState, out ContractState contractState);

            ContractUIUtility.InitializeVisuals(contractState.CachedRequirements, Table);
        }

        static private IEnumerator DisplayRoutine(ResearchResultPanel resultPanel) {
            yield return resultPanel.PanelLayout.FadeIn();
            yield return 0.1f;
            yield return Routine.Combine(
                resultPanel.PanelLayout.FadeInContextPanel(),
                resultPanel.TableRoot.GetComponent<LayoutOffset>().Offset0To(new Vector2(0, 112), 0.3f).DelayBy(0.1f).Ease(Curve.Smooth) 
                );
            resultPanel.PanelLayout.Finish();
        }
    }
}