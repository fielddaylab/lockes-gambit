#if (UNITY_EDITOR && !IGNORE_UNITY_EDITOR) || DEVELOPMENT_BUILD
#define DEVELOPMENT
#endif

using System;
using System.Collections;
using System.IO;
using BeauRoutine;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.SharedState;
using SpaceFab.Save;
using SpaceFab;

namespace SpaceFab.Save
{
    public class SaveLoadState : SharedStateComponent
    {
        public string ServerURL;
        public Routine Operation;

        // Suppresses the automatic saves the game fires on contract accept, minigame exit, chapter
        // advance and so on. Driven by the debug menu toggle.
        [NonSerialized] public bool AutosaveDisabled;

        // Names the bookmark this session was created from or loaded into, empty otherwise. Set once
        // and never cleared: a bookmarked session must never write back over a real player's save.
        // Deliberately outside the DEVELOPMENT guard - bookmarks load in builds, so the firewall has
        // to hold there too.
        [NonSerialized] public string ActiveBookmark;

#if DEVELOPMENT
        [NonSerialized] public bool IsDebug;
#endif // DEVELOPMENT

        private void Awake()
        {
            OGD.Core.Configure(ServerURL, "SPACEFAB");
        }
    }

    static public class SaveUtility
    {
        static public void SetDebugFlag(bool debug)
        {
#if DEVELOPMENT
            Game.SharedState.Get<SaveLoadState>().IsDebug = debug;
#endif // DEVELOPMENT
        }

        static public void Save(SaveSlot slot)
        {
            var save = Game.SharedState.Get<SaveLoadState>();
#if DEVELOPMENT
            if (save.IsDebug)
            {
                return;
            }
#endif // DEVELOPMENT

            // Both of these silently drop the save, which is this feature's most confusing failure
            // mode, so each says why. Reload and LoadFromServer are deliberately left alone: neither
            // writes anything, and after a bookmark load the buffer still holds the bookmark, so
            // "Read Current from Memory" usefully becomes "restart from this bookmark".
            if (!string.IsNullOrEmpty(save.ActiveBookmark))
            {
                Log.Msg("[SaveUtility] Save skipped: session is running from bookmark '{0}'", save.ActiveBookmark);
                return;
            }

            if (save.AutosaveDisabled)
            {
                Log.Msg("[SaveUtility] Save skipped: autosave is disabled from the debug menu");
                return;
            }

            if (save.Operation)
            {
                Log.Error("[SaveUtility] Save/load operation is ongoing");
                return;
            }

            save.Operation = Routine.Start(save, SaveRoutine(slot));
        }

        static public void Reload()
        {
            var save = Game.SharedState.Get<SaveLoadState>();
#if DEVELOPMENT
            if (save.IsDebug)
            {
                return;
            }
#endif // DEVELOPMENT

            if (save.Operation)
            {
                Log.Error("[SaveUtility] Save/load operation is ongoing");
                return;
            }

            save.Operation = Routine.Start(save, ReloadRoutine(true));
        }

        static public Future LoadFromServer(string inUserId)
        {
            var save = Game.SharedState.Get<SaveLoadState>();
#if DEVELOPMENT
            if (save.IsDebug)
            {
                return Future.Failed();
            }
#endif // DEVELOPMENT

            if (save.Operation)
            {
                Log.Error("[SaveUtility] Save/load operation is ongoing");
                return Future.Failed();
            }

            Future future = new Future();
            save.Operation = Routine.Start(save, LoadFromServerRoutine(inUserId, future));
            return future;
        }

        static private IEnumerator SaveRoutine(SaveSlot slot)
        {
            Game.Events.Dispatch(GameEvents.ProfileSaveBegin);

            SpacefabGame.SaveBuffer.Write(slot);
            if (slot == SaveSlot.Main)
            {
                SpacefabGame.SaveBuffer.EncodeToBase64();

#if UNITY_EDITOR
                WriteToFileSystem();
#endif // UNITY_EDITOR

                if (!string.IsNullOrEmpty(SpacefabGame.SaveBuffer.SaveCode))
                {
                    yield return WriteToRemoteSave();
                }
                else
                {
                    Log.Error("[SaveUtility] SaveCode is null or empty -- save failed");
                    Game.Events.Dispatch(GameEvents.ProfileSaveError);
                }
            }
        }

#if UNITY_EDITOR
        static unsafe private void WriteToFileSystem()
        {
            var chars = SpacefabGame.SaveBuffer.GetCurrentBase64();
            Directory.CreateDirectory("Saves");
            string fileName = "Saves/" + DateTime.Now.ToFileTime().ToString() + ".bin";
            using (var str = File.Open(fileName, FileMode.Create))
            {
                using (var stream = new StreamWriter(str))
                {
                    var charsAsSys = new ReadOnlySpan<char>(chars.Ptr, chars.Length);
                    stream.Write(charsAsSys);
                    Log.Msg("[SaveUtility] Wrote save to local file '{0}'", fileName);
                }
            }
        }
#endif // UNITY_EDITOR

        static private IEnumerator WriteToRemoteSave()
        {
            // get save data
            var saveData = SpacefabGame.SaveBuffer.GetCurrentBase64AsString();

            // try to send save data to server

            string profileName = SpacefabGame.SaveBuffer.SaveCode;
            int attempts = (int)(2 + 1);
            int retryCount = 0;
            while (attempts > 0)
            {
                using (var future = Future.Create())
                using (var saveRequest = OGD.GameState.PushState(profileName, saveData, future.Complete, (r) => future.Fail(r), retryCount))
                {
                    yield return future;

                    if (future.IsComplete())
                    {
                        Log.Msg("[SaveUtility] Saved to server!");
                        Game.Events.Dispatch(GameEvents.ProfileSaveSuccess);
                        break;
                    }
                    else
                    {
                        attempts--;
                        Log.Warn("[SaveUtility] Failed to save to server: {0}", future.GetFailure().Object);
                        if (attempts > 0)
                        {
                            Log.Warn("[SaveUtility] Retrying server save...", attempts);
                            yield return 1;
                            ++retryCount;
                        }
                        else
                        {
                            Log.Error("[SaveUtility] Server save failed after {0} attempts", 2 + 1);
                            Game.Events.Dispatch(GameEvents.ProfileSaveError);
                        }
                    }
                }
            }

            Log.Msg("[SaveUtility] ...finished save routine attempt");
            Game.Events.Dispatch(GameEvents.ProfileSaveAttemptCompleted);
        }

        static private IEnumerator ReloadRoutine(bool waitForCutsceneClose)
        {
            yield return null;
            if (SpacefabGame.SaveBuffer.HasSave)
            {
                SpacefabGame.SaveBuffer.Read();
                SpacefabGame.SaveBuffer.HandleChunks();
                SpacefabGame.SaveBuffer.HandlePostLoad();
            }
            Game.Scenes.ReloadMainScene();
        }

        static private IEnumerator LoadFromServerRoutine(string inUserCode, Future response)
        {

            using (var future = Future.Create<string>())
            using (var request = OGD.GameState.RequestLatestState(inUserCode, future.Complete, (r) => future.Fail(r), 0))
            {
                yield return future;

                if (future.IsComplete())
                {
                    bool bSuccess;
                    using (Profiling.Time("reading save data from server"))
                    {
                        bSuccess = SpacefabGame.SaveBuffer.DecodeFromBase64(future.Get());
                        if (bSuccess)
                        {
                            bSuccess = SpacefabGame.SaveBuffer.Read();
                            SpacefabGame.SaveBuffer.HandleChunks();
                            SpacefabGame.SaveBuffer.HandlePostLoad();
                        }
                    }

                    if (!bSuccess)
                    {
                        UnityEngine.Debug.LogErrorFormat("[SaveUtility] Server profile '{0}' could not be read...", inUserCode);
                        response.Fail();
                    }
                    else
                    {
                        response.Complete();
                    }
                }
                else
                {
                    UnityEngine.Debug.LogErrorFormat("[SaveUtility] Failed to find profile on server: {0}", future.GetFailure());
                    response.Fail(future.GetFailure());
                }
            }
        }

        [DebugMenuFactory]
        static private DMInfo SaveDebugMenu()
        {
            DMInfo info = new DMInfo("Save", 4);
            info.AddButton("Write Current to Memory", () => SaveUtility.Save(SaveSlot.Main));
            info.AddButton("Read Current from Memory", () => {
                SaveUtility.Reload();
            }, () => SpacefabGame.SaveBuffer.HasSave);
            return info;
        }
    }
}