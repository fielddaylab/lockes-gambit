using BeauUtil;
using FieldDay;
using FieldDay.Systems;
using SpaceFab.Overarching;
using SpaceFab.Save;
using System.Diagnostics.Contracts;
using UnityEngine;

namespace SpaceFab {
    /// <summary>
    /// Each LateUpdate, mirrors PlayerProgressState.ElapsedCycles into the marker index
    /// and pushes ProgressMeterState through to its bound view when the dirty flag is set.
    /// On first run with no bound view, performs a one-time scene scan to recover the
    /// reference (covers cases where the view enabled before this state registered).
    /// </summary>
    public class ProgressMeterUpdateSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhaseMask.LateUpdate, 0),
                new SysPermissions()
                    .ReadWriteShared<ProgressMeterState>()
                    .ReadShared<PlayerProgressState>()
                    .ReadShared<MinigameSaveStates>()
            );
        }

        // Per-tick: ensure the view is bound, sync ElapsedCycles, drain the dirty flag.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out ProgressMeterState meterState,
                out PlayerProgressState progressState,
                out MinigameSaveStates saveStates,
                out ContractState contractState
                );

            // Drain the dirty flag by pushing state into the view.
            if (meterState.NeedsRefresh) {
                // Update pending cycles
                int numPendingCycles = ProgressMeterUtility.CalculatePendingCycleCells(meterState.ActiveMeter, saveStates);
                int fabCycles = Mathf.Max(0, saveStates.Fabrication.FinalizedTotalCycles);
                int filledEnd = progressState.ElapsedCycles + fabCycles;

                int pendingEnd = progressState.ElapsedCycles + Mathf.Max(fabCycles, numPendingCycles);

                for (int i = progressState.ElapsedCycles; i < pendingEnd; i++)
                {
                    ProgressMeterUtility.SetCycleCellState(meterState, i, CycleCellState.PENDING);
                }
                ProgressMeterUtility.ClearCycleStateFrom(meterState, pendingEnd);

                // Update pending funds

                if (contractState.ContractDefinition)
                {
                    int contractPayout = contractState.ContractDefinition.Payout();
                    ProgressMeterUtility.CalculatePendingFundsCells(meterState.ActiveMeter, saveStates, contractPayout, out int pendingReceivedCount, out int pendingSpentCount);

                    int spentThreshold = progressState.Funds + contractPayout - pendingSpentCount;
                    int pendingReceivedThreshold = spentThreshold - Mathf.Max(0, contractPayout - pendingSpentCount);

                    // TODO: check for out of bounds (spending more than have in funds)

                    for (int i = pendingReceivedThreshold; i < spentThreshold; i++)
                    {
                        ProgressMeterUtility.SetFundsCellState(meterState, i, FundsCellState.PENDING_RECEIVED);
                    }
                    for (int i = spentThreshold; i < progressState.Funds + contractPayout; i++)
                    {
                        ProgressMeterUtility.SetFundsCellState(meterState, i, FundsCellState.PENDING_SPENT);
                    }
                    ProgressMeterUtility.ClearFundStateFrom(meterState, progressState.Funds + contractPayout);
                }

                // apply visual refresh
                // ProgressMeterUtility.EnsureCellsBound(meterState.ActiveMeter);
                ProgressMeterUtility.RefreshVisuals(meterState.ActiveMeter, meterState);
                meterState.NeedsRefresh = false;
            }
        }
    }
}
