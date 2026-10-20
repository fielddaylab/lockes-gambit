using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Music;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Suspends SetupMask when done.
    /// </summary>
    public class SimTableLoadSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.PreUpdate, 5, UpdateMasks.SetupMask),
                new SysPermissions()
                    .ReadWriteShared<DesignTransitionState>()
                    .ReadWriteShared<SimulateUIState>()
                    .ReadWriteShared<SimulateRunState>()
                    .ReadShared<DesignMinigameState>()
                    .ReadShared<ContractState>()
            );
        }

        // Advances the setup phase state machine one step per tick until setup is complete.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out DesignTransitionState transitionState,
                out SimulateUIState simUIState,
                out SimulateRunState simRunState,
                out PlayerProgressState progressState
                );
            Find.State(
                out ContractState contractState,
                out DesignResultPanel resultState
                );
            DesignMinigameState designState = Find.State<DesignMinigameState>();

            LevelData levelData = DesignLevelUtility.GetActiveLevelData(contractState, designState);
            TestSuiteData suiteData = levelData.GetTestSuite();
            var suiteDB = Find.GlobalAsset<SuiteVisualConfig>();

            switch (transitionState.Phase)
            {
                case DesignTransitionPhase.BuildSimTable:
                    Debug.Log("[SimTableLoadSystem] Building Sim Table...");
                    // Apply per-level toolbar availability before building the sim table so any
                    // downstream UI code sees the correct Available flags. Disallowed tools end up
                    // hidden + disabled — see ToolbarAvailabilityUtility.ApplyAllowedTools.
                    ToolbarAvailabilityUtility.ApplyAllowedTools(levelData.GetAllowedTools());

                    // build Sim table
                    SimulateUIUtility.BuildTable(simUIState, suiteData, simRunState, designState, suiteDB);
                    ResultStateUtility.BuildResultsTable(resultState, suiteData, suiteDB);
                    DesignLevelUtility.ApplyWikiPageUnlocks(progressState, levelData);
                    DesignLevelUtility.PopulateDescriptionPanel(simUIState, levelData);
                    // If the player has already passed this contract's suite, present all rows as
                    // Correct on entry rather than forcing them to re-run. FoundValidSolution is
                    // hydrated by DesignStateUtility.ImportState during the prior Loading phase,
                    // so it's authoritative here.
                    if (designState.FoundValidSolution)
                    {
                        SimulateUIUtility.MarkAllRowsCorrect(simUIState, suiteData);
                    }

                    transitionState.Phase = DesignTransitionPhase.SetupComplete;
                    break;
                case DesignTransitionPhase.SetupComplete:
                    Debug.Log("[SimTableLoadSystem] Load Complete!");
                    // Setup is finished (grid + input/output overlays spawned, ElementTag ids
                    // registered). Fire the onboarding hook before suspending so tutorial scripts
                    // can safely highlight design elements by id now that the lookup is populated.
                    ScriptUtility.Trigger(DesignScriptTriggers.OnDesignSetupComplete);
                    // Setup is finished; stop running this system until the next minigame load
                    GameLoop.SuspendUpdates(UpdateMasks.SetupMask);
                    break;
                default:
                    break;
            }
        }
    }
}
