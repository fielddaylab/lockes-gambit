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
    public class WikiPaginator : GuiWidget, IScenePreload {
        [Header("Controls")]
        public GuiButton PageScrollLeft;
        public GuiButton PageScrollRight;

        [Header("Page")]
        public WikiPageButton[] Pages;

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            Action<PointerListener.EventData> buttonHandler = WikiLayoutUtility.HandlePageClicked;
            for (int i = 0; i < Pages.Length; i++) {
                Pages[i].Widget.OnClick.Register(buttonHandler);
            }

            Action<PointerListener.EventData> scrollHandler = WikiLayoutUtility.HandlePageScrollClicked;
            PageScrollLeft.SetVariantValue(-1);
            PageScrollRight.SetVariantValue(1);
            PageScrollLeft.OnClick.Register(scrollHandler);
            PageScrollRight.OnClick.Register(scrollHandler);
            return null;
        }
    }

    static public partial class WikiLayoutUtility {

        #region Handlers

        static public void HandlePageClicked(PointerListener.EventData data) {
            int queuedPageId = GuiButton.GetData(data).AsInt();
            Find.State(out WikiViewState viewState);
            viewState.QueuedTabId = -1;
            viewState.QueuedPageId = queuedPageId;
            Log.Msg("[WikiPaginator] Queued page {0}, frame {1}", queuedPageId, Frame.Index);
        }

        static public void HandlePageScrollClicked(PointerListener.EventData data) {
            int queuedScrollDirection = GuiButton.GetData(data).AsInt();
            Find.State(out WikiViewState viewState);
            viewState.QueuedPageScrollDirection = queuedScrollDirection;
            Log.Msg("[WikiPaginator] Queued scroll {0}, frame {1}", queuedScrollDirection, Frame.Index);
        }

        #endregion // Handlers

        /// <summary>
        /// Populates page visuals with the available pages in the content.
        /// </summary>
        static public unsafe void PopulatePages(WikiPaginator paginator, WikiContent content, int tabId, int scrollOffset) {
            foreach (var page in paginator.Pages) {
                page.Taggable.SetId(null);
            }

            WikiTabData tabData = null;
            WikiContentList availablePages = default;
            int pageCount = 0;
            if (tabId >= 0) {
                tabData = content.Tabs[tabId];
                availablePages = content.TabPages[tabId];
                pageCount = Math.Max(0, Math.Min(paginator.Pages.Length, (availablePages.Count - scrollOffset)));
            }

            for (int i = 0; i < pageCount; i++) {
                int pageIndex = availablePages.Indices[i + scrollOffset];
                WikiPageData pageData = tabData.Pages[pageIndex];
                WikiPageButton pageButton = paginator.Pages[i];
                PopulatePageButton(pageButton, pageData, pageIndex);
                pageButton.gameObject.SetActive(true);
            }

            for (int i = pageCount; i < paginator.Pages.Length; i++) {
                paginator.Pages[i].Widget.SetVariantValue(-1);
                paginator.Pages[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Syncs the toggle states of each page button to align with the current selection.
        /// </summary>
        static public void UpdateSelectedPage(WikiPaginator paginator, int selectedId, bool instant) {
            GuiWidgetUpdateFlags updateFlags = instant ? GuiWidgetUpdateFlags.Initialization : GuiWidgetUpdateFlags.Default;
            for (int i = 0; i < paginator.Pages.Length; i++) {
                WikiPageButton pageButton = paginator.Pages[i];
                if (!pageButton.gameObject.activeSelf) {
                    break;
                }

                pageButton.Widget.SetToggleState(pageButton.Widget.GetVariantValue().AsInt() == selectedId, updateFlags);
            }
        }

        /// <summary>
        /// Updates the paginator's scroll buttons based on current scroll position.
        /// </summary>
        static public void UpdatePaginatorScrollButtons(WikiPaginator paginator, WikiContent content, int tabId, int scrollOffset) {
            if (tabId < 0) {
                paginator.PageScrollLeft.Interactable = paginator.PageScrollRight.Interactable = false;
            } else {
                int totalPagesAvailable = content.TabPages[tabId].Count;
                int pageIconsAvailable = paginator.Pages.Length;
                paginator.PageScrollLeft.Interactable = scrollOffset > 0;
                paginator.PageScrollRight.Interactable = scrollOffset < (totalPagesAvailable - pageIconsAvailable);
            }
        }
    }
}