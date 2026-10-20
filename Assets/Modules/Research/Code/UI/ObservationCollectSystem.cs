using BeauUtil;
using BeauUtil.Variants;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab.Materials;

namespace SpaceFab.Research {
    /// <summary>
    /// Handles the player's add/remove observation gestures. Reads the
    /// slotted material via ChamberInterfacerState.PrimarySlot. Add comes
    /// from a wiki observation chip; remove comes from clicking a
    /// non-locked filled slot in the sample panel, or the same wiki chip
    /// again. Both fan out into the existing ResearchInventoryUtility
    /// add/remove helpers.
    ///
    /// Observations only — hypothesis selection and removal are applied by
    /// HypothesisViewModelSystem, which owns those fields and has to write
    /// them after it snapshots the previous values for change detection.
    ///
    /// Runs on LateUpdate at order 50 so it sits *after* Unity's
    /// EventSystem has dispatched any same-frame click (CursorHint's
    /// onClick fires during the EventSystem update, which on this
    /// project lands after FieldDay's Update phase but before LateUpdate)
    /// and *before* HypothesisViewModelSystem at order 100 — so a click
    /// added this frame is visible in the viewmodel rebuild that same
    /// frame. ResearchUIInputRefreshSystem at order 1000 clears the
    /// flags after every consumer has had a chance to read them.
    /// </summary>
    public class ObservationCollectSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 50, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadShared<ResearchUIInputState>()
                    .ReadWriteShared<HypothesisViewModelState>()
                    .ReadWriteShared<ResearchMinigameState>()
                    .ReadShared<ChamberInterfacerState>()
            );
        }

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out ResearchUIInputState inputState,
                out ResearchMinigameState researchState,
                out ChamberInterfacerState interfacerState
            );

            if (!inputState.ChipPickerSelectedThisFrame && !inputState.RemoveObservationClickedThisFrame) {
                return;
            }

            ResearchSlot primarySlot = interfacerState.PrimarySlot;
            MaterialAsset slotted = primarySlot != null ? primarySlot.CurrentMaterial : null;
            if (slotted == null) {
                return;
            }
            StringHash32 slottedId = slotted.AssetId;

            // Get the secondary material; null check is done later because it
            // is only used for doping chamber
            bool isDopingChamber = interfacerState.ActiveChamber == ActiveChamberKind.Doping;
            ResearchSlot secondarySlot = interfacerState.SecondarySlot;
            MaterialAsset secondarySlotted = secondarySlot != null ? secondarySlot.CurrentMaterial : null;
            StringHash32 secondarySlottedId = secondarySlotted == null ? null : secondarySlotted.AssetId;

            bool viewModelDirty = false;

            // Add path: chip picker selection. Battery scope — context is
            // always null. Combiner will pass a meaningful context once it
            // lands.
            if (inputState.ChipPickerSelectedThisFrame) {
                if (isDopingChamber)
                {
                    if (secondarySlotted == null) {
                        return;
                    }

                    if (MaterialObservationChamberLookup.GetChamberType(inputState.ChipPickerSelectionLabel) != ObservationType.Dopant) {
                        return;
                    }

                    if (ResearchInventoryUtility.AddObservation(researchState, secondarySlottedId, inputState.ChipPickerSelectionLabel, slottedId)) {
                        viewModelDirty = true;
                        using (var table = TempVarTable.Alloc())
                        {
                            table.Set("observationId", inputState.ChipPickerSelectionLabel.ToString().ToLower());
                            ScriptUtility.Trigger(ResearchScriptTriggers.OnObservationAdded, table);
                        }
                    }
                }
                else if (ResearchInventoryUtility.AddObservation(researchState, slottedId, inputState.ChipPickerSelectionLabel, StringHash32.Null)) {
                    viewModelDirty = true;
                    using (var table = TempVarTable.Alloc())
                    {
                        table.Set("observationId", inputState.ChipPickerSelectionLabel.ToString().ToLower());
                        ScriptUtility.Trigger(ResearchScriptTriggers.OnObservationAdded, table);
                    }
                }
            }

            // Remove path: slot index → (label, context) via the
            // viewmodel's slot buffer, which holds whatever the player
            // picked — including observations that support no part of the
            // selected hypothesis. Locked slots are filtered client-side
            // by the click sites before the request fires; the guard here
            // is the slot-index range check + the locked-mask test.
            HypothesisViewModelState viewModelState = Find.State<HypothesisViewModelState>();

            if (inputState.RemoveObservationClickedThisFrame) {
                int idx = inputState.RemoveObservationSlotIndex;
                if (viewModelState != null && idx >= 0 && idx < viewModelState.SlotCount) {
                    bool locked = (viewModelState.SlotLockedMask & (1u << idx)) != 0;
                    if (!locked) {
                        MaterialPropertyLabel label = viewModelState.SlotLabels[idx];
                        StringHash32 context = viewModelState.SlotContexts[idx];
                        if (isDopingChamber)
                        {
                            if (ResearchInventoryUtility.RemoveObservation(researchState, secondarySlottedId, label, context)) {
                                viewModelDirty = true;
                                using (var table = TempVarTable.Alloc()) {
                                    table.Set("observationId", label.ToString().ToLower());
                                    ScriptUtility.Trigger(ResearchScriptTriggers.OnObservationRemoved, table);
                                }
                            }
                        }
                        else if (ResearchInventoryUtility.RemoveObservation(researchState, slottedId, label, context)) {
                            viewModelDirty = true;
                            using (var table = TempVarTable.Alloc()) {
                                table.Set("observationId", label.ToString().ToLower());
                                ScriptUtility.Trigger(ResearchScriptTriggers.OnObservationRemoved, table);
                            }
                        }
                    }
                }
            }

            if (viewModelDirty) {
                HypothesisViewModelUtility.RequestRebuild(viewModelState);
            }
        }
    }
}
