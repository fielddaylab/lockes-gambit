using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Animation;
using FieldDay.Debugging;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using FieldDay.UI;
using Leaf.Runtime;
using SpaceFab.Materials;
using UnityEngine;

namespace SpaceFab.UI {
    [Flags]
    public enum WikiViewDirtyFlags : byte {
        TabList = 0x01,
        TabSelection = 0x02,

        PageList = 0x04,
        PageSelection = 0x08,
        
        PageContent = 0x10,
        PageChips = 0x20,

        All = TabList | TabSelection | PageList | PageSelection | PageContent | PageChips
    }

    public class WikiViewState : SharedStateComponent, IRegistrationCallbacks, IScenePreload {
        [NonSerialized] public bool Expanded;

        [NonSerialized] public int CurrentTabId = -1;
        [NonSerialized] public int CurrentPageId = -1;
        [NonSerialized] public int CurrentPageScroll = 0;

        [NonSerialized] public WikiTabMemoryRecord[] TabMemory = new WikiTabMemoryRecord[WikiContentUtility.MaxTabs];

        [NonSerialized] public int QueuedTabId = -1;
        [NonSerialized] public int QueuedPageId = -1;
        [NonSerialized] public int QueuedPageScrollDirection = 0;
        [NonSerialized] public int QueuedPageScrollRestore = -1;
        [NonSerialized] public bool QueuedExpanded;
        [NonSerialized] public WikiViewDirtyFlags DirtyFlags;

        [NonSerialized] public StringHash32 QueuedTabByName;
        [NonSerialized] public StringHash32 QueuedPageByName;

        [NonSerialized] public StringHash32 AnnouncedTabName;
        [NonSerialized] public StringHash32 AnnouncedPageName;

        public void OnRegister() {
            WikiUtility.WipeTabMemory(this);
            Game.Scenes.OnMainSceneLateEnable.Register(OnSceneLateEnable);
            Game.Scenes.OnMainSceneUnloading.Register(OnSceneUnload);
        }

        public void OnDeregister() {
            Game.Scenes.OnMainSceneLateEnable.Deregister(OnSceneLateEnable);
            Game.Scenes.OnMainSceneUnloading.Deregister(OnSceneUnload);
        }

        static private void OnSceneUnload() {
            Find.State(out WikiViewState state, out WikiContent content, out WikiLayoutState layout);
            WikiUtility.ClearAll(state, content, layout);
        }

        IEnumerator<WorkSlicer.Result?> IScenePreload.Preload() {
            OnSceneUnload();
            return null;
        }

        static private void OnSceneLateEnable() {
            Find.State(out WikiViewState state, out WikiContent content, out WikiLayoutState layout);
            Game.SharedState.TryGet(out GlobalUISceneConfig config);

            WikiUtility.LoadContent(state, content, layout, config.WikiTabs);
        }
    }

    public struct WikiTabMemoryRecord {
        public sbyte Scroll;
        public sbyte PageId;
    }

    static public partial class WikiUtility {
        #region Invalidation

        static public void Invalidate(WikiViewState state, WikiViewDirtyFlags domains) {
            state.DirtyFlags |= domains;
        }

        static public void InvalidateContent(WikiContent content) {
            content.ContentListsDirty = true;
        }

        #endregion // Invalidation

        #region Loading

        static public void LoadContent(WikiViewState state, WikiContent content, WikiLayoutState layout, WikiTabData[] tabs) {
            content.Tabs = tabs ?? Array.Empty<WikiTabData>();
            content.QueuedContentUpdated = default;
            content.ContentListsDirty = true;
            WikiContentUtility.SyncContextWithScene(content);
            WipeTabMemory(state);

            state.QueuedPageId = -1;
            state.QueuedTabId = -1;
            state.QueuedPageByName = default;
            state.QueuedTabByName = default;
            state.QueuedPageScrollDirection = 0;
            state.QueuedPageScrollRestore = -1;
            state.DirtyFlags = WikiViewDirtyFlags.TabList;
            state.CurrentTabId = -1;
            state.CurrentPageId = -1;
            state.Expanded = false;
            state.QueuedExpanded = false;
            WikiLayoutUtility.SnapExpandedState(layout, false);
        }

        static public void ClearAll(WikiViewState state, WikiContent content, WikiLayoutState layout) {
            WikiContentUtility.ClearContexts(content);
            content.Tabs = Array.Empty<WikiTabData>();

            content.AvailableTabs = default;
            WikiLayoutUtility.PopulateTabs(layout.Tabinator, content);

            WikiLayoutUtility.PopulatePages(layout.Paginator, content, -1, 0);
            WikiLayoutUtility.UpdatePaginatorScrollButtons(layout.Paginator, content, -1, 0);

            WikiLayoutUtility.WipePageContentCompletely(layout.PageLayout);

            WipeTabMemory(state);
            ClearScriptAnnouncements(state);
            content.ResearchContext = default;

            state.Expanded = false;
            state.QueuedExpanded = false;
            WikiLayoutUtility.SnapExpandedState(layout, false);
        }

        #endregion // Loading

        #region Tab Memory

        static public void WipeTabMemory(WikiViewState state) {
            for (int i = 0; i < state.TabMemory.Length; i++) {
                state.TabMemory[i] = new WikiTabMemoryRecord() {
                    PageId = -1,
                    Scroll = 0
                };
            }
        }

        #endregion // Tab Memory

        #region Scripting

        /// <summary>
        /// Announces to scripts any changes to the current tab or page.
        /// </summary>
        static public void FlushScriptAnnouncements(WikiViewState state, WikiContent content) {
            StringHash32 currentTabId = default;
            StringHash32 currentPageName = default;
            if (state.CurrentTabId >= 0) {
                currentTabId = content.Tabs[state.CurrentTabId].AssetId;
                if (state.CurrentPageId >= 0) {
                    currentPageName = content.Tabs[state.CurrentTabId].Pages[state.CurrentPageId].AssetId;
                }
            }

            if (state.AnnouncedTabName != currentTabId) {
                state.AnnouncedTabName = currentTabId;

                if (!currentTabId.IsEmpty) {
                    using (TempVarTable table = TempVarTable.Alloc()) {
                        table.Set("tabId", currentTabId);
                        ScriptUtility.Trigger(ScriptTriggers.OnWikiTabOpened, table);
                    }
                }
            }

            if (state.AnnouncedPageName != currentPageName) {
                state.AnnouncedPageName = currentPageName;

                if (!currentPageName.IsEmpty) {
                    using (TempVarTable table = TempVarTable.Alloc()) {
                        table.Set("pageId", currentPageName);
                        ScriptUtility.Trigger(ScriptTriggers.OnWikiPageOpened, table);
                    }
                }
            }
        }

        static public void ClearScriptAnnouncements(WikiViewState state) {
            state.AnnouncedTabName = default;
            state.AnnouncedPageName = default;
        }

        #endregion // Scripting

        #region Page Locks

        static public bool UnlockPage(PlayerProgressState progressState, StringHash32 pageId) {
            if (!progressState.UnlockedWikiPages.Add(pageId)) {
                return false;
            }

            if (Game.SharedState.TryGet(out WikiContent content)) {
                content.ContentListsDirty = true;
            }

            SpacefabGame.Events.Dispatch(GameEvents.WikiPageUnlocked, pageId);
            return true;
        }

        static public bool LockPage(PlayerProgressState progressState, StringHash32 pageId) {
            if (!progressState.UnlockedWikiPages.Remove(pageId)) {
                return false;
            }

            if (Game.SharedState.TryGet(out WikiContent content)) {
                content.ContentListsDirty = true;
            }

            return true;
        }

        [LeafMember("UnlockWikiPage")]
        static private void Leaf_UnlockPage(StringHash32 pageId) {
            Find.State(out PlayerProgressState progressState);
            UnlockPage(progressState, pageId);
        }

        [LeafMember("LockWikiPage")]
        static private void Leaf_LockPage(StringHash32 pageId) {
            Find.State(out PlayerProgressState progressState);
            UnlockPage(progressState, pageId);
        }

        #endregion // Page Locks

        #region Requests

        static public void Open(WikiViewState state) {
            state.QueuedExpanded = true;
        }

        static public void Close(WikiViewState state) {
            state.QueuedExpanded = false;
        }

        static public void ToggleOpen(WikiViewState state) {
            if (state.Expanded) {
                Close(state);
            } else {
                Open(state);
            }
        }

        static public void ChangeSelection(WikiViewState state, StringHash32 tabId, StringHash32 pageId) {
            state.QueuedTabByName = tabId;
            state.QueuedPageByName = pageId;
            state.QueuedTabId = -1;
            state.QueuedPageId = -1;
        }

        static public void ChangeSelection(WikiViewState state, WikiPageAddress pageAddress) {
            state.QueuedTabByName = default;
            state.QueuedPageByName = default;
            state.QueuedTabId = pageAddress.TabId;
            state.QueuedPageId = pageAddress.PageId;
        }

        static public void ChangeSelection(WikiViewState state, int tabId, int pageId = -1) {
            state.QueuedTabByName = default;
            state.QueuedPageByName = default;
            state.QueuedTabId = tabId;
            state.QueuedPageId = pageId;
            Open(state);
        }

        static public void OpenTo(WikiViewState state, StringHash32 tabId, StringHash32 pageId) {
            ChangeSelection(state, tabId, pageId);
            Open(state);
        }

        static public void OpenTo(WikiViewState state, WikiPageAddress pageAddress) {
            ChangeSelection(state, pageAddress);
            Open(state);
        }

        static public void OpenTo(WikiViewState state, int tabId, int pageId = -1) {
            ChangeSelection(state, tabId, pageId);
            Open(state);
        }

        #endregion // Requests
    }
}