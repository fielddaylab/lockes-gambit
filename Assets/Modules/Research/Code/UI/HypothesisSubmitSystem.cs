using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab;
using SpaceFab.Design;
using SpaceFab.Materials;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Research {
    /// <summary>
    /// Reads the submit-click frame-flag and routes it through
    /// ResearchInventoryUtility.TryConfirmHypothesis for the selected
    /// hypothesis's (Label, Context) against the slotted material. The
    /// existing utility handles the evaluator + observation consumption +
    /// sandbox bit set. Multiple definitions per label are fine: the
    /// pre-validation prunes against the union of every registered
    /// definition's leaves, and the evaluator OR-combines definitions
    /// when picking which one to consume.
    ///
    /// Runs on LateUpdate at order 60 — after Unity's EventSystem has
    /// dispatched the submit click (CursorHint.onClick fires there) and
    /// after ObservationCollectSystem (order 50) so any chip the player
    /// added the same frame is already in the inventory, and before
    /// HypothesisViewModelSystem at order 100 so the confirmation bit
    /// shows up in the same-frame rebuild. ResearchUIInputRefreshSystem
    /// at order 1000 clears the flag last.
    /// </summary>
    public class HypothesisSubmitSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 60, UpdateMasks.ResearchMask),
                new SysPermissions()
                    .ReadShared<ResearchUIInputState>()
                    .ReadWriteShared<HypothesisViewModelState>()
                    .ReadWriteShared<ResearchMinigameState>()
                    .ReadShared<ChamberInterfacerState>()
                    .ReadWriteShared<PlayerProgressState>()
            );
        }

        private static void ProcessWork(float deltaTime) {
            Find.State(
                out ResearchUIInputState inputState,
                out ChamberInterfacerState interfacerState,
                out HypothesisViewModelState viewModelState
            );

            if (!inputState.VerifyHypothesisClickedThisFrame && !viewModelState.HypothesisVerified) {
                return;
            }

            if (!viewModelState.HypothesisSelected) {
                return;
            }

            ResearchSlot slot = interfacerState.ActiveChamber == ActiveChamberKind.Doping ?
                interfacerState.SecondarySlot : interfacerState.PrimarySlot;
            MaterialAsset slotted = slot != null ? slot.CurrentMaterial : null;
            if (slotted == null) {
                return;
            }

            Find.State(
                out ResearchMinigameState researchState,
                out PlayerProgressState progressState,
                out ContractState contractState,
                out WikiViewState wikiState
            );
            ResearchSamplePanel panel = Find.FirstComponent<ResearchSamplePanel>();

            if (inputState.VerifyHypothesisClickedThisFrame) {
                ResearchUIAssets uiAssets = Find.GlobalAsset<ResearchUIAssets>();
                viewModelState.VerifyRoutine.Replace(viewModelState, VerificationRoutine(panel, viewModelState, researchState, progressState, contractState, wikiState, slotted, uiAssets)).TryManuallyUpdate(0);
            }
        }

        private static IEnumerator VerificationRoutine(ResearchSamplePanel panel, HypothesisViewModelState viewModelState, ResearchMinigameState researchState, PlayerProgressState progressState, ContractState contractState, WikiViewState wikiState, MaterialAsset material, ResearchUIAssets config)
        {
            Game.Input.PauseAll();

            viewModelState.HypothesisVerified = true;
            panel.Lines.color = config.ProcessingColor;
            panel.ResultText.text = "Verifying...";
            panel.ResultText.color = config.ProcessingColor;

            panel.LoadingBar.SetActive(true);
            panel.FilledBar.fillAmount = 0;

            yield return 0.25f;

            yield return panel.FilledBar.FillTo(1f, 2f).Ease(Curve.QuadInOut);

            yield return 0.5f;

            string failureReason = null;
            bool anyPruned = PruneInvalidObservations(researchState, material, viewModelState);
            if (anyPruned) {
                HypothesisViewModelUtility.RequestRebuild(viewModelState);
                failureReason = "invalid_observation";
            }

            yield return 0.5f;

            bool validHypothesis = ValidateProperty(material, viewModelState);
            if (failureReason == null && !validHypothesis) {
                failureReason = "hypothesis_mismatch";
            }

            bool hasRequiredObs = EvaluateObservations(viewModelState);
            if (failureReason == null && !hasRequiredObs) {
                failureReason = "observation_mismatch";
            }

            bool success = !anyPruned && hasRequiredObs && validHypothesis;
            if (success) {
                if (ResearchInventoryUtility.TryConfirmHypothesis(researchState, progressState, contractState, material.AssetId, viewModelState.HypothesisLabel, viewModelState.HypothesisContext)) {
                    HypothesisViewModelUtility.RequestRebuild(viewModelState);
                    WikiUtility.OpenTo(Find.State<WikiViewState>(), "Materials_Tab", material.AssetId);
                }
                else {
                    success = false;
                }
            }

            yield return 0.25f;

            panel.Lines.color = success ? config.SuccessColor : config.FailureColor;
            panel.ResultIcon.gameObject.SetActive(true);
            panel.ResultIcon.sprite = success ? config.SuccessIcon : config.FailureIcon;
            panel.ResultText.text = success ? "Verified" : "Material not verified";
            panel.ResultText.color = success ? config.SuccessColor : config.FailureColor;
            panel.LoadingBar.SetActive(false);

            yield return 2f;

            panel.Lines.color = config.DefaultColor;
            panel.ResultIcon.gameObject.SetActive(false);
            panel.ResultText.text = "";

            using (var table = TempVarTable.Alloc()) {
                var resultStr = success ? "success" : "failure";
                table.Set("result", resultStr);
                if (!success) {
                    // "invalid_observation": observation does not match the material
                    // "observation_mismatch": observation does not match the hypothesis and/or does not have all required observations for the hypothesis
                    // "hypothesis_mismatch": hypothesis does not match the material
                    table.Set("reason", failureReason);
                }
                ScriptUtility.Trigger(ResearchScriptTriggers.OnHypothesisSubmitted, table);
            }

            viewModelState.HypothesisVerified = false;
            Game.Input.ResumeAll();
        }

        // Checks if the hypothesis property matches the material. If the property does not
        // match the material, it is removed from the hypothesis slot. Otherwise, it remains.
        private static bool ValidateProperty(MaterialAsset material, HypothesisViewModelState viewModelState)
        {
            MaterialPropertyLabel[] validProperties = material.Properties;
            for (int i = 0; i < validProperties.Length; i++) {
                if (validProperties[i] == viewModelState.HypothesisLabel) {
                    if (validProperties[i] != MaterialPropertyLabel.PDopantFor && validProperties[i] != MaterialPropertyLabel.NDopantFor) {
                        return true;
                    }
                    
                    for (int c = 0; c < material.Contexts.Length; c++) {
                        if (viewModelState.SlotContexts[0] == material.Contexts[c].AssetId) {
                            return true;
                        }
                    }
                    return false;
                }
            }

            viewModelState.HypothesisSelected = false;
            HypothesisViewModelUtility.RequestRebuild(viewModelState);
            return false;
        }

        // Checks if observations match the material. Any observations that
        // do not match the material are removed from the slot.
        private static bool PruneInvalidObservations(ResearchMinigameState researchState, MaterialAsset material, HypothesisViewModelState viewModelState)
        {
            int slotCount = viewModelState.SlotCount;
            MaterialPropertyLabel[] validProperties = material.Properties;
            bool anyRemoved = false;

            for (int i = 0; i < slotCount; i++) {
                bool locked = (viewModelState.SlotLockedMask & (1u << i)) != 0;
                if (locked) continue;

                MaterialPropertyLabel slotLabel = viewModelState.SlotLabels[i];
                StringHash32 slotContext = viewModelState.SlotContexts[i];
                bool isDopant = MaterialObservationChamberLookup.GetChamberType(slotLabel) == ObservationType.Dopant;

                StringHash32[] contextIds = isDopant ? new StringHash32[material.Contexts.Length] : new StringHash32[] { StringHash32.Null };

                if (isDopant) {
                    for (int c = 0; c < material.Contexts.Length; c++) {
                        contextIds[c] = material.Contexts[c].AssetId;
                    }
                }

                bool valid = MaterialPropertyDefinitionUtility.IsObservationTrueForProperties(validProperties, slotLabel, slotContext, contextIds);
                if (!valid) {
                    if (ResearchInventoryUtility.RemoveObservation(
                        researchState, material.AssetId, slotLabel, slotContext)) {
                        anyRemoved = true;
                    }
                }
            }
            return anyRemoved;
        }

        // Checks if the property has all necessary observations. If not
        // all necessary observations are present, verification fails.
        // Any observations that are not required by the property become greyed out.
        private static bool EvaluateObservations(HypothesisViewModelState viewModelState)
        {
            List<MaterialObservationEntry> leaves = DecomposeAllDefinitions(viewModelState.HypothesisLabel);
            int leafCount = leaves.Count;
            int slotCount = viewModelState.SlotCount;

            bool hasMissingObs = false;

            List<MaterialPropertyLabel> matched = new();
            for (int i = 0; i < slotCount; i++) {
                MaterialPropertyLabel label = viewModelState.SlotLabels[i];

                bool onLeaf = LeafMatches(leaves, leafCount, label, StringHash32.Null);
                if (onLeaf) {
                    matched.Add(label);
                } else {
                    foreach (var panel in Find.Components<ResearchSamplePanel>()) {
                        if (panel == null || !panel.PickerOpen) continue;
                        // TODO: change sprite for greyed out chips
                    }
                }
            }

            for (int i = 0; i < leafCount; i++) {
                var leaf = leaves[i];
                if (!matched.Contains(leaf.Label)) {
                    hasMissingObs = true;
                    break;
                }
            }

            return !hasMissingObs;
        }

        // True if some leaf matches (label, context).
        private static bool LeafMatches(List<MaterialObservationEntry> leaves, int leafCount, MaterialPropertyLabel label, StringHash32 context) {
            for (int i = 0; i < leafCount; i++) {
                if (leaves[i].Label == label && leaves[i].Context == context) {
                    return true;
                }
            }
            return false;
        }

        // Scratch for the union decomposition; submits are rare and
        // single-threaded, so shared buffers suffice.
        [NotStateful] private static readonly List<MaterialObservationEntry> s_UnionScratch = new List<MaterialObservationEntry>(8);
        [NotStateful] private static readonly List<MaterialObservationEntry> s_DefScratch = new List<MaterialObservationEntry>(8);
        [NotStateful] private static readonly StringHash32[] s_NullContext = new StringHash32[] { StringHash32.Null };

        // Decomposes every registered definition for the label and
        // returns the deduped (label, context) union of their leaves.
        private static List<MaterialObservationEntry> DecomposeAllDefinitions(MaterialPropertyLabel label) {
            s_UnionScratch.Clear();
            MaterialPropertyDefinitionAsset registry = Find.GlobalAsset<MaterialPropertyDefinitionAsset>();
            if (registry == null) {
                return s_UnionScratch;
            }
            MaterialPropertyDefinition[] defs = registry.GetDefinitions(label);
            for (int d = 0; d < defs.Length; d++) {
                s_DefScratch.Clear();
                MaterialPropertyDefinitionUtility.DecomposeToObservations(defs[d], s_NullContext, s_DefScratch);
                for (int i = 0; i < s_DefScratch.Count; i++) {
                    if (!LeafMatches(s_UnionScratch, s_UnionScratch.Count, s_DefScratch[i].Label, s_DefScratch[i].Context)) {
                        s_UnionScratch.Add(s_DefScratch[i]);
                    }
                }
            }
            return s_UnionScratch;
        }
    }
}