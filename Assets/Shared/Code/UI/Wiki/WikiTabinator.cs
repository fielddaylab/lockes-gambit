using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public class WikiTabinator : GuiWidget, IScenePreload {
        [Header("Tabs")]
        public WikiTab[] Tabs;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Action<PointerListener.EventData> handler = WikiLayoutUtility.HandleTabClicked;
            for(int i = 0; i < Tabs.Length; i++) {
                Tabs[i].Widget.OnClick.Register(handler);
            }
            return null;
        }
    }

    static public partial class WikiLayoutUtility {
        #region Handlers

        static public void HandleTabClicked(PointerListener.EventData data) {
            int queuedTabId = GuiButton.GetData(data).AsInt();
            Find.State(out WikiViewState viewState);
            viewState.QueuedTabId = queuedTabId;
            viewState.QueuedPageId = -1;
            Log.Msg("[WikiTabinator] Queued tab {0}, frame {1}", queuedTabId, Frame.Index);
        }

        #endregion // Handlers

        /// <summary>
        /// Syncs the toggle states of each tab to align with the current selection.
        /// </summary>
        static public void UpdateSelectedTab(WikiTabinator tabinator, int selectedId, bool instant) {
            GuiWidgetUpdateFlags updateFlags = instant ? GuiWidgetUpdateFlags.Initialization : GuiWidgetUpdateFlags.Default;
            for(int i = 0; i < tabinator.Tabs.Length; i++) {
                WikiTab tabVisuals = tabinator.Tabs[i];
                if (!tabVisuals.gameObject.activeSelf) {
                    break;
                }

                tabVisuals.Widget.SetToggleState(tabVisuals.Widget.GetVariantValue().AsInt() == selectedId, updateFlags);
            }
        }

        /// <summary>
        /// Populates tab visuals with the available tabs in the content.
        /// </summary>
        static public unsafe void PopulateTabs(WikiTabinator tabinator, WikiContent content) {
            foreach(var tab in tabinator.Tabs) {
                tab.Taggable.SetId(null);
            }

            int tabCount = content.AvailableTabs.Count;
            for(int i = 0; i < tabCount; i++) {
                int tabIndex = content.AvailableTabs.Indices[i];
                WikiTabData tabData = content.Tabs[tabIndex];
                WikiTab tabVisuals = tabinator.Tabs[i];
                PopulateTabButton(tabVisuals, tabData, tabIndex);
                tabVisuals.gameObject.SetActive(true);
            }

            for(int i = tabCount; i < tabinator.Tabs.Length; i++) {
                tabinator.Tabs[i].Widget.SetVariantValue(-1);
                tabinator.Tabs[i].gameObject.SetActive(false);
            }
        }
    }
}