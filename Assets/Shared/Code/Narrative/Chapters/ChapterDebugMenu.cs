using System.Text;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Scenes;
using FieldDay.Scripting;
using SpaceFab.Save;

namespace SpaceFab {
    /// <summary>
    /// Debug menu for jumping between chapters: contributes the Chapters root, one
    /// slot button per chapter in the manifest. Each slot clears the
    /// active contract and restarts the overarching startup sequence on the target chapter, which
    /// lands the player at that chapter's contract selection.
    /// </summary>
    public static class ChapterDebugMenu {
        // Fixed pool of chapter slot buttons. Slots past the manifest's chapter count are disabled by
        // their predicate.
        private const int MaxChapterSlots = 12;

        // Contributes the Chapters root.
        [DebugMenuFactory]
        private static DMInfo CreateChapterDebugMenu() {
            DMInfo menu = new DMInfo("Chapters", MaxChapterSlots + 1);

            // Live readout of the chapter the save is currently on.
            menu.AddText("Current", AppendCurrentChapter);

            for (int i = 0; i < MaxChapterSlots; i++) {
                int index = i; // capture per-iteration for the closures
                menu.AddButton("Skip to chapter #" + (i + 1), () => DebugSkipToChapter(index), () => index < ChapterCount() && CanSkip());
            }

            return menu;
        }

        // Chapter count straight off the manifest, or 0 while it isn't mounted. Unlike
        // ChapterUtility.ChapterCount this never asserts, since it runs every frame the menu is open.
        private static int ChapterCount() {
            return Game.Assets.TryGetGlobal(out ChapterManifest manifest) ? manifest.Chapters.Length : 0;
        }

        // True when every state the skip writes to is present, no scene load is already underway, and
        // no chapter/contract data load is in flight.
        private static bool CanSkip() {
            if (Game.Scenes.IsLoadingAnyScene() || Game.Scenes.IsMainLoading()) {
                return false;
            }

            if (!Game.SharedState.Has<ChapterState>() || !Game.SharedState.Has<ContractState>()
                || !Game.SharedState.Has<PlayerProgressState>() || !Game.SharedState.Has<MinigameSaveStates>()) {
                return false;
            }

            // Unloading mid-load would tear down half-populated state, so hold off until both settle.
            return !Find.State<ChapterState>().LoadRoutine && !Find.State<ContractState>().LoadRoutine;
        }

        // Writes the active chapter as "#index id (of count)". Kept to a single short line on purpose:
        // DMInfo.SharedTextBuilder is capacity-locked at 128 characters and throws rather than growing,
        // so a per-chapter listing would overflow it as soon as the manifest gained a few entries.
        private static void AppendCurrentChapter(StringBuilder sb) {
            if (!Game.SharedState.Has<ChapterState>()) {
                sb.Append("(no chapter state)");
                return;
            }

            ChapterState chapterState = Find.State<ChapterState>();
            sb.Append('#').Append(chapterState.ChapterIndex);
            if (!chapterState.ChapterId.IsEmpty) {
                sb.Append(' ').Append(chapterState.ChapterId.ToDebugString());
            }
            sb.Append(" (of ").Append(ChapterCount()).Append(')');
        }

        // Drops the active contract and restarts the game on the given chapter. The chapter's own data
        // is reloaded by ChapterLoader when the overarching scene preloads, so this only has to leave
        // the persistent state in the shape a fresh chapter expects before kicking off the load.
        private static void DebugSkipToChapter(int chapterIndex) {
            if (!CanSkip()) {
                Log.Warn("[ChapterDebugMenu] Skip To Chapter unavailable: required states missing or a scene load is in progress");
                return;
            }

            if (chapterIndex < 0 || chapterIndex >= ChapterCount()) {
                Log.Warn("[ChapterDebugMenu] Skip To Chapter: index {0} out of range", chapterIndex);
                return;
            }

            Find.State(out ChapterState chapterState, out ContractState contractState, out PlayerProgressState progressState, out MinigameSaveStates saveStates);

            // Freeze the game for the transition the way the real chapter advance does, leaving only
            // the script runtime alive, then kill the threads still running against the chapter and
            // contract scripts that are about to be unloaded.
            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(ScriptUtility.RuntimeUpdateMask);
            ScriptUtility.KillAllThreads();

            // Clear the active contract: unload its assets and script, forget the selection so startup
            // reopens contract select, and wipe the per-contract minigame progress it seeded.
            ContractUtility.UnloadContractData(contractState);
            chapterState.LastSelectedContractIndex = -1;
            MinigameSaveUtility.ClearMinigameState(saveStates);

            chapterState.ChapterIndex = chapterIndex;
            SaveUtility.Save(SaveSlot.Main);

            // Jump to the hub rather than reloading whatever scene the skip was triggered from.
            // forceReload so the startup sequence reruns even when the hub is already the main scene.
            Game.Scenes.LoadMainScene(SceneReference.FromName(GameConsts.OverarchingSceneName), true);
            Log.Msg("[ChapterDebugMenu] Skipped to chapter #{0}", chapterIndex);
        }
    }
}
