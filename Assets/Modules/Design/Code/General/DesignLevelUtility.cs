using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Scenes;
using SpaceFab.Save;
using SpaceFab.UI;

namespace SpaceFab.Design
{
    /// <summary>
    /// Resolves the active Design level for the contract the player is currently working on.
    /// Centralizes the (ContractAssetsWrapper.DesignLevels[ActiveLevelIndex]) lookup so the test
    /// suite, allowed tools, and grid config all read from one place instead of each call site
    /// re-deriving the index.
    /// </summary>
    public static class DesignLevelUtility
    {
        // Returns the LevelData for the player's currently-active Design level, or null if the
        // active contract / its wrapper isn't resolvable or the index is out of range.
        public static LevelData GetActiveLevelData(ContractState contractState, DesignMinigameState designState)
        {
            if (contractState.ContractAssets == null || contractState.ContractAssets.DesignLevels == null)
            {
                if (DebugFlags.LaunchedFromThisScene) {
                    return designState.DebugLevelData;
                }
                return null;
            }

            int idx = designState.ActiveLevelIndex;
            if (idx < 0 || idx >= contractState.ContractAssets.DesignLevels.Length)
            {
                return null;
            }

            return contractState.ContractAssets.DesignLevels[idx];
        }

        // Marks the active level solved in the save state and mirrors the contract-wide aggregate
        // (all levels solved) onto the runtime flag overarching reads. Save-buffer persistence is
        // the caller's concern (export/Save happens in the exit / continue flow).
        public static void MarkActiveLevelSolved(DesignSaveState saveState, DesignMinigameState designState)
        {
            int idx = designState.ActiveLevelIndex;
            if (saveState.LevelCount <= 0 || idx < 0 || idx >= saveState.LevelCount) { return; }

            saveState.FoundValidSolutionForLevel[idx] = true;
            designState.FoundValidSolution = DesignSaveUtility.AllLevelsSolved(saveState);
        }

        // Clears the active level's solved flag (e.g. on a grid edit) and refreshes the aggregate
        // runtime flag. Once any level becomes unsolved the contract is no longer complete.
        public static void ClearActiveLevelSolved(DesignSaveState saveState, DesignMinigameState designState)
        {
            int idx = designState.ActiveLevelIndex;
            if (saveState.LevelCount <= 0 || idx < 0 || idx >= saveState.LevelCount) { return; }

            saveState.FoundValidSolutionForLevel[idx] = false;
            designState.FoundValidSolution = DesignSaveUtility.AllLevelsSolved(saveState);
        }

        // Advances out of the active level: when more levels remain, reloads the Design scene
        // (re-import lands on the next first-unsolved level); on the last level, confirms the normal
        // exit back to overarching. Shared by the results-panel "Continue" flow and the debug skip
        // button so both route through one implementation. Callers are expected to have already
        // marked the active level solved when that's the intent.
        public static void AdvanceFromActiveLevel(DesignSaveState saveState, DesignMinigameState designState, MinigameRequestExitState requestExitState, MinigameStateInterfacer interfacer)
        {
            int activeIdx = designState.ActiveLevelIndex;
            int levelCount = saveState.LevelCount;

            // Last level (or a degenerate single/zero-level contract): return to overarching, where
            // the now-true aggregate FoundValidSolution lets the contract complete.
            if (activeIdx >= levelCount - 1)
            {
                requestExitState.ExitRequestState = RequestState.Confirmed;
                return;
            }

            // More levels remain: reload the Design scene for the next level. Resolve the scene from
            // the global lookup by the active minigame's id, point the exit pipeline at it, and kick
            // the pipeline. The Exiting phase exports + saves before the reload.
            MinigameUtility.Exit(Game.Scenes.MainScene());
        }

        public static void PopulateDescriptionPanel(SimulateUIState uiState, LevelData levelData) {
            Assert.NotNull(uiState.TitleText);
            Assert.NotNull(uiState.DescriptionText);   
            uiState.TitleText.text = levelData.GetTitle();
            uiState.DescriptionText.text = levelData.GetDescription();
        }

        public static void ApplyWikiPageUnlocks(PlayerProgressState playerProgress, LevelData levelData) {
            ToolTypeFlags tools = levelData.GetAllowedTools();

            bool hasN = false;
            bool hasP = false;
            bool hasGate = false;
            bool hasVia = false;

            if ((tools & ToolTypeFlags.METAL) != 0) {
                WikiUtility.UnlockPage(playerProgress, "Metal");
            }
            if ((tools & ToolTypeFlags.NNODE) != 0) {
                WikiUtility.UnlockPage(playerProgress, "N-Type");
                hasN = true;
            }
            if ((tools & ToolTypeFlags.PNODE) != 0) {
                WikiUtility.UnlockPage(playerProgress, "P-Type");
                hasP = true;
            }
            if ((tools & ToolTypeFlags.VIA) != 0) {
                WikiUtility.UnlockPage(playerProgress, "Via");
                hasVia = true;
            }
            if ((tools & ToolTypeFlags.GATE) != 0) {
                WikiUtility.UnlockPage(playerProgress, "Gate");
                hasGate = true;
            }

            foreach(var cellConfig in levelData.GetGridConfig().Cells) {
                switch (cellConfig.CellType) {
                    case CellType.PTransistor: {
                        if (!hasP) {
                            WikiUtility.UnlockPage(playerProgress, "P-Type");
                            hasP = true;
                        }
                        break;
                    }
                    case CellType.NTransistor: {
                        if (!hasN) {
                            WikiUtility.UnlockPage(playerProgress, "N-Type");
                            hasN = true;
                        }
                        break;
                    }
                }

                switch (cellConfig.TransferType) {
                    case TransferType.GateAbove:
                    case TransferType.GateBelow: {
                        if (!hasGate) {
                            WikiUtility.UnlockPage(playerProgress, "Gate");
                            hasGate = true;
                        }
                        break;
                    }
                    case TransferType.Via: {
                        if (!hasVia) {
                            WikiUtility.UnlockPage(playerProgress, "Via");
                            hasVia = true;
                        }
                        break;
                    }
                }

                const InputOutputNodeTypeFlags constantsMask = InputOutputNodeTypeFlags.VPLUS | InputOutputNodeTypeFlags.VMINUS;

                if ((cellConfig.SubtypeLabel & constantsMask) != 0) {
                    WikiUtility.UnlockPage(playerProgress, "Constants");
                }
            }

            if (hasN & hasP) {
                WikiUtility.UnlockPage(playerProgress, "Diode");
            }

            if (hasGate) {
                WikiUtility.UnlockPage(playerProgress, "PNP");
                WikiUtility.UnlockPage(playerProgress, "NPN");
                WikiUtility.UnlockPage(playerProgress, "Transistor");
            }
        }
    }
}
