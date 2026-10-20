using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Supply
{
    public class SupplyMinigameState : MinigameStateBase, IRegistrationCallbacks, IMinigameState
    {
        #region Saved State

        [NonSerialized] public int Risk;
        [NonSerialized] public int TotalCycles;
        [NonSerialized] public int Cost;

        // Mirror of the drawn routes, and the hand-off between the save chunk and the live
        // SupplyRouteCollection in both directions. ImportState fills it from save (the map does
        // not exist yet at that point); SupplyLoader applies it once the map is built. On exit
        // SupplyRouteSaveUtility.Capture refills it and ExportState writes it back out.
        [NonSerialized] public int SavedRouteCount;
        [NonSerialized] public SupplyRouteSaveData[] SavedRoutes = new SupplyRouteSaveData[SupplyRouteData.MaxShips];

        #endregion // Saved State

        #region Runtime State

        [NonSerialized] public SupplyChainMapData CurrSupplyChainMap;

        #endregion // Runtime State

        #region Interfaces

        // IRegistrationCallbacks

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            DefaultUpdateMask = UpdateMasks.SetupMask | UpdateMasks.WikiMask | UpdateMasks.SupplyMask;
        }

        // IMinigameState

        public override void ImportState(MinigameSaveStates saveStates)
        {
            SupplyStateUtility.ImportState(saveStates.Supply, this);
        }

        public override void ExportState(ref MinigameSaveStates saveStates)
        {
            // Snapshot the live routes into the mirror before writing it out. Export runs from
            // MinigameUtility.ExecuteExit with the scene still up, so the collection is available
            // here - unlike at import time, when the map has not been built yet.
            Find.State(out SupplyRouteCollection routes, out SupplyRouteDrawingState draw, out SupplyShipIndex ships);
            SupplyRouteSaveUtility.Capture(routes, draw, ships, this);

            SupplyStateUtility.ExportState(ref saveStates.Supply, this);
        }

        #endregion // Interfaces
    }

    public static class SupplyStateUtility
    {
        public static void ImportState(SupplySaveState saveState, SupplyMinigameState supplyState)
        {
            supplyState.Risk = saveState.FinalizedRisk;
            supplyState.TotalCycles = saveState.FinalizedTotalCycles;
            supplyState.Cost = saveState.FinalizedCost;

            supplyState.FoundValidSolution = saveState.FoundValidSolution;

            supplyState.SavedRouteCount = saveState.RouteCount;
            Array.Copy(saveState.Routes, supplyState.SavedRoutes, SupplyRouteData.MaxShips);
        }

        public static void ExportState(ref SupplySaveState saveState, SupplyMinigameState supplyState)
        {
            saveState.FinalizedRisk = supplyState.Risk;
            saveState.FinalizedTotalCycles = supplyState.TotalCycles;
            saveState.FinalizedCost = supplyState.Cost;

            saveState.FoundValidSolution = supplyState.FoundValidSolution;

            saveState.RouteCount = supplyState.SavedRouteCount;
            Array.Copy(supplyState.SavedRoutes, saveState.Routes, SupplyRouteData.MaxShips);
        }

        // Drops a previously confirmed result. Editing a route after confirming leaves the stored
        // cost/cycles/risk describing routes that no longer exist, so the player has to re-confirm
        // rather than submit the chapter on stale numbers.
        public static void InvalidateFinalizedSolution(SupplyMinigameState supplyState)
        {
            supplyState.Risk = -1;
            supplyState.TotalCycles = -1;
            supplyState.Cost = -1;

            supplyState.FoundValidSolution = false;
        }
    }
}