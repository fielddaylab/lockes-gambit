using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scenes;
using UnityEngine;

namespace SpaceFab.Narrative {
    [PreloadOrder(-99)]
    public sealed class ChapterSkyboxLoader : MonoBehaviour, IScenePreload {
        public Material Fallback;
        public Vector3 Rotation;

        private void Awake() {
            Game.Events.Register(GameEvents.ChapterLoaded, ApplyCurrentChapter)
                .Register(GameEvents.ChapterUnloaded, ClearChapter);
        }

        private void OnDestroy() {
            Game.Events?.DeregisterAllForContext(this);
        }

        private void ApplyCurrentChapter() {
            if (!Game.SharedState.TryGet(out ChapterState chapterState) || !chapterState.ChapterDefinition) {
                ClearChapter();
                return;
            }

            Material material = chapterState.ChapterDefinition.SkyboxMaterial;
            if (!material) {
                Log.Warn("[ChapterSkyboxLoader] No skybox set for chapter '{0}'", chapterState.ChapterId);
                material = Fallback;
            }

            RenderSettings.skybox = material;
            Game.Rendering.PrimaryCamera.clearFlags = CameraClearFlags.Skybox;
        }

        private void ClearChapter() {
            RenderSettings.skybox = Fallback;
        }

        public IEnumerator<WorkSlicer.Result?> Preload() {
            ApplyCurrentChapter();
            return null;
        }
    }
}