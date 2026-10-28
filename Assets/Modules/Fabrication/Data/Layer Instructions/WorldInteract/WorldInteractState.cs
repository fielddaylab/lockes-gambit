using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Fabrication.Robot;
using SpaceFab.Fabrication.StationControl;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Movement {
    /// <summary>
    /// Holds data for world (non-microgame) interactions and inputs. Acts as the outer kill switch
    /// for world-interact input; the station-control machine is the inner gate.
    /// </summary>
    public class WorldInteractState : SharedStateComponent, IRegistrationCallbacks {
        [NonSerialized] public bool WorldInteractEnabled;

        public void OnDeregister() {
        }

        public void OnRegister() {
            WorldInteractEnabled = true;
        }
    }

    public static class WorldInteractUtility {
        // True when Activate input should be forwarded to StationControlUtility.RequestActivate.
        // Requires the outer kill switch (WorldInteractEnabled) AND the station-control gate.
        public static bool CanActivate(WorldInteractState interactState, StationControlState stationState) {
            return interactState.WorldInteractEnabled && StationControlUtility.AllowsActivate(stationState);
        }

        // True when Cancel input should be forwarded to StationControlUtility.RequestCancel.
        // Requires the outer kill switch AND the station-control gate (only honored in InMicrogame).
        public static bool CanCancel(WorldInteractState interactState, StationControlState stationState) {
            return interactState.WorldInteractEnabled && StationControlUtility.AllowsCancel(stationState);
        }
    }
}
