using BeauUtil;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.UI;
using Leaf.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceFab.Comic {
    public sealed class ComicLoader : SceneController, ISceneLoadDependency {
        public SceneReference NextScene;
        
        [Header("-- DEBUG --")]
        [AssetName(typeof(StreamedPack))] public StringHash32 DEBUG_FallbackManifest;

        [NonSerialized] public StringHash32 PackageId;
        [NonSerialized] public StringHash32 ComicId;
        [NonSerialized] public LeafThreadHandle ThreadHandle;

        protected override IEnumerator<WorkSlicer.Result?> OnScenePreload() {
            Game.Scenes.GetLoadContext(this, out SceneRequestContext context);
            StringHash32 name = StringHash32.First(context.Task.Name, Game.IsDevBuild ? DEBUG_FallbackManifest : default);
            if (!name.IsEmpty) {
                PackageId = name;
                Game.Assets.LoadStreamedPackage(name);
            }
            Find.State(
                out ComicDisplayState displayState
            );
            GuiCommands.SetActive(displayState.NextButtonGroup, false);
            return null;
        }

        protected override void OnSceneEnable() {
            Game.Scenes.RegisterLoadDependency(this);
            if (ComicsUtility.Manifest != null) {
                ComicId = ComicsUtility.Manifest.name;
            } else {
                ComicId = null;
            }

            if (!ComicId.IsEmpty) {
                ComicsUtility.SnapCamera(0);

                using (var table = TempVarTable.Alloc()) {
                    table.Set("comicId", ComicId);
                    int preloadThreads = ScriptUtility.Invoke("ComicPreload", table);
                    if (preloadThreads <= 0) {
                        ComicsUtility.PreloadPage(0);
                        ComicResourceUtility.AllocatePageHierarchy(0);
                    }
                }
            }

            ScriptUtility.OnCutsceneEnd.Register(QueueFinish);
        }

        protected override void OnSceneReady() {
            if (ComicId.IsEmpty) {
                GameLoop.QueuePreUpdate(Finish);
            } else {
                using (var table = TempVarTable.Alloc()) {
                    table.Set("comicId", ComicId);
                    ThreadHandle = ScriptUtility.Trigger("ComicExecute", table);
                    if (!ThreadHandle.IsRunning()) {
                        QueueFinish();
                    }
                }
            }
        }

        // Aborts the running comic cutscene immediately and advances to NextScene as if it had ended normally.
        // Safe to call mid-cutscene: kills live Leaf threads first so queued comic ops don't keep firing into
        // the tearing-down scene.
        public void Skip() {
            ScriptUtility.KillAllThreads();
            QueueFinish();
        }

        private void QueueFinish() {
            GameLoop.QueuePreUpdate(Finish);
        }

        private void Finish() {
            Game.Scenes.LoadMainScene(NextScene);
            Game.Scenes.QueueMainLoadContext(new SceneRequestContext() {
                Entrance = ComicId
            });
        }

        protected override void OnSceneUnload() {
            ScriptUtility.OnCutsceneEnd.Deregister(Finish);
            Game.Scenes.DeregisterLoadDependency(this);
            if (!PackageId.IsEmpty) {
                Game.Assets.UnloadStreamedPackage(PackageId);
            }
        }

        bool ISceneLoadDependency.IsLoaded(SceneLoadFence loadPhase) {
            return ScriptUtility.CountFunctionThreads() <= 0;
        }
    }
}