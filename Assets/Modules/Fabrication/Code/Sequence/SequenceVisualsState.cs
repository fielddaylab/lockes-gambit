using BeauRoutine;
using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication.LayerInstructions;
using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// Holds the visuals layer for the Fabrication sequence: references to the two pooled
    /// on-screen step cards (always loaded; never destroyed), the in-flight transition routine,
    /// and the polled dirty flags written by SequenceUtility when the sequence resets, advances,
    /// or completes. Consumed by SequenceVisualsSystem in LateUpdate.
    /// </summary>
    public class SequenceVisualsState : SharedStateComponent, IRegistrationCallbacks
    {
        // Authored on the Sequence Panel Group prefab — both cards live there at the same local
        // position. Which one is "in front" is controlled by sibling order, not by translation.
        public RectTransform SequencePanelGroup;
        public Image SequenceCardBG;
        public Image SequenceCardHeaderBG;
        public TMP_Text SequenceCardHeaderText;
        public Image GlitchedText;

        public CanvasGroup PanelCanvasGroup;

        // Layer progress panel (one row per layer). Rebuilt on reset, updated on advance/completion.
        public LayerInstructionPanel LayerInstructionPanel;
        public SequenceCard CardSlotA;
        public SequenceCard CardSlotB;

        // Placeholder transition timing. The current animation just toggles CanvasGroup alpha on
        // the two cards across this duration; replace with a real tween when the visual is final.
        public float TransitionDurationSeconds = 0.4f;
        public float PanelTransitionDurationSeconds = 0.2f;

        // Runtime pointers tracking which authored slot is currently in front vs. behind. Swapped
        // by SequenceVisualsUtility.AdvanceRoutine. Initialized in OnRegister and on every Reset.
        [NonSerialized] public SequenceCard FrontCard;
        [NonSerialized] public SequenceCard BackCard;

        // BeauRoutine handle for the active transition animation.
        [NonSerialized] public Routine TransitionRoutine;

        // Set by SequenceUtility.ResetSequence. Consumed by SequenceVisualsSystem to rebuild both
        // cards' content from step 0 / step 1 and reset which slot is in front.
        [NonSerialized] public bool ResetRequested;

        // Set by SequenceUtility.AdvanceStep on a non-final advance. Drives the swap-and-repopulate
        // transition.
        [NonSerialized] public bool AdvanceRequested;

        // for use in moving the top panel off frame
        [NonSerialized] public bool MoveAwayRequested;

        // Set by SequenceUtility.AdvanceStep on the final advance. Hides both cards permanently
        // until the next reset.
        [NonSerialized] public bool CompletionRequested;

        public void OnRegister()
        {
            FrontCard = CardSlotA;
            BackCard = CardSlotB;
            ResetRequested = false;
            AdvanceRequested = false;
            CompletionRequested = false;
            
            SequencePanelGroup.anchoredPosition = new Vector2(0, 250);

            // Start hidden. Cards only become visible once ModeTransitionSystem transitions into
            // AttemptLeadIn and sets ResetRequested (consumed by SequenceVisualsSystem).
            SequenceVisualsUtility.HideAllCards(this);
        }

        public void OnDeregister()
        {
            TransitionRoutine.Stop();
        }
    }

    /// <summary>
    /// Pairs with SequenceVisualsState. Populates and animates the two pooled SequenceCards in
    /// response to sequence reset / advance / completion signals. The system consumes the dirty
    /// flags and calls these methods; SequenceUtility never calls them directly.
    /// </summary>
    public static class SequenceVisualsUtility
    {
        // Resets the pool back to "CardSlotA is front, CardSlotB is back" and repopulates both
        // with the current and next steps. Called when ResetRequested is observed. Hides either
        // card whose step index is out of range (no level, no steps, last step alone, etc.).
        public static void RebuildAllCards(SequenceVisualsState visualsState, SequenceState sequenceState)
        {
            // 1. Stop any in-flight transition; the rebuild supersedes it.
            visualsState.TransitionRoutine.Stop();

            // 2. Restore the canonical front/back pointers and bring the front card to the top.
            visualsState.FrontCard = visualsState.CardSlotA;
            visualsState.BackCard = visualsState.CardSlotB;
            BringToFront(visualsState.FrontCard);

            // 3. Bail with both cards hidden if there is nothing to display.
            if (sequenceState.Level == null || sequenceState.Level.Sequence.Steps == null || sequenceState.Level.Sequence.Steps.Length == 0) {
                SetCardVisible(visualsState.FrontCard, false);
                SetCardVisible(visualsState.BackCard, false);
                return;
            }

            SequenceLookup lookup = Find.GlobalAsset<SequenceLookup>();
            WaferStepUILookup waferLookup = Find.GlobalAsset<WaferStepUILookup>();

            int currentIndex = sequenceState.CurrentStepIndex;
            FabricationStep[] steps = sequenceState.Level.Sequence.Steps;

            // 4. Populate and show the front card with the current step (if in range).
            if (currentIndex >= 0 && currentIndex < steps.Length) {
                StepRuntimeData runtime = GetRuntime(sequenceState, currentIndex);
                PopulateCard(visualsState, visualsState.FrontCard, steps[currentIndex], runtime, lookup, waferLookup);
                SpacefabGame.Events.Dispatch(GameEvents.FabInstructionUpdated, EvtArgs.Box((steps[currentIndex].StepId.ToString(), false)));
                SetCardVisible(visualsState.FrontCard, true);
                visualsState.GlitchedText.enabled = runtime.IsGlitched;
                visualsState.FrontCard.StationLabelText.enabled = !runtime.IsGlitched;
                visualsState.BackCard.StationLabelText.enabled = !runtime.IsGlitched;
            } else {
                SetCardVisible(visualsState.FrontCard, false);
            }

            // 5. Pre-load the back card with the next step (if in range) but keep it hidden — it
            //    only becomes visible when AdvanceRoutine reveals it during the transition.
            int nextIndex = currentIndex + 1;
            if (nextIndex < steps.Length) {
                PopulateCard(visualsState, visualsState.BackCard, steps[nextIndex], GetRuntime(sequenceState, nextIndex), lookup, waferLookup);
            }
            SetCardVisible(visualsState.BackCard, false);

            visualsState.TransitionRoutine.Replace(InitialMoveIntoFrame(visualsState));

            // The panel background/header is shared by both cards, so it follows the current
            // (front) step's chunk, not the pre-loaded next step.
            if (currentIndex >= 0 && currentIndex < steps.Length) {
                ApplyChunkVisuals(visualsState, steps[currentIndex].Chunk, lookup);
            }
        }

        // Sets the shared sequence panel background, header background, and header text for the
        // given chunk.
        private static void ApplyChunkVisuals(SequenceVisualsState visualsState, SequenceChunk chunk, SequenceLookup lookup)
        {
            SequenceChunkEntry chunkEntry = lookup.GetChunk(chunk);
            visualsState.SequenceCardBG.sprite = chunkEntry.ChunkSequenceBG;
            visualsState.SequenceCardHeaderBG.sprite = chunkEntry.ChunkSequenceHeaderBG;
            switch (chunk)
            {
                case SequenceChunk.N:
                    visualsState.SequenceCardHeaderText.text = "N-Type Layer";
                    break;
                case SequenceChunk.P:
                    visualsState.SequenceCardHeaderText.text = "P-Type Layer";
                    break;
                case SequenceChunk.Metal:
                    visualsState.SequenceCardHeaderText.text = "Metal Layer";
                    break;
            }
        }

        public static IEnumerator InitialMoveIntoFrame(SequenceVisualsState visualsState)
        {
            yield return visualsState.SequencePanelGroup.AnchorPosTo(new Vector2(0, 10), visualsState.TransitionDurationSeconds);
        }

        // Non-final step advance. Reveals the pre-loaded back card (which already holds the
        // just-advanced-to step's content), hides the old front, swaps the front/back pointers,
        // and pre-loads the now-back card with the new "next" step — kept hidden until the next
        // transition. The placeholder visual is a flat CanvasGroup-alpha toggle.
        public static IEnumerator AdvanceRoutine(SequenceVisualsState visualsState, SequenceState sequenceState, int justCompletedIndex)
        {
            // 1. Reveal the back card — it already holds the new current step's content
            //    (pre-loaded on the previous Reset or Advance). Hide the outgoing front.
            StepRuntimeData incomingRuntime = GetRuntime(sequenceState, sequenceState.CurrentStepIndex);
            SetCardVisible(visualsState.BackCard, true);
            SetCardVisible(visualsState.FrontCard, false);
            visualsState.GlitchedText.enabled = incomingRuntime.IsGlitched;
            visualsState.FrontCard.StationLabelText.enabled = !incomingRuntime.IsGlitched;
            visualsState.BackCard.StationLabelText.enabled = !incomingRuntime.IsGlitched;

            // Switch the shared panel BG/header to the incoming step's chunk.
            FabricationStep[] advanceSteps = sequenceState.Level != null ? sequenceState.Level.Sequence.Steps : null;
            if (advanceSteps != null && sequenceState.CurrentStepIndex < advanceSteps.Length) {
                ApplyChunkVisuals(visualsState, advanceSteps[sequenceState.CurrentStepIndex].Chunk, Find.GlobalAsset<SequenceLookup>());
            }

            yield return visualsState.SequencePanelGroup.AnchorPosTo(new Vector2(0, 10), visualsState.TransitionDurationSeconds);

            // 2. Swap which authored slot is front vs. back. The newly-visible card becomes the
            //    front; the just-hidden card becomes the back and will be re-used for step N+2.
            SequenceCard newFront = visualsState.BackCard;
            SequenceCard newBack = visualsState.FrontCard;
            visualsState.FrontCard = newFront;
            visualsState.BackCard = newBack;
            BringToFront(newFront);

            // 3. Pre-load the new back card with the step after the new current step (if any),
            //    but keep it hidden — it only becomes visible at the next advance. AdvanceStep has
            //    already incremented CurrentStepIndex, so the new upcoming "next" step is
            //    CurrentStepIndex + 1 == justCompletedIndex + 2.
            FabricationStep[] steps = sequenceState.Level != null ? sequenceState.Level.Sequence.Steps : null;
            int newBackIndex = justCompletedIndex + 2;
            if (steps != null && newBackIndex < steps.Length) {
                SequenceLookup lookup = Find.GlobalAsset<SequenceLookup>();
                WaferStepUILookup waferLookup = Find.GlobalAsset<WaferStepUILookup>();
                PopulateCard(visualsState, newBack, steps[newBackIndex], GetRuntime(sequenceState, newBackIndex), lookup, waferLookup);
            }
            SpacefabGame.Events.Dispatch(GameEvents.FabInstructionUpdated, EvtArgs.Box((steps[justCompletedIndex + 1].StepId.ToString(), false)));
            SetCardVisible(newBack, false);
        }

        public static IEnumerator MoveOffscreenRoutine(SequenceVisualsState visualsState)
        {
            yield return visualsState.SequencePanelGroup.AnchorPosTo(new Vector2(0, 250), visualsState.TransitionDurationSeconds);
        }

        // Final-step completion. Hides both cards and leaves them hidden until the next reset.
        public static IEnumerator CompletionRoutine(SequenceVisualsState visualsState)
        {
            SetCardVisible(visualsState.FrontCard, false);
            SetCardVisible(visualsState.BackCard, false);

            yield return visualsState.TransitionDurationSeconds;
        }

        // Pulls per-step display data from SequenceLookup and WaferStepUILookup and applies it to
        // the card. ConvertToB is optional: if no entry id is authored, the overlay renderer is
        // disabled so nothing layers on top of the base wafer.
        private static void PopulateCard(SequenceVisualsState visualState, SequenceCard card, FabricationStep step, StepRuntimeData runtime, SequenceLookup lookup, WaferStepUILookup waferLookup)
        {
            SequenceStepEntry entry = lookup.GetStep(step.StepId);

            // Wafer images: ConvertFrom + ConvertToA always set; ConvertToB only if authored.
            card.WaferState1.sprite = waferLookup.GetSprite(entry.ConvertFrom);
            if (entry.ConvertToA.Equals("dopant-np"))
            {
                switch (step.Chunk)
                {
                    case SequenceChunk.N:
                        card.WaferState2Base.sprite = waferLookup.GetSprite("dopant-n");
                        break;
                    case SequenceChunk.P:
                        card.WaferState2Base.sprite = waferLookup.GetSprite("dopant-np");
                        break;
                    case SequenceChunk.Metal:
                        card.WaferState2Base.sprite = waferLookup.GetSprite("metal");
                        break;
                }
            }
            else
            {
                card.WaferState2Base.sprite = waferLookup.GetSprite(entry.ConvertToA);
            }

            if (entry.ConvertToB.IsEmpty) {
                card.WaferState2Overlay.enabled = false;
            } else {
                card.WaferState2Overlay.enabled = true;
                card.WaferState2Overlay.sprite = waferLookup.GetSprite(entry.ConvertToB);
            }

            // Station icon and text labels.
            card.StationIcon.sprite = entry.StationIconSprite;
            card.StationLabelText.text = entry.StationLabel;
            card.InstructionLabelText.text = entry.InstructionLabel;

            // Shared panel BG/header is NOT set here: this also runs for the hidden back card, so
            // it would show the next step's chunk early. See ApplyChunkVisuals.

            // TODO: when runtime.IsGlitched, apply lookup.GlitchOverlaySprite / GlitchOverlayText.
            // Deferred until the card prefab carries a dedicated glitch overlay child
        }

        // Looks up the per-step runtime data (IsGlitched, WasCheckpointReached) for the given step
        // index, returning default if StepRuntime is unallocated or out of range.
        public static StepRuntimeData GetRuntime(SequenceState sequenceState, int stepIndex)
        {
            if (sequenceState.StepRuntime == null || stepIndex < 0 || stepIndex >= sequenceState.StepRuntime.Length) {
                return default;
            }
            return sequenceState.StepRuntime[stepIndex];
        }

        // Hides both pooled cards. Used by OnRegister to enforce "cards start hidden until the
        // first lead-in transition", and as the terminal state after CompletionRoutine.
        public static void HideAllCards(SequenceVisualsState visualsState)
        {
            SetCardVisible(visualsState.CardSlotA, false);
            SetCardVisible(visualsState.CardSlotB, false);
        }

        // Toggles a card's CanvasGroup visibility + interactability in one place so the routines
        // can't drift apart on what "hidden" means.
        private static void SetCardVisible(SequenceCard card, bool visible)
        {
            if (card == null || card.Group == null) {
                return;
            }
            card.Group.alpha = visible ? 1f : 0f;
            card.Group.interactable = visible;
            card.Group.blocksRaycasts = visible;
        }

        // Puts a card on top by making it the last sibling under its parent. Sibling order is how
        // the pool encodes "in front" vs. "behind" — both cards share a transform position.
        private static void BringToFront(SequenceCard card)
        {
            if (card == null) {
                return;
            }
            card.transform.SetAsLastSibling();
        }
    }
}
