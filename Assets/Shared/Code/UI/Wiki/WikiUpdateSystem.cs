using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Debugging;
using FieldDay.Scripting;
using FieldDay.Systems;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SpaceFab.UI {
    public class WikiUpdateSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 600),
                new SysPermissions()
                    .ReadWriteShared<WikiViewState>()
                    .ReadWriteShared<WikiContent>()
                    .ReadWriteShared<WikiLayoutState>()
                    .ReadShared<PlayerProgressState>()
            );
        }

        static private unsafe void ProcessWork(float deltaTime) {
            Find.State(out WikiViewState state, out WikiContent content, out WikiLayoutState layout, out PlayerProgressState playerProgress);

            WikiContentUtility.FlushContentChanges(content, playerProgress);
            ResolveQueuedNameRequests(state, content);
            RespondToContentUpdates(state, content);

            // default to first available tab
            if (state.CurrentTabId < 0 && content.AvailableTabs.Count > 0) {
                state.QueuedTabId = content.AvailableTabs.Indices[0];
            }

            ResolvedQueuedNavigation(state, content, layout);

            bool updatedVisibility = UpdateViewVisibility(state, layout);
            RepaintDirtyLayout(state, content, layout, !updatedVisibility);

            if (state.Expanded) {
                WikiUtility.FlushScriptAnnouncements(state, content);
            }
        }

        /// <summary>
        /// Resolves any queued tab/page changes that were specified by name.
        /// </summary>
        static private void ResolveQueuedNameRequests(WikiViewState state, WikiContent content) {
            if (!state.QueuedTabByName.IsEmpty) {
                StringHash32 tabName = state.QueuedTabByName;
                state.QueuedTabByName = default;

                int tabId = WikiContentUtility.LookupTabId(content, tabName);

                if (tabId < 0 || !content.AvailableTabs.Mask.IsSet(tabId)) {
                    Log.Warn("[WikiUpdateSystem] Tab '{0}' requested but not available!", tabName);
                    state.QueuedPageByName = default;
                } else {
                    if (!state.QueuedPageByName.IsEmpty) {
                        StringHash32 pageName = state.QueuedPageByName;
                        state.QueuedPageByName = default;

                        int pageId = WikiContentUtility.LookupPageIndex(content, tabId, pageName);
                        if (pageId < 0 || !content.TabPages[tabId].Mask.IsSet(pageId)) {
                            Log.Warn("[WikiUpdateSystem] Page '{0}' requested but not available in tab {1}!", pageName, tabName);
                        } else {
                            state.QueuedTabId = tabId;
                            state.QueuedPageId = pageId;
                        }
                    } else {
                        state.QueuedTabId = tabId;
                    }
                }
            }

            if (!state.QueuedPageByName.IsEmpty) {
                StringHash32 pageName = state.QueuedPageByName;
                state.QueuedPageByName = default;

                WikiPageAddress pageAddress = WikiContentUtility.LookupPageAddress(content, state.QueuedPageByName);
                if (pageAddress.TabId < 0 || !content.AvailableTabs.Mask.IsSet(pageAddress.TabId)) {
                    Log.Warn("[WikiUpdateSystem] Page '{0}' requested but not available!", pageName);
                } else if (pageAddress.PageId < 0 || !content.TabPages[pageAddress.TabId].Mask.IsSet(pageAddress.PageId)) {
                    Log.Warn("[WikiUpdateSystem] Page '{0}' requested but not available!", pageName);
                } else {
                    state.QueuedTabId = pageAddress.TabId;
                    state.QueuedPageId = pageAddress.PageId;
                }
            }
        }

        /// <summary>
        /// Changes tabs and pages if necessary in response to updated content.
        /// </summary>
        static private unsafe void RespondToContentUpdates(WikiViewState state, WikiContent content) {
            if (!content.QueuedContentUpdated.AvailableTabsUpdated.IsEmpty) {
                WikiUtility.Invalidate(state, WikiViewDirtyFlags.TabList);
                content.QueuedContentUpdated.AvailableTabsUpdated = default;
                if (state.QueuedTabId < 0 && state.QueuedPageId < 0 && state.CurrentTabId >= 0) {
                    WikiContentList tabList = content.AvailableTabs;
                    if (!tabList.Mask.IsSet(state.CurrentTabId)) {
                        Assert.True(tabList.Count > 0, "No tabs remaining!");
                        state.QueuedTabId = tabList.Indices[0];
                    }
                }
            }

            if (!content.QueuedContentUpdated.PageListsUpdated.IsEmpty) {
                if (state.CurrentTabId >= 0 && content.QueuedContentUpdated.PageListsUpdated.IsSet(state.CurrentTabId)) {
                    WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageList);
                    // if we don't have a queued tab/page shift, check for page going missing
                    if (state.QueuedTabId < 0 && state.QueuedPageId < 0 && state.CurrentTabId >= 0) {
                        WikiContentList pageList = content.TabPages[state.CurrentTabId];
                        if (!pageList.Mask.IsSet(state.CurrentPageId)) {
                            state.QueuedPageId = FindFirstAvailablePage(pageList, state.CurrentPageId);
                        }
                    }
                }
                content.QueuedContentUpdated.PageListsUpdated = default;
            }
        }

        /// <summary>
        /// Resolves all queued tab, scroll, and page changes.
        /// </summary>
        static private unsafe void ResolvedQueuedNavigation(WikiViewState state, WikiContent content, WikiLayoutState layout) {
            PageSeekMode seekMode = PageSeekMode.NotSeeking;

            if (state.QueuedTabId >= 0) {
                seekMode = state.QueuedPageId >= 0 ? PageSeekMode.SpecificTabAndPage : PageSeekMode.ChangeTab;
            } else if (state.QueuedPageId >= 0) {
                seekMode = PageSeekMode.ChangePage;
            }

            // tab selection changes
            if (state.QueuedTabId >= 0) {
                Log.Msg("[WikiUpdateUtility] Processing tab change {0}, frame {1}", state.QueuedTabId, Frame.Index);
                int tabIndex = state.QueuedTabId;
                state.QueuedTabId = -1;

                state.QueuedPageScrollDirection = 0;
                if (SwapTabs(state, content, layout, tabIndex, ref seekMode)) {
                    WikiUtility.Invalidate(state, WikiViewDirtyFlags.TabSelection | WikiViewDirtyFlags.PageList);
                }
            }

            // process page scroll first in case page needs to change
            if (state.QueuedPageScrollDirection != 0 && state.QueuedPageId < 0) {
                Assert.True(state.CurrentTabId >= 0, "No tab selected!");
                Log.Msg("[WikiUpdateUtility] Processing page scroll {0}, frame {1}", state.QueuedPageScrollDirection, Frame.Index);
                int scrollDirection = state.QueuedPageScrollDirection;
                state.QueuedPageScrollDirection = 0;
                WikiContentList pageList = content.TabPages[state.CurrentTabId];
                int pageCount = pageList.Count;
                int windowSize = layout.Paginator.Pages.Length;
                int nextScroll = ClampScroll(state.CurrentPageScroll + scrollDirection * 4, pageCount, windowSize);
                if (state.CurrentPageScroll != nextScroll) {
                    state.CurrentPageScroll = nextScroll;
                    WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageList);
                    int currentVisualIndex = WikiContentUtility.GetVisualIndex(pageList, state.CurrentPageId);
                    if (currentVisualIndex < nextScroll) {
                        state.QueuedPageId = pageList.Indices[nextScroll];
                        seekMode = PageSeekMode.ScrollInduced;
                    } else if (currentVisualIndex >= nextScroll + windowSize) {
                        state.QueuedPageId = pageList.Indices[nextScroll + windowSize - 1];
                        seekMode = PageSeekMode.ScrollInduced;
                    } else {
                        Sfx.Play("Wiki.PageScroll");
                    }
                }
            }

            // page selection changes
            if (state.QueuedPageId >= 0) {
                Log.Msg("[WikiUpdateUtility] Processing page change {0}, frame {1}", state.QueuedPageId, Frame.Index);
                int pageId = state.QueuedPageId;
                int queuedScroll = state.QueuedPageScrollRestore;
                state.QueuedPageId = -1;
                state.QueuedPageScrollRestore = -1;

                state.QueuedPageScrollDirection = 0;
                int currentScroll = state.CurrentPageScroll;
                bool pageSwap = SwapPages(state, content, layout, pageId, queuedScroll, seekMode);
                if (pageSwap) {
                    WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageContent | WikiViewDirtyFlags.PageSelection);
                }
                if (currentScroll != state.CurrentPageScroll) {
                    WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageList);
                }
            }
        }

        /// <summary>
        /// Responds to visibility change requests.
        /// </summary>
        static private bool UpdateViewVisibility(WikiViewState state, WikiLayoutState layout) {
            if (state.Expanded == state.QueuedExpanded) {
                return false;
            }

            state.Expanded = state.QueuedExpanded;
            // TODO: animate
            if (!state.Expanded) {
                WikiUtility.ClearScriptAnnouncements(state);
                ScriptUtility.Trigger(ScriptTriggers.OnWikiClosed);
                Sfx.Play("Wiki.Close");
            } else {
                ScriptUtility.Trigger(ScriptTriggers.OnWikiOpened);
                WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageContent);
                Sfx.Play("Wiki.Open");
            }
            WikiLayoutUtility.SnapExpandedState(layout, state.Expanded);
            return true;
        }

        /// <summary>
        /// Repaints all dirty layout domains.
        /// </summary>
        static private void RepaintDirtyLayout(WikiViewState state, WikiContent content, WikiLayoutState layout, bool canPlayAudio) {
            if (!state.Expanded) {
                return;
            }
            
            bool tabsChangedInstant = false;
            if ((state.DirtyFlags & WikiViewDirtyFlags.TabList) != 0) {
                WikiLayoutUtility.PopulateTabs(layout.Tabinator, content);
                state.DirtyFlags &= ~WikiViewDirtyFlags.TabList;
                WikiUtility.Invalidate(state, WikiViewDirtyFlags.TabSelection);
                tabsChangedInstant = true;
            }

            if ((state.DirtyFlags & WikiViewDirtyFlags.TabSelection) != 0) {
                WikiLayoutUtility.UpdateSelectedTab(layout.Tabinator, state.CurrentTabId, tabsChangedInstant);
                if (state.CurrentTabId >= 0) {
                    layout.Header.SetText(content.Tabs[state.CurrentTabId].Title);
                } else {
                    layout.Header.SetText("---");
                }
                state.DirtyFlags &= ~WikiViewDirtyFlags.TabSelection;
                if (canPlayAudio) {
                    Sfx.Play("Wiki.TabOpen");
                    canPlayAudio = false;
                }
            }

            bool pagesChangedInstant = false;
            if ((state.DirtyFlags & WikiViewDirtyFlags.PageList) != 0) {
                WikiLayoutUtility.PopulatePages(layout.Paginator, content, state.CurrentTabId, state.CurrentPageScroll);
                WikiLayoutUtility.UpdatePaginatorScrollButtons(layout.Paginator, content, state.CurrentTabId, state.CurrentPageScroll);
                state.DirtyFlags &= ~WikiViewDirtyFlags.PageList;
                WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageSelection);
                pagesChangedInstant = true;
            }

            if ((state.DirtyFlags & WikiViewDirtyFlags.PageSelection) != 0) {
                WikiLayoutUtility.UpdateSelectedPage(layout.Paginator, state.CurrentPageId, pagesChangedInstant);
                state.DirtyFlags &= ~WikiViewDirtyFlags.PageSelection;
            }

            if ((state.DirtyFlags & WikiViewDirtyFlags.PageContent) != 0) {
                if (state.CurrentTabId >= 0) {
                    WikiLayoutUtility.PopulatePageContent(layout.PageLayout, content.Tabs[state.CurrentTabId].Pages[state.CurrentPageId], content);
                } else {
                    WikiLayoutUtility.ClearPageContent(layout.PageLayout);
                }
                state.DirtyFlags &= ~WikiViewDirtyFlags.PageContent;
                WikiUtility.Invalidate(state, WikiViewDirtyFlags.PageChips);
                if (canPlayAudio) {
                    Sfx.Play("Wiki.PageOpen");
                }
            }

            if ((state.DirtyFlags & WikiViewDirtyFlags.PageChips) != 0) {
                WikiLayoutUtility.UpdateMaterialPageData(layout.PageLayout, content);
                WikiLayoutUtility.UpdateObservationPageData(layout.PageLayout, content);
                WikiLayoutUtility.UpdatePropertyPageData(layout.PageLayout, content);
                state.DirtyFlags &= ~WikiViewDirtyFlags.PageChips;
            }
        }

        #region Swaps

        static private bool SwapTabs(WikiViewState state, WikiContent content, WikiLayoutState layout, int tabIndex, ref PageSeekMode seekMode) {
            Assert.True(tabIndex >= 0, "Tab index out of bounds");

            if (tabIndex == state.CurrentTabId) {
                // if we'd otherwise be seeking a specific page, but we're seeking within the same tab,
                // downgrade to page change
                if (seekMode == PageSeekMode.SpecificTabAndPage) {
                    seekMode = PageSeekMode.ChangePage;
                }
                return false;
            }

            if (!content.AvailableTabs.Mask.IsSet(tabIndex)) {
                Log.Warn("[WikiUpdateState] Tab '{0}' is not available right now", content.Tabs[tabIndex].AssetId);
                return false;
            }

            int prevTabIndex = state.CurrentTabId;
            if (prevTabIndex >= 0) {
                ref WikiTabMemoryRecord oldMemoryRecord = ref state.TabMemory[prevTabIndex];
                oldMemoryRecord.PageId = (sbyte) state.CurrentPageId;
                oldMemoryRecord.Scroll = (sbyte)state.CurrentPageScroll;
            }

            state.CurrentTabId = tabIndex;

            WikiContentList tabContentList = content.TabPages[tabIndex];
            Assert.True(tabContentList.Count > 0, "Tab has no pages!");
            WikiTabMemoryRecord tabMemoryRecord = state.TabMemory[tabIndex];

            // restore from tab memory
            if (seekMode == PageSeekMode.ChangeTab) {
                state.QueuedPageId = FindFirstAvailablePage(tabContentList, tabMemoryRecord.PageId);
                state.QueuedPageScrollRestore = tabMemoryRecord.Scroll;
            }

            state.CurrentPageId = -1;

            return true;
        }

        static private bool SwapPages(WikiViewState state, WikiContent content, WikiLayoutState layout, int pageIndex, int desiredScroll, PageSeekMode seekMode) {
            Assert.True(pageIndex >= 0, "Page index out of bounds");

            int tabIndex = state.CurrentTabId;

            WikiContentList tabContentList = content.TabPages[tabIndex];
            Assert.True(tabContentList.Count > 0, "Tab has no pages!");

            int correctedPageIndex = FindFirstAvailablePage(tabContentList, pageIndex);
            int visualIndex = WikiContentUtility.GetVisualIndex(tabContentList, correctedPageIndex);
            int scroll = desiredScroll;
            if (seekMode == PageSeekMode.SpecificTabAndPage) {
                scroll = FindCenteredScroll(visualIndex, tabContentList.Count, layout.Paginator.Pages.Length);
            } else if (scroll < 0) {
                scroll = state.CurrentPageScroll;
            }

            scroll = AdjustScrollToEnsureInWindow(visualIndex, scroll, tabContentList.Count, layout.Paginator.Pages.Length);
            state.CurrentPageScroll = scroll;

            if (state.CurrentPageId != correctedPageIndex || seekMode == PageSeekMode.ChangeTab) {
                state.CurrentPageId = correctedPageIndex;
                return true;
            }

            return false;
        }

        #endregion // Swaps

        #region Helpers

        static private unsafe int FindFirstAvailablePage(WikiContentList contentList, int targetId) {
            Assert.True(contentList.Count > 0, "Tab has no pages!");

            if (targetId < 0) {
                return contentList.Indices[0];
            }

            if (contentList.Mask.IsSet(targetId)) {
                return targetId;
            }

            // content list is always sorted in ascending order
            // so we don't need to check against the current closest
            int closest = 0;
            for (int i = 0; i < contentList.Count; i++) {
                int index = contentList.Indices[i];
                if (index < targetId) {
                    closest = i;
                } else {
                    break;
                }
            }
            return closest;
        }

        static private int FindCenteredScroll(int visualIndex, int count, int windowSize) {
            int lower = visualIndex - windowSize / 2;
            return ClampScroll(lower, count, windowSize);
        }

        static private int AdjustScrollToEnsureInWindow(int visualIndex, int currentScroll, int count, int windowSize) {
            int lower = currentScroll;
            int upper = currentScroll + windowSize;

            int unbounded;
            if (visualIndex >= upper) {
                unbounded = visualIndex + 1 - windowSize;
            } else if (visualIndex < lower) {
                unbounded = visualIndex;
            } else {
                unbounded = currentScroll;
            }

            return ClampScroll(unbounded, count, windowSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static private int ClampScroll(int scroll, int count, int windowSize) {
            return Math.Max(0, Math.Min(scroll, count - windowSize));
        }

        #endregion // Helpers

        private enum PageSeekMode {
            NotSeeking,
            ChangeTab,
            ChangePage,
            SpecificTabAndPage,
            ScrollInduced
        }
    }
}
