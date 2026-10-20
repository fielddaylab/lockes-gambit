using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.UI;
using FieldDay.UI.Animation;
using FieldDay.UI.Widgets;
using SpaceFab.UI;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Narrative {
    /// <summary>
    /// Overarching-scene dialogue printer. Gated by a Next button per line, pushes input
    /// priority while visible so the rest of the scene's UI is blocked, and auto-dismisses
    /// via OnThreadReleased when the script ends. For the minigame variant (no Next button,
    /// no input gating, stays up until explicitly dismissed), see MinigameDialogueBox.
    /// </summary>
    public sealed class DialogueBox : BaseSpacefabDialogueBox {
        #region Inspector

        [Header("Next")]
        [SerializeField] private GuiButton m_NextButton;

        [Header("Animation")]
        [SerializeField] private CanvasGroup m_VisiblityGroup;
        [SerializeField] private LayoutOffset m_LayoutOffset;
        [SerializeField] private RectTransform m_FoldOutTransform;
        [SerializeField] private float m_FoldOutYPosDefault;
        [SerializeField] private float m_FoldOutYPosOffscreen;
        [SerializeField] private float m_FoldOutWidthDefault;
        [SerializeField] private float m_FoldOutWidthCollapsed;
        [SerializeField] private Graphic[] m_FoldOutCollapsedToHide;

        #endregion // Inspector

        [NonSerialized] private IInputLayer m_InputLayer;
        // Tracks whether AnimateToOn pushed m_InputLayer onto GuiMgr's priority stack. We can't
        // rely on m_IsVisible because AnimateToOff clears that flag before the pop actually runs,
        // and we need OnDisable to know whether a defensive pop is owed.
        [NonSerialized] private bool m_PriorityPushed;

        private void Start() {
            m_VisiblityGroup.gameObject.SetActive(false);
            m_InputLayer = IInputLayer.Find(this);
        }

        // Safety net: if the box is disabled or destroyed while a dialogue is mid-flight, the
        // normal OnThreadReleased -> AnimateToOff -> PopPriority chain never runs and GuiMgr is
        // left with a stale PriorityRecord that disables every layer below it.
        protected override void OnDisable() {
            if (m_PriorityPushed && m_InputLayer != null) {
                Game.Gui?.PopPriority(m_InputLayer);
                m_PriorityPushed = false;
            }
            base.OnDisable();
        }

        public override IEnumerator CompleteLine() {
            if ((m_CurrentLineFlags & LineFlags.AutoContinue) != 0) {
                yield return 0.2f;
                yield break;
            }

            TMP_Text label = m_NextButton.LayoutSizeGroup.Root.GetComponent<TMP_Text>();

            m_NextButton.gameObject.SetActive(true);
            if ((m_CurrentLineFlags & LineFlags.IsEnd) != 0) {
                label.SetText("END CALL");
            } else {
                label.SetText("NEXT");
            }

            m_NextButton.LayoutSizeGroup.Sync();

            m_NextButton.ConsumeClick();
            while (!m_NextButton.ConsumeClick()) {
                yield return null;
            }
            m_NextButton.gameObject.SetActive(false);

            if ((m_CurrentLineFlags & LineFlags.IsEnd) != 0) {
                CurrentThread.ReleaseCurrentPrinter(ScriptThreadOwnershipClearReason.Completed);
            }
        }

        protected override void OnLineReplacedWhileVisible() {
            // Pop the fold-out's layout offset to draw the eye when the line text changes
            // mid-conversation. Only fires when maxVisibleCharacters has reset to 0 — i.e. a
            // genuine new line, not a fast-forward within the current line.
            if (m_Contents.maxVisibleCharacters == 0) {
                PopAnim.Play(m_LayoutOffset, PopAnim.Default);
            }
        }

        protected override void OnThreadReleased() {
            if (m_IsVisible) {
                m_IsVisible = false;
                m_Animation.Replace(this, AnimateToOff());
            }
        }

        protected override float GetActiveTypingDelayMultiplier() {
            // Faster typing while the player holds the mouse down — only available when input is
            // actually enabled on this layer (otherwise we'd respond to clicks meant for nothing).
            if (m_InputLayer.IsInputEnabled() && Game.Input.IsMouseDown(0)) {
                return 0.1f;
            }
            return 0.4f;
        }

        #region Animations

        protected override IEnumerator AnimateToOn() {
            // TODO: Handle partially offscreen
            m_IsVisible = true;

            m_VisiblityGroup.blocksRaycasts = false;

            foreach (var foldoutObj in m_FoldOutCollapsedToHide) {
                foldoutObj.enabled = false;
            }

            m_NextButton?.gameObject.SetActive(false);

            m_FoldOutTransform.anchoredPosition = new Vector2(m_FoldOutTransform.anchoredPosition.x, m_FoldOutYPosOffscreen);
            Positioning.SetWidthDelta(m_FoldOutTransform, m_FoldOutWidthCollapsed);

            m_VisiblityGroup.gameObject.SetActive(true);
            Game.Gui.PushPriority(m_InputLayer);
            m_PriorityPushed = true;
            yield return null;

            yield return m_FoldOutTransform.AnchorPosTo(m_FoldOutYPosDefault, 0.2f, Axis.Y).Ease(Curve.BackOut);

            foreach (var obj in m_FoldOutCollapsedToHide) {
                obj.SetAlpha(0);
                obj.enabled = true;
            }

            yield return Routine.Combine(
                m_FoldOutTransform.SizeDeltaTo(m_FoldOutWidthDefault, 0.3f, Axis.X).Ease(Curve.CubeOut),
                Tween.ZeroToOne(SetFoldoutCollapsableObjectsAlpha, 0.2f).DelayBy(0.1f)
            );

            m_VisiblityGroup.blocksRaycasts = true;
        }

        protected override IEnumerator AnimateToOff() {
            m_IsVisible = false;

            m_VisiblityGroup.blocksRaycasts = false;
            if (m_PriorityPushed) {
                Game.Gui.PopPriority(m_InputLayer);
                m_PriorityPushed = false;
            }

            yield return m_FoldOutTransform.AnchorPosTo(m_FoldOutYPosOffscreen, 0.2f, Axis.Y).Ease(Curve.BackIn);

            m_VisiblityGroup.gameObject.SetActive(false);
        }

        private void SetFoldoutCollapsableObjectsAlpha(float alpha) {
            foreach (var obj in m_FoldOutCollapsedToHide) {
                obj.SetAlpha(alpha);
            }
        }

        #endregion // Animations
    }
}
