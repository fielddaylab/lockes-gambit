using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using ScriptableBake;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class ResultsPanelLayout : MonoBehaviour, IScenePreload {
        public Canvas Canvas;
        public CanvasGroup Group;
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
        public float HeaderInitialPosition;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Canvas.enabled = false;
            Input.SetInputOverride(false);
            Fader.alpha = 0;
            yield return null;

            Find.State(out ChapterState chapterState, out ContractState contractState);
            ContractUtility.LoadContractData(Contract, chapterState.ChapterIndex, contractState.ContractDefinition);
            yield return null;
        }

        public void Prepare() {
            Canvas.enabled = true;
            Input.TryPushPriority();
            Fader.alpha = 0;
            Positioning.SetAnchorY((RectTransform) HeaderPositionOffset.transform, HeaderInitialPosition);
            Positioning.SetAnchorY((RectTransform) ContractPositionOffset.transform, ContractInitialPosition);
            ContractPositionOffset.Offset0 = new Vector2(0, -60);
            HeaderPositionOffset.Offset0 = new Vector2(0, 60);
            ContractPositionOffset.GetComponent<CanvasGroup>().alpha = 0;
            HeaderPositionOffset.GetComponent<CanvasGroup>().alpha = 0;
        }

        public IEnumerator FadeIn() {
            yield return Routine.Combine(
                Fader.FadeTo(1, 0.3f),
                ContractPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.1f),
                ContractPositionOffset.Offset0To(default, 0.4f).DelayBy(0.1f).Ease(Curve.CubeOut),
                HeaderPositionOffset.GetComponent<CanvasGroup>().FadeTo(1, 0.4f).DelayBy(0.15f),
                HeaderPositionOffset.Offset0To(default, 0.4f).DelayBy(0.15f).Ease(Curve.CubeOut)
                );
        }
    }
}