using BeauUtil;
using BeauUtil.Variants;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Materials;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Recomputes HypothesisViewModelState only when something has
    /// requested a rebuild. Rebuild triggers, in order of cost to check:
    ///   - ChamberInterfacerState.SlotMaterialUpdatedThisFrame for the
    ///     Primary slot (the slotted material changed; clears the
    ///     selection)
    ///   - HypothesisViewModelState.NeedsRebuild (an explicit raise from
    ///     ObservationCollectSystem, HypothesisSubmitSystem, or
    ///     ResearchTransitionSystem on minigame entry)
    /// When none are set, the system early-returns without touching the
    /// viewmodel — its visual consumers continue to read the same state
    /// that was last computed.
    ///
    /// On rebuild:
    /// * Applies a pending hypothesis-selection click (label from
    ///   ResearchUIInputState.AddHypothesisLabel). Dynamic labels require
    ///   the doping chamber + a slotted substrate for their context;
    ///   otherwise the selection is rejected.
    /// * Builds the slot view (SlotLabels / SlotContexts / SlotCount /
    ///   SlotLockedMask) from researchState.Observations in insertion
    ///   order, deduped, capped at MaxObservationSlots.
    /// * Auto-clears a selection whose label has since been fulfilled by
    ///   some known material — the confirm path raises NeedsRebuild after
    ///   flipping the record bit, so a verified hypothesis empties itself.
    /// * Decomposes the selected label's first-registered definition for
    ///   HypothesisLeafCount; VerifyButtonVisible is true when the slot
    ///   view is full against that count.
    /// </summary>
    public class HypothesisViewModelSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 100, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadWriteShared<HypothesisViewModelState>()
                    .ReadWriteShared<ResearchUIInputState>()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadShared<ResearchMinigameState>()
            );
        }

        // Scratch for first-definition decomposition; rebuilds are rare
        // and single-threaded, so one shared buffer suffices.
        [NotStateful] private static readonly List<MaterialObservationEntry> s_LeafScratch = new List<MaterialObservationEntry>(8);
        [NotStateful] private static readonly StringHash32[] s_NullContext = new StringHash32[] { StringHash32.Null };

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out HypothesisViewModelState viewModelState,
                out ResearchUIInputState inputState,
                out ChamberInterfacerState interfacerState
            );

            // Rebuild gating. If no trigger fired this frame, leave the
            // viewmodel as-is and clear the per-frame "changed" signal so
            // visual consumers can short-circuit. Slot changes only count
            // when the Primary slot moved — Secondary updates are for
            // dual-slot chambers (Combiner, future) and don't affect the
            // hypothesis viewmodel. The hypothesis input flags are listed
            // here because this system is their only consumer: nothing
            // else raises NeedsRebuild on their behalf.
            bool slotChanged = interfacerState.SlotMaterialUpdatedThisFrame;
            bool hypothesisInput = inputState.HypothesisSelectedClickedThisFrame
                || inputState.RemoveHypothesisClickedThisFrame;
            if (!viewModelState.NeedsRebuild && !slotChanged && !hypothesisInput) {
                viewModelState.HypothesisChangedThisFrame = false;
                return;
            }
            viewModelState.NeedsRebuild = false;

            ResearchMinigameState researchState = Find.State<ResearchMinigameState>();

            bool prevSelected = viewModelState.HypothesisSelected;
            MaterialPropertyLabel prevLabel = viewModelState.HypothesisLabel;
            StringHash32 prevContext = viewModelState.HypothesisContext;
            uint prevSlotLocked = viewModelState.SlotLockedMask;
            int prevSlotCount = viewModelState.SlotCount;
            bool prevSubmit = viewModelState.VerifyButtonVisible;

            bool isDopingChamber = interfacerState.ActiveChamber == ActiveChamberKind.Doping;

            // 1. Apply this frame's hypothesis edits. Every write to the
            // selection lands below the prev-value snapshot above — one
            // written before it would compare equal to itself, report no
            // change, and leave the wiki's property chip greyed.
            //
            // Deselect, either because the player asked or because
            // swapping the sample abandons a claim about the material that
            // just left the chamber.
            if (slotChanged || inputState.RemoveHypothesisClickedThisFrame) {
                if (viewModelState.HypothesisSelected) {
                    viewModelState.HypothesisSelected = false;
                    viewModelState.HypothesisContext = StringHash32.Null;
                    ScriptUtility.Trigger(ResearchScriptTriggers.OnPropertyRemoved);
                }
            }

            // Dynamic labels need a substrate context, which only the
            // doping chamber provides — selections that can't resolve one
            // are rejected outright. A property the sample is already
            // confirmed for is caught by the auto-clear below.
            if (inputState.HypothesisSelectedClickedThisFrame) {
                MaterialPropertyLabel label = inputState.AddHypothesisLabel;
                StringHash32 context = StringHash32.Null;
                bool accepted = true;
                if (MaterialPropertyLabelUtility.IsDynamic(label)) {
                    MaterialAsset substrate = interfacerState.PrimarySlot != null ? interfacerState.PrimarySlot.CurrentMaterial : null;
                    if (isDopingChamber && substrate != null) {
                        context = substrate.AssetId;
                    } else {
                        accepted = false;
                    }
                }
                if (accepted) {
                    viewModelState.HypothesisSelected = true;
                    viewModelState.HypothesisLabel = label;
                    viewModelState.HypothesisContext = context;

                    ScriptUtility.WriteVariable(new TableKeyPair("research", "propertyId"), label.ToString());
                    ScriptUtility.Trigger(ResearchScriptTriggers.OnPropertyAdded);
                }
            }

            // 2. Resolve the slotted material (the sample under test —
            // secondary in the doping chamber, primary elsewhere).
            ResearchSlot slot = isDopingChamber ?
                interfacerState.SecondarySlot : interfacerState.PrimarySlot;
            MaterialAsset slottedMaterial = slot != null ? slot.CurrentMaterial : null;
            StringHash32 slottedId = slottedMaterial != null ? slottedMaterial.AssetId : StringHash32.Null;

            // 3. Build the slot view from researchState.Observations in
            // insertion order, skipping any duplicate (label, context)
            // already in the slot buffer. The ancestor-confirmed auto-lock
            // pass was retired with the page model; SlotLockedMask stays
            // for parity but is always 0 today.
            int slotCap = HypothesisViewModelState.MaxObservationSlots;
            int slotCount = 0;
            uint slotLockedMask = 0;
            MaterialPropertyLabel[] slotLabels = viewModelState.SlotLabels;
            StringHash32[] slotContexts = viewModelState.SlotContexts;

            if (slottedMaterial != null
                && researchState.Observations.TryGetValue(slottedId, out var pickedList)) {
                int pickedCount = pickedList.Count;
                for (int p = 0; p < pickedCount && slotCount < slotCap; p++) {
                    MaterialPropertyLabel label = MaterialObservationListUtility.GetLabel(pickedList, p);
                    StringHash32 context = MaterialObservationListUtility.GetContext(pickedList, p);
                    if (ContainsSlot(slotLabels, slotContexts, slotCount, label, context)) {
                        continue;
                    }
                    slotLabels[slotCount] = label;
                    slotContexts[slotCount] = context;
                    slotCount++;
                }
            }

            viewModelState.SlotCount = slotCount;
            viewModelState.SlotLockedMask = slotLockedMask;

            // 4. Auto-clear a selection the slotted material has already
            // been confirmed for — the hypothesis is answered, so the
            // panel empties. Scoped to this material on purpose: the same
            // property can still be hunted on other samples, which a
            // contract asking for two dopants of the same type requires.
            // Same predicate the confirm path short-circuits on, so the
            // clear can never disagree with what a submit would do.
            if (viewModelState.HypothesisSelected && slottedMaterial != null
                && ResearchStateUtility.HasConfirmed(researchState, slottedId, viewModelState.HypothesisLabel, viewModelState.HypothesisContext)) {
                viewModelState.HypothesisSelected = false;
                viewModelState.HypothesisContext = StringHash32.Null;
            }

            // 5. Leaf count of the selected label's first-registered
            // definition. Submit shows whenever the slot view is full,
            // regardless of correctness — the submit handler validates
            // the picks and removes any that don't hold up.
            int leafCount = 0;
            if (viewModelState.HypothesisSelected) {
                leafCount = CountFirstDefinitionLeaves(viewModelState.HypothesisLabel);
                if (leafCount > slotCap) {
                    leafCount = slotCap;
                }
            }
            viewModelState.HypothesisLeafCount = leafCount;
            viewModelState.VerifyButtonVisible = viewModelState.HypothesisSelected && slotCount > 0;

            // 6. Frame-flag — any change drives the panel's LateUpdate render.
            viewModelState.HypothesisChangedThisFrame =
                viewModelState.HypothesisSelected != prevSelected
                || (viewModelState.HypothesisSelected && viewModelState.HypothesisLabel != prevLabel)
                || viewModelState.HypothesisContext != prevContext
                || slotCount != prevSlotCount
                || slotLockedMask != prevSlotLocked
                || viewModelState.VerifyButtonVisible != prevSubmit;
        }

        // Returns true if any of slotLabels[0..count) matches (label, context).
        private static bool ContainsSlot(MaterialPropertyLabel[] slotLabels, StringHash32[] slotContexts, int count, MaterialPropertyLabel label, StringHash32 context) {
            for (int i = 0; i < count; i++) {
                if (slotLabels[i] == label && slotContexts[i] == context) {
                    return true;
                }
            }
            return false;
        }

        // Leaf count of the label's first-registered definition,
        // decomposed with a null context (contexts are assigned during
        // verification). 0 when no definition is registered.
        private static int CountFirstDefinitionLeaves(MaterialPropertyLabel label) {
            MaterialPropertyDefinitionAsset registry = Find.GlobalAsset<MaterialPropertyDefinitionAsset>();
            if (registry == null) {
                return 0;
            }
            MaterialPropertyDefinition[] defs = registry.GetDefinitions(label);
            if (defs.Length == 0) {
                Debug.LogWarningFormat("[HypothesisViewModelSystem] No MaterialPropertyDefinition registered for label '{0}'.", label);
                return 0;
            }
            s_LeafScratch.Clear();
            MaterialPropertyDefinitionUtility.DecomposeToObservations(defs[0], s_NullContext, s_LeafScratch);
            return s_LeafScratch.Count;
        }
    }

    /// <summary>
    /// Mutators paired with HypothesisViewModelState. Other systems call
    /// RequestRebuild when their work invalidates the viewmodel — e.g.
    /// ObservationCollectSystem after a successful add/remove,
    /// HypothesisSubmitSystem after a successful confirmation, or
    /// ResearchTransitionSystem on minigame entry.
    /// HypothesisViewModelSystem clears the flag once it has recomputed.
    /// ChamberInterfacerState.SlotMaterialUpdatedThisFrame already implies
    /// rebuild and does not need an explicit call.
    /// </summary>
    public static class HypothesisViewModelUtility {
        public static void RequestRebuild(HypothesisViewModelState viewModelState) {
            if (viewModelState == null) return;
            viewModelState.NeedsRebuild = true;
        }
    }
}
