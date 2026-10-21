using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication.Sequence;
using SpaceFab.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication
{
    /// <summary>
    /// Stores minigame-specific data for import/export
    /// </summary>
    public class FabricationMinigameState : MinigameStateBase, IRegistrationCallbacks, IMinigameState
    {
        #region Saved State

        [NonSerialized] public int TotalCycles;
        [NonSerialized] public float Precision;
        [NonSerialized] public int TotalCost;

        #endregion // Saved State

        #region Interfaces

        // IRegistrationCallbacks

        public void OnDeregister()
        {
            foreach(var asset in Find.NamedAssets<ContractAssetSet>()) {
                if (asset.FabricationLevel) {
                    asset.FabricationLevel.ClearCachedReferences();
                }
            }
        }

        public void OnRegister()
        {
            DefaultUpdateMask = UpdateMasks.SetupMask | UpdateMasks.WikiMask;
        }

        // IMinigameState

        public override void ImportState(MinigameSaveStates saveStates)
        {
            FabricationStateUtility.ImportState(saveStates.Fabrication, this);
        }

        public override void ExportState(ref MinigameSaveStates saveStates)
        {
            FabricationStateUtility.ExportState(ref saveStates.Fabrication, this);
        }

        #endregion // Interfaces
    }

    public static class FabricationStateUtility
    {
        public static void ImportState(FabricationSaveState saveState, FabricationMinigameState fabState)
        {
            fabState.TotalCycles = saveState.FinalizedTotalCycles;
            fabState.TotalCost = saveState.FinalizedCost;

            fabState.FoundValidSolution = saveState.FoundValidSolution;
        }

        public static void ExportState(ref FabricationSaveState saveState, FabricationMinigameState fabState)
        {
            // TODO: check if run completed
            saveState.FinalizedTotalCycles = fabState.TotalCycles;
            saveState.FinalizedCost = fabState.TotalCost;

            saveState.FoundValidSolution = fabState.FoundValidSolution;

            if (Game.SharedState.Has<ProgressMeterState>())
            {
                Find.State<ProgressMeterState>().NeedsRefresh = true;
            }
        }
    }
}