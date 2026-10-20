using BeauUtil;
using FieldDay;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab.Research
{
    /// <summary>
    /// Operations on ResearchMinigameState's per-material observation lists
    /// and the bridge into hypothesis confirmation. Observations are tentative
    /// in-session evidence; confirming a hypothesis consumes the observations
    /// that satisfied a definition's dependency list and flips the matching
    /// bit in SandboxProperties.
    /// </summary>
    public static class ResearchInventoryUtility
    {
        // Records an observation against a material. Returns false if the
        // buffer is full or if the same (label, context) is already present.
        public static bool AddObservation(ResearchMinigameState researchState, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            researchState.Observations.TryGetValue(materialId, out var list);
            bool added = MaterialObservationListUtility.TryAdd(ref list, label, contextMaterialId);
            if (added)
            {
                researchState.Observations[materialId] = list;
            }
            return added;
        }

        // Removes the first matching (label, context) observation from a
        // material's list. Returns true if an entry was removed.
        public static bool RemoveObservation(ResearchMinigameState researchState, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            if (!researchState.Observations.TryGetValue(materialId, out var list))
            {
                return false;
            }
            bool removed = MaterialObservationListUtility.Remove(ref list, label, contextMaterialId);
            if (removed)
            {
                researchState.Observations[materialId] = list;
            }
            return removed;
        }

        // True if the player has the given observation for the material.
        public static bool HasObservation(ResearchMinigameState researchState, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            if (!researchState.Observations.TryGetValue(materialId, out var list))
            {
                return false;
            }
            return MaterialObservationListUtility.Has(list, label, contextMaterialId);
        }

        // Snapshot of a material's observation list. Returned by value; the
        // caller should not mutate it as a backdoor into state.
        public static MaterialObservationList GetObservations(ResearchMinigameState researchState, StringHash32 materialId)
        {
            researchState.Observations.TryGetValue(materialId, out var list);
            return list;
        }

        // Empties a material's observation list.
        public static void ClearObservations(ResearchMinigameState researchState, StringHash32 materialId)
        {
            if (researchState.Observations.TryGetValue(materialId, out var list))
            {
                MaterialObservationListUtility.Clear(ref list);
                researchState.Observations[materialId] = list;
            }
        }

        // Empties every material's observation list. Called on minigame entry
        // alongside the sandbox seed so an entry starts with a clean slate.
        public static void ClearAllObservations(ResearchMinigameState researchState)
        {
            researchState.Observations.Clear();
        }

        // Tries to confirm a hypothesis: runs the evaluator against the
        // material's current observations + the sandbox record. On success,
        // removes the observations the satisfying definition consumed (the
        // observation entries in its Dependencies list, evaluated against
        // the inherited context), flips the bit on SandboxProperties, and
        // returns true. Idempotent: confirming an already-confirmed property
        // returns true without consuming observations.
        //
        // When multiple definitions match the label and more than one is
        // satisfied, the first satisfied definition (in registry order) wins
        // and only its dependencies are consumed.
        public static bool TryConfirmHypothesis(ResearchMinigameState researchState, PlayerProgressState progressState, ContractState contractState, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterialId)
        {
            if (researchState == null) return false;

            // Filter out non-persistent labels - those are evidence, not confirmable.
            if (!MaterialPropertyLabelUtility.IsPersistent(label)) {
                return false;
            }

            // Idempotent: already confirmed means success without further work.
            if (ResearchStateUtility.HasConfirmed(researchState, materialId, label, contextMaterialId)) {
                return true;
            }

            // Bind the evaluator's observation lookup to this material's list.
            researchState.SandboxProperties.TryGetValue(materialId, out MaterialPropertyRecord record);
            MaterialPropertyDefinitionUtility.HasObservationDelegate hasObservation =
                (obsLabel, obsContext) => HasObservation(researchState, materialId, obsLabel, obsContext);

            MaterialPropertyDefinition satisfied = MaterialPropertyDefinitionUtility.FindSatisfiedDefinition(label, contextMaterialId, record, hasObservation);
            if (satisfied == null) {
                return false;
            }

            // Consume the observation entries from the satisfying definition.
            // Sub-property dependencies aren't observations, so they aren't
            // touched here; they were satisfied by record bits or by their
            // own recursive confirmation in some prior call.
            if (satisfied.Dependencies != null)
            {
                for (int i = 0; i < satisfied.Dependencies.Length; i++)
                {
                    MaterialPropertyLabel dep = satisfied.Dependencies[i];
                    if (!MaterialPropertyLabelUtility.IsPersistent(dep))
                    {
                        RemoveObservation(researchState, materialId, dep, contextMaterialId);
                    }
                }
            }

            ResearchPropertyConfirmBridge.HandleConfirmedProperty(researchState, progressState, contractState, materialId, label, contextMaterialId);

            if (MaterialPropertyLabelUtility.TryGetBaseProperty(label, out MaterialPropertyLabel baseLabel)) {
                ResearchPropertyConfirmBridge.HandleConfirmedProperty(researchState, progressState, contractState, materialId, baseLabel, contextMaterialId);
            }
            return true;
        }
    }
}
