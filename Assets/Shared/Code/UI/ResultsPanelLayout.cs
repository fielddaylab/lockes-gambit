using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using ScriptableBake;
using SpaceFab.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class ResultsPanelLayout : MonoBehaviour, IScenePreload, ISceneLateInitialize {
        public Canvas Canvas;
        public CanvasInputLayer Input;

        [Header("Background")]
        public CanvasGroup Fader;

        [Header("Header")]
        public RectTransform HeaderPosition;
        public LayoutOffset HeaderPositionOffset;
        public TMP_Text HeaderText;
        public Graphic HeaderBackground;

        [Header("Contract")]
        public ContractUI Contract;
        public LayoutOffset ContractPositionOffset;

        [Header("Positions")]
        public float ContractInitialPosition;
        public float ContractExpandDistance;
        public float HeaderInitialPosition;
        public float HeaderExpandDistance;
        public float ContractContextHeight;

        [Header("Colors")]
        public Color IncompleteHeaderColor;
        public Color CompleteHeaderColor;

        [NonSerialized] public Routine AnimationRoutine;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Canvas.enabled = false;
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            yield return null;
        }

        public void Prepare() {
            Canvas.enabled = true;
            Input.TryPushPriority();
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            ContractPositionOffset.ClearOffsets();
            HeaderPositionOffset.ClearOffsets();

            Positioning.SetOffsetY((RectTransform) HeaderPositionOffset.transform, HeaderInitialPosition);
            Positioning.SetOffsetY((RectTransform) ContractPositionOffset.transform, ContractInitialPosition);

            ContractPositionOffset.GetComponent<CanvasGroup>().alpha = 0;
            HeaderPositionOffset.GetComponent<CanvasGroup>().alpha = 0;

            Contract.ContextFader.alpha = 0;
            Contract.ContextSubCanvas.enabled = false;
        }

        public IEnumerator FadeIn() {
            ContractPositionOffset.Offset0 = new Vector2(0, -60);
            HeaderPositionOffset.Offset0 = new Vector2(0, 60);
            yield return Routine.Combine(
                Fader.FadeTo(1, 0.3f),
                ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.1f),
                ContractPositionOffset.Offset0To(default, 0.4f).DelayBy(0.1f).Ease(Curve.CubeOut),
                HeaderPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.15f),
                HeaderPositionOffset.Offset0To(default, 0.4f).DelayBy(0.15f).Ease(Curve.CubeOut)
                );
        }

        public IEnumerator FadeOut() {
            Input.SetInputOverride(false);
            yield return Routine.Combine(
                Fader.FadeTo(0, 0.3f).DelayBy(0.1f),
                ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(0, 0.4f),
                HeaderPositionOffset.GetComponent<CanvasGroup>().FadeTo(0, 0.4f)
                );

            Canvas.enabled = false;
            Input.TryPopPriority();
        }

        public IEnumerator FadeInContextPanel() {
            Contract.ContextFader.alpha = 0;
            Contract.ContextSubCanvas.enabled = true;
            Contract.ContextContentsMask.SetAlpha(0);
            Positioning.SetHeightDelta(Contract.ContextPanel, 32);

            yield return Routine.Combine(
                ContractPositionOffset.Offset1To(new Vector2(0, ContractExpandDistance), 0.3f).Ease(Curve.Smooth),
                HeaderPositionOffset.Offset1To(new Vector2(0, HeaderExpandDistance), 0.3f).Ease(Curve.Smooth),
                Contract.ContextFader.FadeTo(1, 0.2f).DelayBy(0.1f),
                Contract.ContextPanel.SizeDeltaTo(ContractContextHeight, 0.3f, Axis.Y).Ease(Curve.CubeOut).DelayBy(0.1f),
                Tween.ZeroToOne(Contract.ContextContentsMask.SetAlpha, 0.2f).DelayBy(0.35f)
                );
        }

        public void Finish() {
            Input.SetInputOverride(null);
        }

        void ISceneLateInitialize.LateInitialize() {
            Find.State(out ChapterState chapterState, out ContractState contractState);

            ContractUtility.LoadContractUI(Contract, chapterState.ChapterIndex, contractState.ContractDefinition);
            if (contractState.ContractDefinition) {
                ContractUtility.ApplyContractProgressToUI(Contract, contractState.ContractDefinition);
            }
        }
    }
}