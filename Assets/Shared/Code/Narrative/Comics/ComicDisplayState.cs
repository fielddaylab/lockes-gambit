using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Collections;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace SpaceFab.Comic
{
    public class ComicDisplayState : SharedStateComponent
    {
        [NonSerialized] public int CurrentPageIndex = -1;
        [NonSerialized] public int CurrentPanelIndex = -1;

        public GameObject NextButtonGroup;
        public GuiButton NextButton;
    }

    static public partial class ComicsUtility {
        static public void PreloadPage(int pageIndex) {
            ComicSequenceManifest manifest = Manifest;
            Assert.NotNullOrDestroyed(manifest);
            Assert.True(pageIndex >= 0 && pageIndex < manifest.Pages.Length);

            var panelRange = manifest.Pages[pageIndex].Panels;
            for(int i = panelRange.Offset; i < panelRange.End; i++) {
                PreloadPanel(manifest, manifest.Panels[i]);
            }
        }

        static public void CancelPreloadPage(int pageIndex) {
            ComicSequenceManifest manifest = Manifest;
            Assert.NotNullOrDestroyed(manifest);
            Assert.True(pageIndex >= 0 && pageIndex < manifest.Pages.Length);

            var panelRange = manifest.Pages[pageIndex].Panels;
            for (int i = panelRange.Offset; i < panelRange.End; i++) {
                CancelPreloadPanel(manifest, manifest.Panels[i]);
            }
        }

        static private void PreloadPanel(ComicSequenceManifest manifest, in PanelData panel) {
            if (panel.MaskIndex != ComicMesh.NullIndex) {
                PreloadMesh(panel.MaskIndex, StreamedMeshType.Mask);
            }

            var layerRange = panel.Layers;
            for(int i = layerRange.Offset; i < layerRange.End; i++) {
                PreloadLayer(manifest, manifest.Layers[i]);
            }
        }

        static private void CancelPreloadPanel(ComicSequenceManifest manifest, in PanelData panel) {
            if (panel.MaskIndex != ComicMesh.NullIndex) {
                CancelMeshPreload(panel.MaskIndex, StreamedMeshType.Mask);
            }

            var layerRange = panel.Layers;
            for (int i = layerRange.Offset; i < layerRange.End; i++) {
                CancelPreloadLayer(manifest, manifest.Layers[i]);
            }
        }

        static private void PreloadLayer(ComicSequenceManifest manifest, in LayerData layer) {
            if (layer.MeshIndex != ComicMesh.NullIndex) {
                PreloadMesh(layer.MeshIndex, StreamedMeshType.Layer);
            }
            if (layer.TextureIndex != ushort.MaxValue) {
                // TODO: texture upload? ensure it's on the gpu
            }
        }

        static private void CancelPreloadLayer(ComicSequenceManifest manifest, in LayerData layer) {
            if (layer.MeshIndex != ComicMesh.NullIndex) {
                CancelMeshPreload(layer.MeshIndex, StreamedMeshType.Layer);
            }
        }

        static public IEnumerator DisplayAndWaitForNextButton(string text) {
            Find.State(out ComicDisplayState displayState);
            displayState.NextButton.LayoutSizeGroup.Root.GetComponent<TMP_Text>().SetText(text);
            displayState.NextButton.LayoutSizeGroup.Sync();
            GuiCommands.SetActive(displayState.NextButtonGroup, true);
            displayState.NextButton.ConsumeClick();
            while(!ScriptUtility.IsSkippingCutscene() && !displayState.NextButton.ConsumeClick()) {
                yield return null;
            }
            if (!ScriptUtility.IsSkippingCutscene())
            {
                SpacefabGame.Events.Dispatch(GameEvents.CutsceneClickedNext, EvtArgs.Create((displayState.CurrentPageIndex, CurrentPanelIndex())));
            }
            GuiCommands.SetActive(displayState.NextButtonGroup, false);
        }

        static private List<StringHash32> s_ComicSceneOrder;

        static public int GetSceneId(StringHash32 packageId)
        {
            if (s_ComicSceneOrder == null)
            {
                Find.GlobalAsset(out ChapterManifest manifest);
                s_ComicSceneOrder = new List<StringHash32>();
                foreach (var data in manifest.ResidentData)
                {
                    if (!data.ComicPackage.IsEmpty && !s_ComicSceneOrder.Contains(data.ComicPackage))
                        s_ComicSceneOrder.Add(data.ComicPackage);
                }
                if (!manifest.EpilogueComicPackage.IsEmpty && !s_ComicSceneOrder.Contains(manifest.EpilogueComicPackage))
                    s_ComicSceneOrder.Add(manifest.EpilogueComicPackage);
            }
            return s_ComicSceneOrder.IndexOf(packageId);
        }

        static public int CurrentSceneId()
        {
            Find.State(out ComicDisplayState displayState);
            return Math.Max(0, displayState.CurrentPageIndex);
        }

        static public int CurrentPanelIndex()
        {
            Find.State(out ComicDisplayState displayState);
            if (displayState.CurrentPageIndex < 0 || displayState.CurrentPanelIndex < 0) return -1;
            return displayState.CurrentPanelIndex - Manifest.Pages[displayState.CurrentPageIndex].Panels.Offset;
        }
    }
}