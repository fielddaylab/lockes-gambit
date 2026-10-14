using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Animation;
using FieldDay.Assets;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.Threading;
using Leaf.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SpaceFab.Comic {
    static public class ComicScripting {

        [LeafMember("ComicPreloadPage")]
        static public void Leaf_PreloadPage(int pageIndex) {
            ComicsUtility.PreloadPage(pageIndex);
        }

        [LeafMember("ComicPreloadNextPage")]
        static public void Leaf_PreloadNextPage() {
            Find.State(out ComicDisplayState displayState);
            int nextIndex = displayState.CurrentPageIndex + 1;
            if (nextIndex >= 0 && nextIndex < ComicsUtility.Manifest.Pages.Length) {
                ComicsUtility.PreloadPage(nextIndex);
            }
        }

        [LeafMember("ComicOpenPage")]
        static public void Leaf_SpawnPage(int pageIndex) {
            Find.State(out ComicDisplayState displayState);
            displayState.CurrentPageIndex = pageIndex;
            ComicResourceUtility.AllocatePageHierarchy(pageIndex);
        }

        [LeafMember("ComicOpenNextPage")]
        static public void Leaf_SpawnNextPage() {
            Find.State(out ComicDisplayState displayState);
            displayState.CurrentPageIndex++;
            ComicResourceUtility.AllocatePageHierarchy(displayState.CurrentPageIndex);
        }

        [LeafMember("ComicUnloadPage")]
        static public void Leaf_UnloadPage(int pageIndex) {
            ComicResourceUtility.FreePageHierarchy(pageIndex);
            ComicsUtility.CancelPreloadPage(pageIndex);
        }

        [LeafMember("ComicUnloadPreviousPage")]
        static public void Leaf_UnloadPreviousPage() {
            Find.State(out ComicDisplayState displayState);
            int prevPage = displayState.CurrentPageIndex - 1;
            if (prevPage >= 0) {
                ComicResourceUtility.FreePageHierarchy(prevPage);
                ComicsUtility.CancelPreloadPage(prevPage);
            }
        }

        [LeafMember("ComicSetPanelIndex")]
        static public void Leaf_SetPanelByIndex(int panelIndex) {
            Find.State(out ComicDisplayState displayState);
            displayState.CurrentPanelIndex = ComicsUtility.Manifest.Pages[displayState.CurrentPageIndex].Panels.Offset + panelIndex;
        }

        [LeafMember("ComicSetPanelId")]
        static public void Leaf_SetPanelByName(StringHash32 panelName) {
            Find.State(out ComicDisplayState displayState);
            displayState.CurrentPanelIndex = ComicsUtility.GetPanelIndexForName(panelName, displayState.CurrentPageIndex);
        }

        [LeafMember("ComicSnapCamera")]
        static public void Leaf_SnapCamera(StringHash32 cameraId) {
            ComicsUtility.SnapCamera(cameraId);
        }

        [LeafMember("ComicPanCameraAsync")]
        static public void Leaf_PanCamera(StringHash32 cameraId, float duration = 1, Curve easing = Curve.Smooth) {
            ComicsUtility.PanCamera(cameraId, duration, easing);
        }

        [LeafMember("ComicPanCamera")]
        static public IEnumerator Leaf_PanCameraAndWait(StringHash32 cameraId, float duration = 1, Curve easing = Curve.Smooth) {
            AnimHandle pan = ComicsUtility.PanCamera(cameraId, duration, easing);
            while(Game.Animation.IsAnimationRunning(pan)) {
                yield return null;
            }
        }

        [LeafMember("ComicWaitForCamera")]
        static public IEnumerator Leaf_WaitForCamera() {
            Find.State(out ComicCameraState camState);
            while (Game.Animation.IsAnimationRunning(camState.CameraTransition)) {
                yield return null;
            }
        }

        [LeafMember("ComicSpawnMask")]
        static public IEnumerator Leaf_SpawnMask(LayoutSpawnAnimationType animation = default) {
            Find.State(out ComicDisplayState displayState);
            PanelData panel = ComicsUtility.Manifest.Panels[displayState.CurrentPanelIndex];
            if (panel.MaskIndex != ushort.MaxValue) {
                UniqueId16 request = ComicResourceUtility.QueueElementSpawn(panel.MaskIndex, true, animation);
                return WaitTokens.WhileIdAllocated(request, Find.State<ComicLayoutState>().SpawnIdAllocator);
            }
            return null;
        }

        [LeafMember("ComicSpawnLayerIndex")]
        static public IEnumerator Leaf_SpawnLayerByIndex(int index, LayoutSpawnAnimationType animation = default) {
            Find.State(out ComicDisplayState displayState);
            PanelData panel = ComicsUtility.Manifest.Panels[displayState.CurrentPanelIndex];
            Assert.True(index >= 0 && index < panel.Layers.Length, "Index out of range");
            UniqueId16 request = ComicResourceUtility.QueueElementSpawn((ushort) (panel.Layers.Offset + index), false, animation);
            return WaitTokens.WhileIdAllocated(request, Find.State<ComicLayoutState>().SpawnIdAllocator);
        }

        [LeafMember("ComicSpawnLayerId")]
        static public IEnumerator Leaf_SpawnLayerById(StringHash32 name, LayoutSpawnAnimationType animation = default) {
            Find.State(out ComicDisplayState displayState);
            PanelData panel = ComicsUtility.Manifest.Panels[displayState.CurrentPanelIndex];
            for(int i = panel.Layers.Offset; i < panel.Layers.End; i++) {
                if (ComicsUtility.Manifest.Layers[i].Id == name) {
                    UniqueId16 request = ComicResourceUtility.QueueElementSpawn((ushort) i, false, animation);
                    return WaitTokens.WhileIdAllocated(request, Find.State<ComicLayoutState>().SpawnIdAllocator);
                }
            }
            Assert.Fail("No node in current panel with id '{0}'", name);
            return null;
        }

        [LeafMember("ComicNextButton")]
        static public IEnumerator Leaf_NextButton([BindThread] ScriptThread thread) {
            bool predictEnd = LeafRuntime.PredictEnd(thread);
            return ComicsUtility.DisplayAndWaitForNextButton(predictEnd ? "Close" : "Next");
        }

        [LeafMember("ComicLoad")]
        static public void LoadComic(StringHash32 comicId) {
            Game.Scenes.LoadMainScene(SceneReference.FromName("ComicScene"), true);
            Game.Scenes.QueueMainLoadContext(new SceneRequestContext() {
                Task = comicId
            });
        }

        [LeafMember("ComicLoadForChapter")]
        static public void LoadComicForChapter(int chapterIndex) {
            Find.GlobalAsset(out ChapterManifest manifest);
            Game.Scenes.LoadMainScene(SceneReference.FromName("ComicScene"), true);
            Game.Scenes.QueueMainLoadContext(new SceneRequestContext() {
                Task = manifest.ResidentData[chapterIndex].ComicPackage
            });
        }

        [LeafMember("ComicLoadForCurrentChapter")]
        static public void LoadComicForCurrentChapter() {
            Find.State(out ChapterState chapterState);
            LoadComicForChapter(chapterState.ChapterIndex);
        }
    }
}