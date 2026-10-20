using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using SpaceFab.Fabrication.Sequence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    /// <summary>
    /// Shared helpers for microgame IMicrogame implementations. Centralizes cross-cutting work
    /// that every station microgame does on exit (precision commit, later: SFX stubs, analytics).
    /// </summary>
    public static class MicrogameUtility
    {
        // Writes the microgame's computed precision to the wafer at the current sequence step.
        // Called from each microgame's OnExitBegin(completedNormally: true). No-op path is
        // intentionally NOT handled here — callers early-out on !completedNormally before calling.
        public static void CommitStepPrecision(float precision)
        {
            Find.State(out WaferState waferState, out SequenceState sequenceState);
            WaferStateUtility.SetStepPrecision(waferState, sequenceState.CurrentStepIndex, precision);
        }

        public static MicrogameStationConfig GetStationConfig()
        {
            Find.State(out ContractState contractState);
            if (contractState.ContractAssets != null && contractState.ContractAssets.StationConfig != null)
            {
                return contractState.ContractAssets.StationConfig;
            }
            if (DebugFlags.LaunchedFromThisScene)
            {
                return Find.State<FabricationTransitionState>().DEBUG_StationConfig;
            }
            Log.Error("[MicrogameUtility] No station config available for the active contract");
            return null;
        }
    }
}
