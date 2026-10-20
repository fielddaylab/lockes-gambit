using System;
using System.Collections;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Tags;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Scripting;
using FieldDay.UI;
using FieldDay.UI.Animation;
using Leaf.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Narrative {
    /// <summary>
    /// Shared base for SpaceFab dialogue printers. Owns the universal pieces — character
    /// name / portrait / tint, typewriter, quip + typing SFX, line-flag bookkeeping, and the
    /// visibility flag — and defers presentation (show / hide animation, completion gating,
    /// dismissal policy, input gating) to subclasses.
    ///
    /// Two concrete subclasses today:
    ///   - DialogueBox: overarching, Next-button-gated, pushes input priority, auto-dismisses
    ///     when the script thread releases.
    ///   - MinigameDialogueBox: no Next button, no input gating, stays up until explicitly
    ///     dismissed (via Leaf call or close button).
    /// </summary>
    public abstract class BaseSpacefabDialogueBox : BaseDialoguePrinter, ITypewriterModule {
        #region Inspector

        [Header("Character")]
        [SerializeField] protected TMP_Text m_CharacterName;
        [SerializeField] protected Image m_CharacterPortrait;
        [SerializeField] protected Graphic[] m_CharacterThemeTint;

        [Header("Text")]
        [SerializeField] protected TMP_Text m_Contents;

        #endregion // Inspector

        [Flags]
        protected enum LineFlags {
            AutoContinue = 0x01,
            IsEnd = 0x02
        }

        [NonSerialized] protected CharacterDef m_CurrentCharacter;
        [NonSerialized] protected DialogueCharacterState m_CurrentCharacterState;
        [NonSerialized] protected LineFlags m_CurrentLineFlags;

        [NonSerialized] protected TypewriterEnumerator m_Typewriter = new TypewriterEnumerator();
        [NonSerialized] protected long m_NextAllowedTypingSfx;
        [NonSerialized] protected Routine m_Animation;
        [NonSerialized] protected bool m_IsVisible;
        [NonSerialized] protected AudioHandle m_CurrentQuip;

        #region BaseDialoguePrinter overrides

        protected override void ConfigureEventHandler(TagStringEventHandler handler) {
            handler.Register(TagEvents.AutoContinue, () => m_CurrentLineFlags |= LineFlags.AutoContinue)
                .Register(TagEvents.InterpretAsClose, () => m_CurrentLineFlags |= LineFlags.IsEnd)
                .Register(TagEvents.PlayQuip, PlayQuipSfx);
        }

        public override void FastForwardLine(int visibleCount, int richCount) {
            m_Contents.maxVisibleCharacters += visibleCount;
        }

        public override IEnumerator TypeLine(TagString text, TagTextData textData, DialogueCharacterState character) {
            if (!m_IsVisible) {
                m_Animation.Stop();
                yield return AnimateToOn();
            } else {
                OnLineReplacedWhileVisible();
            }

            m_Typewriter.Prepare(m_Contents, this, text, textData);
            yield return m_Typewriter;
        }

        public override void UpdateCharacter(DialogueCharacterState character) {
            bool charChanged = !m_IsVisible;

            CharacterDef def = m_CurrentCharacter;
            if (charChanged || character.CharacterId != m_CurrentCharacterState.CharacterId) {
                m_CurrentCharacter = def = character.CharacterId.IsEmpty ? null : Find.NamedAsset<CharacterDef>(character.CharacterId);
                UpdateDecorationColors(def);
                charChanged = true;
            }

            if (charChanged || !string.Equals(character.OverrideName, m_CurrentCharacterState.OverrideName, StringComparison.Ordinal)) {
                string charName;
                if (!string.IsNullOrEmpty(character.OverrideName)) {
                    charName = character.OverrideName;
                } else if (def) {
                    charName = def.DisplayName;
                } else {
                    charName = "[NULL CHARACTER]";
                }

                m_CharacterName.SetText(charName);
            }

            if (charChanged || character.PoseId != m_CurrentCharacterState.PoseId) {
                // TODO: Implement per-pose portraits
                m_CharacterPortrait.sprite = CharacterDef.ResolvePortrait(def);
            }

            m_CurrentCharacterState = character;
        }

        protected override void PrepareTextDisplay(TagString text, DialogueCharacterState character) {
            UpdateCharacter(character);

            m_Contents.SetText(text.RichText);
            m_Contents.maxVisibleCharacters = 0;

            LineFlags flags = 0;
            if (LeafRuntime.PredictEnd(CurrentThread)) {
                flags |= LineFlags.IsEnd;
            }
            m_CurrentLineFlags = flags;
        }

        #endregion // BaseDialoguePrinter overrides

        #region Subclass hooks

        /// <summary>
        /// Implement to animate the box on-screen. TypeLine calls this whenever a line arrives
        /// and the box isn't already visible. Subclasses are responsible for flipping
        /// m_IsVisible to true at the start (or whenever the box should be considered "showing"
        /// for the purposes of skipping AnimateToOn on subsequent lines).
        /// </summary>
        protected abstract IEnumerator AnimateToOn();

        /// <summary>
        /// Implement to animate the box off-screen. Subclasses are responsible for flipping
        /// m_IsVisible to false. Called by subclass-specific dismissal paths — the base does
        /// not invoke this; DialogueBox calls it from OnThreadReleased, MinigameDialogueBox
        /// calls it from its dismiss API.
        /// </summary>
        protected abstract IEnumerator AnimateToOff();

        /// <summary>
        /// Called from TypeLine when a new line arrives while the box is already visible
        /// (i.e. a same-thread line-to-line transition). Default is a no-op; subclasses can
        /// override to play a pop / shake / attention animation on the existing fold-out.
        /// </summary>
        protected virtual void OnLineReplacedWhileVisible() { }

        #endregion // Subclass hooks

        #region Audio

        protected void PlayTypingSound() {
            long ts = Frame.Timestamp();
            if (ts >= m_NextAllowedTypingSfx) {
                if (m_CurrentCharacter && !m_CurrentCharacter.CharacterTypeEvent.IsEmpty) {
                    Sfx.Play(m_CurrentCharacter.CharacterTypeEvent);
                    m_NextAllowedTypingSfx = Frame.AdjustTimestamp(ts, 0.05f);
                }
            }
        }

        protected void PlayQuipSfx(TagEventData evtData, object context) {
            StringHash32 art = evtData.Argument0.AsStringHash();
            if (m_CurrentCharacter) {
                StringHash32 quipEvent = m_CurrentCharacter.DefaultQuip;
                // TODO: find quip
                if (!quipEvent.IsEmpty) {
                    Sfx.Stop(m_CurrentQuip);
                    m_CurrentQuip = Sfx.Play(quipEvent);
                }
            }
        }

        private void UpdateDecorationColors(CharacterDef def) {
            Color tint;
            if (def) {
                tint = def.DialogueTint;
            } else {
                tint = Color.white;
            }

            foreach (var tintable in m_CharacterThemeTint) {
                tintable.SetColor(tint);
            }
        }

        #endregion // Audio

        #region ITypewriterModule

        protected virtual float GetActiveTypingDelayMultiplier() {
            return 0.4f;
        }

        void ITypewriterModule.GetTypewriterParameters(out float delayMultiplier, out TypewriterTimingTable timingTable) {
            if (CurrentThread.IsSkipping()) {
                delayMultiplier = -1;
                timingTable = default;
            } else {
                delayMultiplier = GetActiveTypingDelayMultiplier();
                timingTable = DialoguePrinting.DefaultTimingTable;
            }
        }

        void ITypewriterModule.OnTypewriterType(char charValue, DialogueCharacterClass charClass) {
            if (charClass == DialogueCharacterClass.LetterOrDigit) {
                PlayTypingSound();
            }
        }

        #endregion // ITypewriterModule
    }
}
