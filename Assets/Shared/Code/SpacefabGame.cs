using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Processes;
using FieldDay.Systems;
using SpaceFab.Save;
using SpaceFab;
using UnityEngine;
using FieldDay.Music;
using FieldDay.Scenes;
using BeauUtil;

namespace SpaceFab
{
    public sealed class SpacefabGame : Game
    {
        static public new EventDispatcher<EvtArgs> Events { get; private set; }
        static public TransitionStateMgr TransitionState { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; internal set; }
        static public SaveMgr SaveBuffer { get; private set; }

        static private bool s_IsInGame;

        [InvokePreBoot]
        static private void OnPreBoot() {
            Events = new EventDispatcher<EvtArgs>();
            SetEventDispatcher(Events);

            Log.Msg("[SpacefabGame] Creating TransitionState manager...");
            TransitionState = new TransitionStateMgr();

            Log.Msg("[SpacefabGame] Creating Save manager...");
            SaveBuffer = new SaveMgr();

            Log.Msg("[SpacefabGame] Creating Music player...");
            MusicPlayer.Initialize();
            MusicPlayer.SetDefaultTransition(new MusicTransitionParams() {
                FadeIn = 0.4f,
                FadeOut = 0.4f,
                Overlap = 0.2f
            });
            MusicPlayer.ConfigureSceneUnloadBehavior(true, "PreserveMusic");

            Scenes.OnLoadProcessStarted.Register(OnLoadProcessStarted);
            Scenes.OnMainSceneLoadQueued.Register(OnMainSceneLoadQueued);
            Scenes.OnMainSceneUnloaded.Register(OnMainSceneUnloaded);

            UpdateMasks.RegisterDebugNames();
        }

        static private void OnMainSceneLoadQueued() {
            Game.Input.PauseAll();
        }

        static private void OnMainSceneUnloaded() {
            Game.Input.ResumeAll();
        }

        static private void OnLoadProcessStarted(SceneProcessCallbackArgs args) {
            if (args.LoadType != SceneType.Main) {
                return;
            }

            bool inGame = args.SceneIndex > 3;
            if (s_IsInGame != inGame) {
                s_IsInGame = inGame;

                if (inGame) {
                    Assets.LoadStreamedPackage("InGameStream");
                    Scenes.LoadPersistentScene(SceneReference.FromName("InGameUI"), "InGame");
                } else {
                    Assets.UnloadStreamedPackage("InGameStream");
                    Scenes.UnloadScenesByTag("InGame");
                }
            }

            Scenes.GetQueuedLoadContext(out SceneRequestContext context);
            if (context.Get("QueueSave").AsBool()) {
                Scenes.QueueOnEnable(() => SaveUtility.Save(SaveSlot.Main));
            }
        }

        static public bool IsSceneInGame { get { return s_IsInGame; } }

        [InvokeOnBoot]
        static private void OnBoot() {
            Game.Scenes.LoadPersistentScene(SceneReference.FromName("PersistentUI"));
        }
    }
}