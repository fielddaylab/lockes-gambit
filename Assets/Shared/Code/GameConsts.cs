using BeauUtil;
using FieldDay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab
{
    public static class GameConsts
    {
        public static int StartGameSceneIndex = 0; // index of the first scene -- usually Boot
        public static string OverarchingSceneName = "Overarching";
    }

    public static class GameEvents
    {
        // Title
        static public readonly StringHash32 TitleNewGameGroupClicked = "title:new-game-group-clicked";
        static public readonly StringHash32 TitleContinueGameGroupClicked = "title:continue-game-group-clicked";
        static public readonly StringHash32 TitleOptionsGroupClicked = "title:options-group-clicked";
        static public readonly StringHash32 TitleCreditsClicked = "title:credits-group-clicked";

        static public readonly StringHash32 TitleStartGameClicked = "title:start-game-clicked";
        static public readonly StringHash32 TitleBackFromInputClicked = "title:back-from-input-clicked";

        static public readonly StringHash32 TitleProfileStarting = "title:profile-starting";

        static public readonly StringHash32 TitleNewGameClicked = "title:new-game-clicked";
        static public readonly StringHash32 TitleContinueGameClicked = "title:continue-game-clicked";


        // Save
        public static readonly StringHash32 ProfileSaveBegin = "save:profile-save-begin";
        public static readonly StringHash32 ProfileSaveError = "save:profile-save-error";
        public static readonly StringHash32 ProfileSaveSuccess = "save:profile-save-success";
        public static readonly StringHash32 ProfileSaveAttemptCompleted = "save:profile-save-attempt-completed";

        // Shared
        public static readonly StringHash32 ClickPauseGame = "shared:click-pause-game";
        public static readonly StringHash32 OnGamePaused = "shared:on-game-paused";
        public static readonly StringHash32 ClickResumeGame = "shared:click-resume-game";
        public static readonly StringHash32 OnGameResumed = "shared:on-game-resumed";

        // Minigame Navigation
        public static readonly StringHash32 ShipMenuDisplayed = "shared:ship-menu-displayed";
        public static readonly StringHash32 SelectMinigame = "shared:select-minigame";
        public static readonly StringHash32 StartMinigame = "shared:start-minigame";
        public static readonly StringHash32 OnMinigameLoad = "shared:on-minigame-load";
        public static readonly StringHash32 OnMinigameExit = "shared:on-minigame-exit";

        // Fabrication: Station Control
        public static readonly StringHash32 FabStationArrived = "fab:station-arrived";
        public static readonly StringHash32 FabActivateStation = "fab:activate-station"; // Check conflicts with other consts
        public static readonly StringHash32 FabInstructionUpdated = "fab:instruction-updated";
        public static readonly StringHash32 FabStationLeft = "fab:station-left";
        public static readonly StringHash32 FabStationEnterBegin = "fab:station-enter-begin";
        public static readonly StringHash32 FabMicrogameEntered = "fab:microgame-entered";
        public static readonly StringHash32 FabMicrogameCompleted = "fab:microgame-completed";
        public static readonly StringHash32 FabMicrogameCancelled = "fab:microgame-cancelled";
        // A completed microgame failed the Leaf precision gate; it is paused (no exit animation) awaiting
        // a restart. UI listens for this to show the restart panel.
        public static readonly StringHash32 FabMicrogameRetryRequired = "fab:microgame-retry-required";
        // A paused microgame was reset to a fresh play state via RestartMicrogame.
        public static readonly StringHash32 FabMicrogameRestarted = "fab:microgame-restarted";
        public static readonly StringHash32 FabStationExit = "fab:station-exit";
        public static readonly StringHash32 FabWrongStationAttempt = "fab:wrong-station-attempt";
        public static readonly StringHash32 FabStunBegin = "fab:stun-begin";
        public static readonly StringHash32 FabStunEnd = "fab:stun-end";

        public static readonly StringHash32 FabCompleted = "fab:completed";
        public static readonly StringHash32 FabSucceeded = "fab:succeeded";

        // Fabrication: Sequence
        public static readonly StringHash32 FabGenerateWafer = "fab:generate-wafer";
        public static readonly StringHash32 FabTimeStart = "fab:time-start";
        public static readonly StringHash32 FabSequenceReset = "fab:sequence-reset";
        public static readonly StringHash32 FabSequenceStepCompleted = "fab:sequence-step-completed";
        public static readonly StringHash32 FabSequenceCompleted = "fab:sequence-completed";
        public static readonly StringHash32 FabWaferMisalignment = "fab:wafer-misalignment";
        public static readonly StringHash32 FabCheckpointReached = "fab:checkpoint-reached";
        public static readonly StringHash32 FabCheckpointRestoreBegin = "fab:checkpoint-restore-begin";
        public static readonly StringHash32 FabCheckpointRestoreComplete = "fab:checkpoint-restore-complete";
        public static readonly StringHash32 FabStepUnglitched = "fab:step-unglitched";

        // Design: Simulate Mode
        public static readonly StringHash32 DesignSimPlayStarted = "design-sim:play-started";
        public static readonly StringHash32 DesignSimRowStarted = "design-sim:row-started";
        public static readonly StringHash32 DesignSimPaused = "design-sim:paused";
        public static readonly StringHash32 DesignSimResumed = "design-sim:resumed";
        public static readonly StringHash32 DesignSimRowResolved = "design-sim:row-resolved";
        public static readonly StringHash32 DesignSimSuiteComplete = "design-sim:suite-complete";
        public static readonly StringHash32 DesignSimSuiteSucceeded = "design-sim:suite-succeeded";
        public static readonly StringHash32 DesignSimCancelled = "design-sim:cancelled";

        // Design: Grid
        public static readonly StringHash32 DeisgnGridSetup = "design-grid:setup";
        public static readonly StringHash32 DesignGridModified = "design-grid:modified";
        public static readonly StringHash32 DesignGridCleared = "design-grid:cleared";
        public static readonly StringHash32 DesignToolSelected = "design-grid:tool-selected";
        public static readonly StringHash32 DesignClearSelected = "design-grid:clear-selected";

        // Wiki
        public static readonly StringHash32 WikiPageUnlocked = "wiki:page-unlocked";

        // Overarching
        public static readonly StringHash32 OpenContractView = "overarching:open-contract-view";
        public static readonly StringHash32 SelectContract = "overarching:select-contract"; // when player clicks SelectContractButton

        public static readonly StringHash32 AcceptContract = "overarching:confirm-select-contract";
        public static readonly StringHash32 StartChangeContract = "overarching:start-change-contract";
        public static readonly StringHash32 ConfirmSelectContract = "overarching:confirm-change-contract";
        public static readonly StringHash32 CancelSelectContract = "overarching:cancel-change-contract";
        public static readonly StringHash32 CompleteGoal = "overarching:complete-goal";

        // Supply: Route Drawing
        public static readonly StringHash32 SupplyRouteDrawingOpen = "supply:route-drawing-open";
        public static readonly StringHash32 SupplyRouteDrawingClose = "supply:route-drawing-close";

        // Loading States

        public static readonly StringHash32 ChapterLoaded = "chapter:loaded";
        public static readonly StringHash32 ChapterUnloaded = "chapter:unloaded";
    }

    public static class ScriptTriggers
    {
        public static readonly StringHash32 OnMinigameLoad =        "OnMinigameLoad";
        public static readonly StringHash32 OnWikiOpened =          "OnWikiOpened";
        public static readonly StringHash32 OnWikiClosed =          "OnWikiClosed";
        // Fired when a wiki tab becomes the one on screen. Passes `tabId` — the tab asset's name.
        public static readonly StringHash32 OnWikiTabOpened =       "OnWikiTabOpened";
        // Fired when a wiki page becomes the one on screen. Passes `pageId` — the page asset's
        // name, the same id UnlockWikiPage is keyed by.
        public static readonly StringHash32 OnWikiPageOpened =      "OnWikiPageOpened";
        public static readonly StringHash32 OnOverarchingLoaded =   "OnOverarchingLoaded";

        public static readonly StringHash32 OnGoalCompleted =       "OnGoalCompleted";
    }

    public static class UpdateMasks
    {
        public const int PauseUpdateMask = 1 << 0;

        public const int ResearchMask = 1 << 2;
        public const int DesignMask = 1 << 3;
        public const int SupplyMask = 1 << 4;
        public const int FabricationMask = 1 << 5;

        // overarching
        public const int OverarchingMask = 1 << 6;
        public const int ContractSystemsMask = 1 << 7;
        public const int SetupMask = 1 << 9;

        // fab
        public const int PreAttemptMask = 1 << 11;
        public const int AttemptMask = 1 << 12;
        public const int AttemptLeadInMask = 1 << 13;
        public const int PostAttemptMask = 1 << 14;

        // design
        public const int ToolModeMask = 1 << 15;
        public const int SimulateModeMask = 1 << 16;

        // fab
        public const int MicrogameMask = 1 << 17;

        // research chambers (Battery, Thermal, Combiner, Junction)
        public const int ResearchChamberMask = 1 << 18;
        
        // tutorial
        public const int TutorialMask = 1 << 19;

        // shared UI
        public const int WikiMask = 1 << 20;

        // entire game mask
        public const int EntireGame = (1 << 24) - 1;

        static public void RegisterDebugNames() {
            GameLoop.SetDebugUpdateBitName(0, "Pause");
            GameLoop.SetDebugUpdateBitName(2, "Research");
            GameLoop.SetDebugUpdateBitName(3, "Design");
            GameLoop.SetDebugUpdateBitName(4, "Supply");
            GameLoop.SetDebugUpdateBitName(5, "Fabrication");

            GameLoop.SetDebugUpdateBitName(6, "Overarching");
            GameLoop.SetDebugUpdateBitName(7, "Contract Systems");
            GameLoop.SetDebugUpdateBitName(9, "Setup");

            GameLoop.SetDebugUpdateBitName(11, "Fab: Pre-Attempt");
            GameLoop.SetDebugUpdateBitName(12, "Fab: Attempt");
            GameLoop.SetDebugUpdateBitName(13, "Fab: Attempt Lead-In");
            GameLoop.SetDebugUpdateBitName(14, "Fab: Post-Attempt");

            GameLoop.SetDebugUpdateBitName(15, "Design: Tool Mode");
            GameLoop.SetDebugUpdateBitName(16, "Design: Simulate Mode");

            GameLoop.SetDebugUpdateBitName(17, "Fab: Microgame");
            GameLoop.SetDebugUpdateBitName(18, "Research: Chambers");

            GameLoop.SetDebugUpdateBitName(19, "Tutorial");
            GameLoop.SetDebugUpdateBitName(20, "Wiki");
        }
    }

    static public class LayerMasks
    {

        // Layer 0: Default
        public const int Default_Index = 0;
        public const int Default_Mask = 1;

        // Layer 1: TransparentFX
        public const int TransparentFX_Index = 1;
        public const int TransparentFX_Mask = 2;
        // Layer 2: Ignore Raycast
        public const int IgnoreRaycast_Index = 2;
        public const int IgnoreRaycast_Mask = 4;
        // Layer 4: Water
        public const int Water_Index = 4;
        public const int Water_Mask = 16;
        // Layer 5: UI
        public const int UI_Index = 5;
        public const int UI_Mask = 32;

        // Layer 24: Interrupt UI
        public const int Interrupt_UI_Index = 24;
        public const int Interrupt_UI_Mask = 1 << 24;

        // Layer 6: ResearchSlot — Physics2D layer for slot collider overlap queries
        public const int ResearchSlot_Index = 20;
        public const int ResearchSlot_Mask = 1 << 20;

        // Layer 7: ResearchGem — Physics2D layer for free-floating draggable colliders
        public const int ResearchGem_Index = 21;
        public const int ResearchGem_Mask = 1 << 21;

        // Tutorial focus layer — onboarding parks lock-focused targets here while a
        // tutorial gate is active so that PhysicsRaycaster.eventMask filters all
        // other clicks out before they reach EventSystem.
        public const int TutorialFocus_Index = 22;
        public const int TutorialFocus_Mask = 1 << 22;
    }
}