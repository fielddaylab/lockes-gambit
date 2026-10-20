using System.Text;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Scripting;

namespace SpaceFab.Overarching
{
    /// <summary>
    /// Development-only debug menu for jumping to game states: contributes Contracts -> Set Contract and
    /// Minigames -> JumpTo. Registered via [DebugMenuFactory] (auto-discovered at boot; compiled out of
    /// release builds since the attribute is Conditional). The Contracts / Minigames roots merge with the
    /// same-named menus contributed by other factories. Both tools depend on Overarching state and no-op
    /// gracefully when it isn't present.
    ///
    /// NOTE: DMMenuUI builds its element views (PopulateMenu) BEFORE firing a menu's OnEnter, so mutating a
    /// DMInfo's Elements at runtime desyncs the views and crashes UpdateElements. All elements here are
    /// therefore built once at boot; dynamic content is expressed via live text getters and predicate-gated
    /// buttons (both re-evaluated every frame), never by rebuilding the element list.
    /// </summary>
    public static class OverarchingDebugMenu
    {
        // Fixed pool of contract slot buttons. Out-of-range slots are disabled by their predicate. A chapter
        // is expected to have far fewer than this many available contracts.
        private const int MaxContractSlots = 8;

        // Contributes Contracts -> Set Contract. The Contracts root merges with any other factory's
        // Contracts menu.
        [DebugMenuFactory]
        private static DMInfo CreateContractsDebugMenu()
        {
            DMInfo menu = new DMInfo("Contracts", 1);
            menu.AddSubmenu(BuildSetContractMenu());
            return menu;
        }

        // Contributes Minigames -> JumpTo. The Minigames root merges with the SetSolved button and
        // the Design submenu contributed by other factories.
        [DebugMenuFactory]
        private static DMInfo CreateMinigamesJumpDebugMenu()
        {
            DMInfo menu = new DMInfo("Minigames", 1);
            menu.AddSubmenu(BuildJumpToMenu());
            return menu;
        }

        // True while a scene load or an Overarching->minigame transition is underway. The jump/set buttons
        // disable themselves during this window so a debug action can't race an in-flight transition.
        private static bool IsTransitioning()
        {
            if (Game.Scenes.IsLoadingAnyScene() || Game.Scenes.IsMainLoading())
            {
                return true;
            }
            if (!Game.SharedState.Has<OverarchingState>())
            {
                return true;
            }
            if (!Game.SharedState.TryGet(out ContractState contractState) || contractState.LoadRoutine.Exists())
            {
                return true;
            }
            return false;
        }

        // ---- Set Contract ----

        private static DMInfo BuildSetContractMenu()
        {
            DMInfo menu = new DMInfo("Set Contract", MaxContractSlots + 1);

            // Live listing of the available contracts (index: title), refreshed every frame.

            menu.AddText("Contracts", AppendContractList);
            // Fixed slot buttons; the predicate enables only the slots that map to a real contract.
            for (int i = 0; i < MaxContractSlots; i++)
            {
                int index = i; // capture per-iteration for the closures
                menu.AddButton("Set contract #" + i, () => DebugSetContract(index), () => index < AvailableContractCount() && !IsTransitioning());
            }

            // Opening the menu loads the chapter's available-contracts bundle if it isn't already (safe: no
            // element mutation), so the slots populate even from outside the contract-select screen.
            menu.OnEnter.Register(EnsureContractsLoaded);
            return menu;
        }

        // Loads the current chapter's available-contracts bundle on menu open if it isn't loaded yet.
        private static void EnsureContractsLoaded(DMInfo menu)
        {
            ChapterUtility.LoadCurrentChapter(Find.State<ChapterState>());
        }

        static private int AvailableContractCount() {
            Find.State(out ChapterState chapterState);
            return chapterState.ChapterDefinition == null ? 0 : chapterState.ChapterDefinition.AvailableContracts.Length;
        }

        private static void AppendContractList(StringBuilder sb)
        {
            Find.State(out ChapterState chapterState);
            if (chapterState.ChapterDefinition == null || chapterState.ChapterDefinition.AvailableContracts.Length == 0) {
                sb.Append("(no contracts loaded)");
                return;
            }

            StringHash32[] contracts = chapterState.ChapterDefinition.AvailableContracts;
            for (int i = 0; i < contracts.Length; i++) {
                ContractDef contract = ContractUtility.GetDefinition(contracts[i]);

                if (i > 0) { sb.Append('\n'); }
                string title = string.IsNullOrEmpty(contract.Title()) ? ("Contract " + i) : contract.Title();
                sb.Append(i).Append(": ").Append(title);
            }
        }

        // Applies the chosen contract as the active one via the shared data path (no selection UI). Runs the
        // confirm data core as a routine because it waits on the contract-assets scene load.
        private static void DebugSetContract(int index)
        {
            if (!Game.SharedState.Has<ChapterState>() || !Game.SharedState.Has<PlayerProgressState>()) {
                Log.Warn("[OverarchingDebugMenu] Set Contract unavailable: required states not present");
                return;
            }

            if (IsTransitioning()) {
                Log.Warn("[OverarchingDebugMenu] Set Contract ignored: a scene load or transition is in progress");
                return;
            }

            ChapterState chapterState = Find.State<ChapterState>();
            ChapterDef bundle = chapterState.ChapterDefinition;
            if (bundle == null || bundle.AvailableContracts == null || index < 0 || index >= bundle.AvailableContracts.Length) {
                Log.Warn("[OverarchingDebugMenu] Set Contract: index {0} out of range or contracts not loaded", index);
                return;
            }

            PlayerProgressState playerProgress = Find.State<PlayerProgressState>();
            ContractState contractState = Find.State<ContractState>();
            StringHash32 contractId = bundle.AvailableContracts[index];

            // Host on the lookup so the routine is stopped if its scene unloads mid-flight, and keep the
            // handle so IsTransitioning() can hold off a minigame jump until the contract is fully applied.
            ContractViewUtility.ApplyContract(index);
            Log.Msg("[OverarchingDebugMenu] Set contract to index {0} ('{1}')", index, ContractUtility.GetDefinition(contractId).Title());
        }

        // ---- JumpTo ----

        private static DMInfo BuildJumpToMenu()
        {
            DMInfo menu = new DMInfo("JumpTo", (int) MinigameId.COUNT);
            for (int i = 0; i < (int) MinigameId.COUNT; i++)
            {
                MinigameId id = (MinigameId) i; // capture per-iteration for the closure
                menu.AddButton(id.ToString(), () => DebugEnterMinigame(id), () => !IsTransitioning());
            }
            return menu;
        }

        // Force-enters the given minigame from the Overarching hub, bypassing the zone lock (the lock is only
        // enforced in the UI click handler; ConfirmEnterMinigame itself does not check it). No-ops elsewhere.
        private static void DebugEnterMinigame(MinigameId id)
        {
            if (!Game.SharedState.Has<MinigameZonesState>())
            {
                Log.Warn("[OverarchingDebugMenu] Enter Minigame only works from the Overarching hub");
                return;
            }

            if (IsTransitioning())
            {
                Log.Warn("[OverarchingDebugMenu] Enter Minigame ignored: a scene load or transition is in progress");
                return;
            }

            var zones = Find.Components<MinigameZone>();
            foreach(var zone in zones) {
                if (zone.Minigame == id) {
                    ScriptUtility.KillAllThreads();
                    Find.State<MinigameZonesState>().QueuedZone = zone;
                    Log.Msg("[OverarchingDebugMenu] Force-entering minigame {0}", id);

                    MinigameZonesState state = Find.State<MinigameZonesState>();
                    if (state.QueuedZone != null) {
                        MinigameZonesUtility.AttemptStartGame(state.QueuedZone);
                        state.QueuedZone = null;
                    }
                    return;
                }
            }

            Log.Warn("[OverarchingDebugMenu] No zone found for minigame {0}", id);
        }
    }
}
