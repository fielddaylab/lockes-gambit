using System;
using System.Collections;
using BeauRoutine;
using BeauUtil.Tags;
using FieldDay.Scripting;
using FieldDay.UI.Widgets;
using SpaceFab.UI;
using UnityEngine;

namespace SpaceFab.Narrative {
    /// <summary>
    /// In-minigame dialogue printer. Differs from DialogueBox in three ways:
    ///   - CompleteLine returns immediately (no Next-button gate), so script threads advance
    ///     line-to-line without waiting for player input.
    ///   - Stays visible after the script thread releases — dismissal is explicit, via the
    ///     close button or the LeafMember surface in DialogueScripting.
    ///   - Does not push input priority. Other minigame UI stays interactable while dialogue
    ///     is on screen.
    /// </summary>
    public sealed class MinigameDialogueBox : BaseSpacefabDialogueBox {
        #region Inspector

        [Header("Animation")]
        [SerializeField] private CanvasGroup m_VisiblityGroup;
        [SerializeField] private float m_FadeDuration = 0.2f;

        [Header("Close")]
        // Optional. When assigned, clicking dismisses the box. Leave null for dialogue that
        // can only be dismissed via the Leaf LeafMember API.
        [SerializeField] private GuiButton m_CloseButton;

        [Header("Button")]
        [SerializeField] private GuiButton m_PrimaryButton;

        #endregion // Inspector

        // Armed by Leaf, waiting for the next line to print. Moved into m_ActiveButtonAction — and
        // cleared — the moment that line starts typing, so a single arm shows the button for
        // exactly one line.
        [NonSerialized] private DialogueButtonAction m_PendingButtonAction;
        // The action the button currently on screen will perform when clicked.
        [NonSerialized] private DialogueButtonAction m_ActiveButtonAction;

        private void Start() {
            m_VisiblityGroup.gameObject.SetActive(false);
            m_VisiblityGroup.alpha = 0f;
            m_VisiblityGroup.blocksRaycasts = false;

            if (m_CloseButton != null) {
                m_CloseButton.OnClick.AddListener(HandleCloseClicked);
            }

            if (m_PrimaryButton != null) {
                m_PrimaryButton.gameObject.SetActive(false);
                m_PrimaryButton.OnClick.AddListener(HandlePrimaryClicked);
            }
        }

        protected override void OnDisable() {
            // No input-priority cleanup needed — the minigame box never pushes priority. But
            // make sure any in-flight fade is stopped so the routine doesn't continue against
            // a disabled GameObject.
            m_Animation.Stop();
            ClearPrimaryButton();
            base.OnDisable();
        }

        private void OnDestroy() {
            if (m_CloseButton != null) {
                m_CloseButton.OnClick.RemoveListener(HandleCloseClicked);
            }

            if (m_PrimaryButton != null) {
                m_PrimaryButton.OnClick.RemoveListener(HandlePrimaryClicked);
            }
        }

        public override IEnumerator TypeLine(TagString text, TagTextData textData, DialogueCharacterState character) {
            // Consume the pending arm here rather than in PrepareTextDisplay: the runtime can
            // re-prepare a single line several times (after vox load, after a mid-line printer
            // swap), but TypeLine runs exactly once per line.
            ApplyPendingButton();
            return base.TypeLine(text, textData, character);
        }

        public override IEnumerator CompleteLine() {
            // No Next-button gate in the minigame variant — line completion is purely a function
            // of typewriter completion, which has already happened by the time TypeLine returned.
            // Yield break lets the script thread immediately advance to whatever comes next.
            yield break;
        }

        protected override void OnThreadReleased() {
            // Stay visible after the script ends. The box hangs around until Dismiss() is
            // called explicitly (close button or LeafMember). Overarching DialogueBox's
            // auto-hide-on-release behaviour is the wrong default here.
            //
            // A button already on screen belongs to the last line printed and stays with it, but an
            // arm the script never got to spend is dropped — otherwise it would leak onto the first
            // line of whatever conversation comes next.
            m_PendingButtonAction = DialogueButtonAction.None;
        }

        /// <summary>
        /// Explicitly dismisses the box: fades out and deactivates. Safe to call when already
        /// hidden (no-op). Invoked by the close-button listener and by the Leaf-callable
        /// dismiss entry point in DialogueScripting.
        /// </summary>
        public void Dismiss() {
            if (!m_IsVisible) { return; }
            m_Animation.Replace(this, AnimateToOff());
        }

        private void HandleCloseClicked() {
            Dismiss();
        }

        #region Primary button

        /// <summary>
        /// Arms the primary button to appear on the next line this box prints, carrying the given
        /// action's label and behavior. Arming again before that line prints replaces the pending
        /// action. Invoked from the Leaf-callable surface in DialogueScripting.
        /// </summary>
        public void ArmPrimaryButton(DialogueButtonAction action) {
            m_PendingButtonAction = action;
        }

        // Promotes the pending arm to the active one and repaints the button to match. Runs at the
        // start of every line, so a line with nothing armed is what takes the button back down.
        private void ApplyPendingButton() {
            m_ActiveButtonAction = m_PendingButtonAction;
            m_PendingButtonAction = DialogueButtonAction.None;

            if (m_PrimaryButton == null) { return; }

            string label = DialogueButtonActionUtility.GetLabel(m_ActiveButtonAction);
            if (label == null) {
                m_ActiveButtonAction = DialogueButtonAction.None;
                m_PrimaryButton.gameObject.SetActive(false);
                return;
            }

            // WideButton's primary graphic is its TMP label, so TextGraphic is the text renderer.
            m_PrimaryButton.TextGraphic.SetText(label);
            m_PrimaryButton.gameObject.SetActive(true);
        }

        // Drops both the armed and the on-screen action and hides the button. Used by the paths
        // that take the whole box away, where no line boundary will come along to do it.
        private void ClearPrimaryButton() {
            m_PendingButtonAction = DialogueButtonAction.None;
            m_ActiveButtonAction = DialogueButtonAction.None;

            if (m_PrimaryButton != null) {
                m_PrimaryButton.gameObject.SetActive(false);
            }
        }

        // The button stays up after a click: the action typically covers the box with another
        // panel, and the player should be able to trigger it again after closing that panel.
        private void HandlePrimaryClicked() {
            DialogueButtonActionUtility.Invoke(m_ActiveButtonAction);
        }

        #endregion // Primary button

        #region Animations

        protected override IEnumerator AnimateToOn() {
            m_IsVisible = true;
            m_VisiblityGroup.gameObject.SetActive(true);
            m_VisiblityGroup.blocksRaycasts = true;
            yield return m_VisiblityGroup.FadeTo(1f, m_FadeDuration);
        }

        protected override IEnumerator AnimateToOff() {
            m_IsVisible = false;
            m_VisiblityGroup.blocksRaycasts = false;
            yield return m_VisiblityGroup.FadeTo(0f, m_FadeDuration);
            m_VisiblityGroup.gameObject.SetActive(false);

            // Cleared after the fade so the button goes out with the box rather than popping away
            // first. blocksRaycasts already made it non-interactive at the start of the fade.
            ClearPrimaryButton();
        }

        #endregion // Animations
    }
}
