using System;
using System.Collections;
using BeauRoutine;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.Scripting;
using UnityEngine;
using BeauUtil;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif // UNITY_EDITOR

namespace SpaceFab.Save {
    /// <summary>
    /// Creation and loading of debug bookmarks: named snapshots of a playthrough, stored as the same
    /// base64 blob SaveMgr already produces for the server. Pairs with SaveLoadState, whose
    /// ActiveBookmark field names the bookmark a session came from and permanently blocks saving.
    ///
    /// Creating a bookmark is Editor-only, since it writes a project asset and drives a modal panel;
    /// loading one works in builds too. Both arm the save firewall, so a bookmarked session can never
    /// write back over a real player's server save.
    /// </summary>
    public static class BookmarkUtility {
        // The path passed to Resources.Load, and the project folder backing it. Bookmarks must live
        // here specifically: Resources.Load cannot see files outside a Resources folder.
        public const string BookmarkResourcePath = "Bookmarks";
        public const string BookmarkAssetFolder = "Assets/Resources/Bookmarks";

        /// <summary>
        /// Returns every bookmark name under Resources/Bookmarks, sorted. Called once at boot to build
        /// the debug menu; the TextAssets are released as they are read so the full set never stays
        /// resident.
        /// </summary>
        public static string[] EnumerateNames() {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(BookmarkResourcePath);
            string[] names = new string[assets.Length];

            for (int i = 0; i < assets.Length; i++) {
                names[i] = assets[i].name;
                Resources.UnloadAsset(assets[i]);
            }

            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }

        /// <summary>
        /// True when a bookmark can be applied right now. Runs every frame the debug menu is open, so
        /// it stays to cheap checks.
        /// </summary>
        public static bool CanLoad(SaveLoadState save) {
            if (save.Operation.Exists()) {
                return false;
            }

            if (Game.Scenes.IsLoadingAnyScene() || Game.Scenes.IsMainLoading()) {
                return false;
            }

            // SaveMgr.Read writes the blob's player code straight into UserSettingsState.
            return Game.SharedState.Has<UserSettingsState>();
        }

        /// <summary>
        /// Applies the named bookmark, replacing the live save data and player code, then hands off to
        /// the hub. Arms the save firewall on success.
        /// </summary>
        public static void Load(SaveLoadState save, string name) {
            if (save.Operation) {
                Log.Error("[BookmarkUtility] Save/load operation is ongoing");
                return;
            }

            save.Operation = Routine.Start(save, LoadRoutine(save, name));
        }

        // Mirrors SaveUtility.LoadFromServerRoutine's decode -> Read -> HandleChunks sequence, then
        // hands off to the hub the way ChapterDebugMenu's chapter skip does. Every failure path bails
        // before the firewall is armed, so a bad bookmark leaves saving enabled.
        private static IEnumerator LoadRoutine(SaveLoadState save, string name) {
            TextAsset asset = Resources.Load<TextAsset>(BookmarkResourcePath + "/" + name);
            if (asset == null) {
                Log.Warn("[BookmarkUtility] No bookmark named '{0}' under Resources/{1}", name, BookmarkResourcePath);
                yield break;
            }

            bool decoded = SpacefabGame.SaveBuffer.DecodeFromBase64(asset.text.Trim());
            Resources.UnloadAsset(asset);

            if (!decoded) {
                Log.Error("[BookmarkUtility] Bookmark '{0}' could not be decoded - not valid base64, or larger than SaveMgr's main buffer", name);
                yield break;
            }

            if (!SpacefabGame.SaveBuffer.Read()) {
                Log.Error("[BookmarkUtility] Bookmark '{0}' decoded but could not be read - the chunk layout may predate the current save format", name);
                yield break;
            }

            SpacefabGame.SaveBuffer.HandleChunks();
            SpacefabGame.SaveBuffer.HandlePostLoad();
            save.ActiveBookmark = name;

            // Freeze the game for the transition, leaving only the script runtime alive, then kill the
            // threads still running against the chapter and contract scripts about to be unloaded.
            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(ScriptUtility.RuntimeUpdateMask);
            ScriptUtility.KillAllThreads();

            // The chapter and contract assets need no manual unloading: ChapterLoader and
            // OverarchingStartupSequenceSystem rebuild both from the ChapterIndex and
            // LastSelectedContractIndex the chunks just restored, and each swaps out whatever was
            // loaded before when the id differs. forceReload so startup reruns even when the hub is
            // already the main scene.
            Game.Scenes.LoadMainScene(SceneReference.FromName(GameConsts.OverarchingSceneName), true);
            Log.Msg("[BookmarkUtility] Loaded bookmark '{0}' - saving is disabled for the rest of this session", name);
        }

#if UNITY_EDITOR

        /// <summary>
        /// Snapshots the live game state into a named bookmark asset under Resources/Bookmarks, then
        /// arms the save firewall.
        /// </summary>
        public static void Create(SaveLoadState save) {
            // Snapshot through SaveMgr directly rather than SaveUtility.Save: this has to capture state
            // without a server push, and without being turned away by the firewall it is about to arm.
            // EncodeToBase64 reads the main buffer, so SaveSlot.Uncommitted is not usable here.
            SpacefabGame.SaveBuffer.Write(SaveSlot.Main);
            SpacefabGame.SaveBuffer.EncodeToBase64();
            string encoded = SpacefabGame.SaveBuffer.GetCurrentBase64AsString();

            Directory.CreateDirectory(BookmarkAssetFolder);

            // The panel is modal and takes focus while the game may have the cursor hidden.
            bool cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            string path = EditorUtility.SaveFilePanelInProject("Save Bookmark", string.Empty, "txt", "Choose a name for this bookmark", BookmarkAssetFolder);
            Cursor.visible = cursorWasVisible;

            if (string.IsNullOrEmpty(path)) {
                return;
            }

            // Resources.Load only sees files inside a Resources folder, and EnumerateNames keys on the
            // bare filename while LoadAll recurses - so a bookmark tucked into a subfolder would be
            // listed but never loadable. Refuse rather than write a file the loader can't reach.
            if (!path.StartsWith(BookmarkAssetFolder + "/", StringComparison.OrdinalIgnoreCase)
                || path.IndexOf('/', BookmarkAssetFolder.Length + 1) >= 0) {
                Log.Error("[BookmarkUtility] Bookmarks must be saved directly in '{0}' - '{1}' would never be loadable", BookmarkAssetFolder, path);
                return;
            }

            File.WriteAllText(path, encoded);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string name = Path.GetFileNameWithoutExtension(path);
            save.ActiveBookmark = name;

            // The debug menu's bookmark list is built once at boot and cannot be safely rebuilt while
            // the menu exists, so a bookmark made now shows up in the list next play session.
            Log.Msg("[BookmarkUtility] Wrote bookmark '{0}' to '{1}'. Saving is now disabled for this session; the bookmark appears in the debug menu next play session.", name, path);
        }

#endif // UNITY_EDITOR
    }
}
