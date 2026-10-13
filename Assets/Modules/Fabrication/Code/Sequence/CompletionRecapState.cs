using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.SharedState;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Sequence
{
    /// <summary>
    /// Holds the visuals layer for the step-completion recap: references to the five pooled recap
    /// cards (authored on Sequence Completion Recap.prefab), layout positions, the mystery sprite
    /// for future steps, tunable durations for each animation stage, and dirty-flag state consumed
    /// by CompletionRecapSystem.
    /// </summary>
    public class CompletionRecapState : SharedStateComponent, IRegistrationCallbacks
    {
        // Authored cards in left-to-right order. Index meaning relative to the just-completed step:
        // 0 = -2 (prior-prior), 1 = -1 (prior), 2 = 0 (just completed, center), 3 = +1, 4 = +2.
        public CompletionRecapCard[] CardSlots;

        // Per-slot offset applied around the panel-root origin. Slot i's landing localPosition is
        // (i - 2) * SlotSpacing, so slot 2 (center) sits at the origin and the row spreads outward.
        // Author the panel-root transform so that "origin" lines up with the desired screen center.
        public Vector3 SlotSpacing = new Vector3(260f, 0f, 0f);

        // Offscreen positions used as slide-in starts for slots 0/1 and slots 3/4 respectively.
        public Vector3 OffscreenLeftLocalPos;
        public Vector3 OffscreenRightLocalPos;

        // WaferStepUILookup entry id used as the "mystery" image on cards for steps the player
        // hasn't reached yet. Picked from the same dropdown as SequenceStepEntry's ConvertFrom /
        // ConvertToA / ConvertToB; the corresponding sprite is resolved at runtime via
        // WaferStepUILookup.GetSprite(UnknownStepId).
        [WaferStepUIRef] public SerializedHash32 UnknownStepId;

        // Tunables for each animation stage.
        public float WaferAppearSeconds = 0.35f;
        public float CardStampSeconds = 0.20f;
        public float SlideSeconds = 0.30f;
        public float LabelFadeInSeconds = 0.15f;
        public float HoldSeconds = 0.80f;
        public float FadeOutSeconds = 0.30f;

        // BeauRoutine handle for the active recap.
        [NonSerialized] public Routine RecapRoutine;

        // One-shot flag raised by SequenceUtility.AdvanceStep. CompletionRecapSystem consumes it,
        // captures RecapJustCompletedIndex, and starts the recap routine.
        [NonSerialized] public bool RecapRequested;

        // Step index that just completed. Captured at the moment the recap is requested so the
        // routine reads a stable value even as SequenceState.CurrentStepIndex moves forward.
        [NonSerialized] public int RecapJustCompletedIndex;

        // True while RecapRoutine is in flight. CompletionRecapSystem checks this each frame to
        // re-arm StationControlState.ExitTimerExternalHold (which holds the exit phase open).
        [NonSerialized] public bool RecapInProgress;

        public void OnRegister()
        {
            RecapRequested = false;
            RecapInProgress = false;
            RecapJustCompletedIndex = 0;
            CompletionRecapUtility.HideAll(this);
        }

        public void OnDeregister()
        {
            RecapRoutine.Stop();
        }
    }

    /// <summary>
    /// Pairs with CompletionRecapState. Holds the PlayRecap coroutine that drives the 5-stage
    /// recap animation, plus populate / show / hide helpers. CompletionRecapSystem owns flag
    /// consumption and post-recap fan-out to SequenceVisualsState.
    /// </summary>
    public static class CompletionRecapUtility
    {
        // The five-slot animation. Stages: wafer appear -> card stamp -> side cards slide in ->
        // hold -> fade out. Out-of-range side slots (e.g. for the first or last step) stay hidden.
        public static IEnumerator PlayRecap(CompletionRecapState recapState, SequenceState sequenceState, int justCompletedIndex)
        {
            // 1. Guard against missing level data or a bad index.
            if (sequenceState.Level == null || sequenceState.Level.Sequence.Steps == null) {
                yield break;
            }
            FabricationStep[] steps = sequenceState.Level.Sequence.Steps;
            if (justCompletedIndex < 0 || justCompletedIndex >= steps.Length) {
                yield break;
            }
            if (recapState.CardSlots == null || recapState.CardSlots.Length < 5) {
                yield break;
            }

            HideAll(recapState);

            SequenceLookup lookup = Find.GlobalAsset<SequenceLookup>();
            WaferStepUILookup waferLookup = Find.GlobalAsset<WaferStepUILookup>();

            // 2. Compute which side slots map to in-range steps. Slot 2 is always the center.
            int[] slotOffsets = { -2, -1, 0, 1, 2 };
            int[] stepIndices = new int[5];
            bool[] slotVisible = new bool[5];
            for (int i = 0; i < 5; i++) {
                stepIndices[i] = justCompletedIndex + slotOffsets[i];
                slotVisible[i] = stepIndices[i] >= 0 && stepIndices[i] < steps.Length;
            }

            // 3. Stage 1 — wafer appears on the center card. CardBackground is disabled so only
            //    the wafer shows; the background "stamps" in during Stage 2. The slot's transform
            //    and CanvasGroup are reset here so a re-entrant call lands in a clean state.
            CompletionRecapCard centerCard = recapState.CardSlots[2];
            PopulateRecapCard(centerCard, steps, stepIndices[2], justCompletedIndex, lookup, waferLookup, recapState.UnknownStepId, sequenceState);
            if (centerCard.CardBackground != null) {
                centerCard.CardBackground.enabled = false;
            }
            // Hide the label until after the card has scaled to full size — it fades in at the
            // end of stage 2 so it doesn't appear next to a still-small wafer.
            if (centerCard.Label != null) {
                centerCard.Label.alpha = 0f;
            }
            centerCard.Root.localPosition = SlotPositionFor(recapState, 2);
            centerCard.Root.localScale = Vector3.one;
            centerCard.Group.alpha = 0f;
            centerCard.Group.interactable = false;
            centerCard.Group.blocksRaycasts = false;
            yield return centerCard.Group.FadeTo(1f, recapState.WaferAppearSeconds);

            // 4. Stage 2 — card background pops in around the wafer. ScaleTo interpolates from
            //    the current localScale, so we set it to the small starting scale first. Once the
            //    scale finishes, the label fades in quickly so it lands on the full-size card.
            if (centerCard.CardBackground != null) {
                centerCard.CardBackground.enabled = true;
            }
            centerCard.Root.localScale = new Vector3(0.7f, 0.7f, 1f);
            yield return centerCard.Root.ScaleTo(1f, recapState.CardStampSeconds, Axis.XY).Ease(Curve.BackOut);
            if (centerCard.Label != null) {
                yield return centerCard.Label.FadeTo(1f, recapState.LabelFadeInSeconds);
            }

            // 5. Stage 3 — surrounding cards slide in from offscreen. Skip the center (already
            //    onscreen) and any out-of-range slots. MoveTo tweens from the card's current
            //    localPosition (set to the offscreen pos here) to the slot's landing position.
            List<IEnumerator> fadeIns = new List<IEnumerator>();
            for (int i = 0; i < 5; i++) {
                if (i == 2) continue;
                if (!slotVisible[i]) continue;

                CompletionRecapCard card = recapState.CardSlots[i];
                PopulateRecapCard(card, steps, stepIndices[i], justCompletedIndex, lookup, waferLookup, recapState.UnknownStepId, sequenceState);
                if (card.CardBackground != null) {
                    card.CardBackground.enabled = true;
                }
                Vector3 offscreen = i < 2 ? recapState.OffscreenLeftLocalPos : recapState.OffscreenRightLocalPos;
                offscreen.x = SlotPositionFor(recapState, i).x;
                card.Root.localPosition = offscreen;
                card.Root.localScale = Vector3.one;
                card.Group.alpha = 0f;
                card.Group.interactable = false;
                card.Group.blocksRaycasts = false;
                float delay = Math.Abs(i - 2) * 0.12f + Math.Clamp(2 - i, 0, 1) * 0.08f;
                Log.Msg("index: {0}, delay: {1}", i, delay);
                fadeIns.Add(card.Root.MoveTo(SlotPositionFor(recapState, i), recapState.SlideSeconds, Axis.XYZ, Space.Self).Ease(Curve.BackOut).DelayBy(delay));
                fadeIns.Add(card.Group.FadeTo(1f, recapState.SlideSeconds * 0.8f).Ease(Curve.SineIn).DelayBy(delay));
            }
            if (fadeIns.Count > 0) {
                yield return Routine.Combine(fadeIns.ToArray());
            }

            // 6. Stage 4 — hold so the player can read the row.
            yield return recapState.HoldSeconds;

            // 7. Stage 5 — fade all visible slots together, then snap to fully hidden.
            List<IEnumerator> fades = new List<IEnumerator>();
            fades.Add(centerCard.Group.FadeTo(0f, recapState.FadeOutSeconds));
            for (int i = 0; i < 5; i++) {
                if (i == 2) continue;
                if (!slotVisible[i]) continue;
                fades.Add(recapState.CardSlots[i].Group.FadeTo(0f, recapState.FadeOutSeconds));
            }
            yield return Routine.Combine(fades.ToArray());

            HideAll(recapState);
        }

        // Sets the wafer image and label on a card. For steps after justCompletedIndex (future),
        // the wafer image is resolved from the mystery WaferStepUILookup entry id so the player
        // sees a "?" placeholder; the label still shows (gives a preview of upcoming work).
        public static void PopulateRecapCard(CompletionRecapCard card, FabricationStep[] steps, int stepIndex, int justCompletedIndex, SequenceLookup lookup, WaferStepUILookup waferLookup, SerializedHash32 unknownStepId, SequenceState sequence)
        {
            if (card == null) {
                return;
            }
            SequenceStepEntry entry = lookup.GetStep(steps[stepIndex].StepId);
            bool isGlitched  = SequenceVisualsUtility.GetRuntime(sequence, stepIndex).IsGlitched;
            if (stepIndex > justCompletedIndex) {
                card.Wafer.sprite = waferLookup.GetSprite(unknownStepId);
                card.Label.text = isGlitched ? "?" : entry.InstructionLabel;
            } else {
                SequenceChunk chunk = steps[stepIndex].Chunk;
                if (entry.ConvertToA.Equals("dopant-np"))
                {
                    switch (chunk)
                    {
                        case SequenceChunk.N:
                            card.Wafer.sprite = waferLookup.GetSprite("dopant-n");
                            break;
                        case SequenceChunk.P:
                            card.Wafer.sprite = waferLookup.GetSprite("dopant-np");
                            break;
                        case SequenceChunk.Metal:
                            card.Wafer.sprite = waferLookup.GetSprite("metal");
                            break;
                    }
                }
                else
                {
                    card.Wafer.sprite = waferLookup.GetSprite(entry.ConvertToA);
                }
                card.Label.text = entry.InstructionLabel;
            }
        }

        // Computes a slot's landing localPosition: (slotIndex - 2) * SlotSpacing. Slot 2 (center)
        // ends up at the panel-root origin; slots 0/1 lay out to the left, 3/4 to the right.
        private static Vector3 SlotPositionFor(CompletionRecapState recapState, int slotIndex)
        {
            return (slotIndex - 2) * recapState.SlotSpacing;
        }

        // Hides every authored slot. Used by OnRegister to enforce "recap stays invisible until
        // the first request" and as the terminal state after the fade-out finishes.
        public static void HideAll(CompletionRecapState recapState)
        {
            if (recapState.CardSlots == null) {
                return;
            }
            for (int i = 0; i < recapState.CardSlots.Length; i++) {
                SetCardVisible(recapState.CardSlots[i], false);
            }
        }

        // Toggles a card's CanvasGroup visibility + interactability in one place so all callers
        // agree on what "hidden" means. Tweens use FadeTo for animated transitions; this helper
        // is the snap-to-state version (used in HideAll and at the end of the routine).
        private static void SetCardVisible(CompletionRecapCard card, bool visible)
        {
            if (card == null || card.Group == null) {
                return;
            }
            card.Group.alpha = visible ? 1f : 0f;
            card.Group.interactable = visible;
            card.Group.blocksRaycasts = visible;
        }
    }
}
