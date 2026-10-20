using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Components;
using FieldDay.Scenes;
using FieldDay.SharedState;
using SpaceFab.Comic;
using SpaceFab.Materials;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SpaceFab.UI {
    /// <summary>
    /// Root BatchedComponent of the shared wiki prefab. Carries the ordered list of tabs the
    /// active minigame exposes. One instance per scene. Consumed by WikiSelectSystem (to
    /// resolve TabId/PageId lookups for OpenTo calls) and WikiVisualsUtility (to render
    /// tab/page content).
    /// </summary>
    public class WikiContent : SharedStateComponent, IRegistrationCallbacks {
        // Number of page thumbnails visible at once in the paginator strip. Scrolling moves the
        // strip one slot at a time, keeping the selected page inside the window.
        public const int PageWindowSize = 5;

        // Runtime-only: the tab set is authored per-minigame on GlobalUISceneConfig.WikiTabs and
        // pushed here by QuickToolbar on scene late enable. Empty until that happens, so a scene
        // without a config simply shows no tabs rather than tripping the strip asserts.
        [NonSerialized] public WikiTabData[] Tabs = Array.Empty<WikiTabData>();

        [NonSerialized] public IWikiContentFilter ContentFilter;
        [NonSerialized] public WikiResearchContext ResearchContext;

        [NonSerialized] public WikiContentList AvailableTabs;
        [NonSerialized] public WikiContentList[] TabPages = new WikiContentList[WikiContentUtility.MaxTabs];

        [NonSerialized] public WikiContentUpdateResult QueuedContentUpdated;
        [NonSerialized] public bool ContentListsDirty;

        public void OnDeregister() {
            ContentFilter = null;
            Tabs = null;
        }

        public void OnRegister() {
            ContentFilter = BaseWikiContentFilter.Instance;
        }
    }

    public struct WikiContentList {
        public BitSet32 Mask;
        public int Count;
        public unsafe fixed sbyte Indices[WikiContentUtility.MaxPages];
    }

    public interface IWikiContentFilter {
        PageAvailabilityOverride GetPageAvailability(PlayerProgressState playerProgress, WikiTabData tabData, WikiPageData pageData);
    }
       
    public enum PageAvailabilityOverride {
        Default,
        AlwaysShow,
        AlwaysHide
    }

    public sealed class BaseWikiContentFilter : IWikiContentFilter {
        static public readonly BaseWikiContentFilter Instance = new BaseWikiContentFilter();

        public PageAvailabilityOverride GetPageAvailability(PlayerProgressState playerProgress, WikiTabData tabData, WikiPageData pageData) {
            return PageAvailabilityOverride.Default;
        }
    }

    public abstract class WikiContentFilterComponent : MonoBehaviour, IWikiContentFilter {
        public virtual PageAvailabilityOverride GetPageAvailability(PlayerProgressState playerProgress, WikiTabData tabData, WikiPageData pageData) {
            return PageAvailabilityOverride.Default;
        }
    }

    static public partial class WikiContentUtility {
        public const int MaxTabs = 4;
        public const int MaxPages = 20;

        #region List Updates

        /// <summary>
        /// Updates the list of available pages for the given tab.
        /// </summary>
        static public unsafe WikiListUpdateResult UpdatePageList(ref WikiContentList pageList, WikiTabData tabData, PlayerProgressState progressState, IWikiContentFilter contentFilter) {
            Assert.True(tabData.Pages.Length <= MaxPages, "Too many pages in tab '{0}'!", tabData.AssetId);

            BitSet32 originalBits = pageList.Mask;

            pageList.Mask.Clear();
            pageList.Count = 0;
            for(int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                if (IsPageAvailable(progressState, tabData, tabData.Pages[pageIndex], contentFilter)) {
                    pageList.Mask.Set(pageIndex);
                    pageList.Indices[pageList.Count++] = (sbyte) pageIndex;
                }
            }

            if (originalBits != pageList.Mask) {
                return WikiListUpdateResult.Changed;
            }
            return WikiListUpdateResult.NoChange;
        }

        static private bool IsPageAvailable(PlayerProgressState progressState, WikiTabData tabData, WikiPageData pageData, IWikiContentFilter contentFilter) {
            Assert.NotNullOrDestroyed(contentFilter);
            switch(GetDefaultPageAvailability(progressState, tabData, pageData)) {
                case PageAvailabilityOverride.AlwaysHide: {
                    return false;
                }
                case PageAvailabilityOverride.AlwaysShow: {
                    return true;
                }
            }
            switch(contentFilter.GetPageAvailability(progressState, tabData, pageData)) {
                case PageAvailabilityOverride.AlwaysHide: {
                    return false;
                }
                case PageAvailabilityOverride.AlwaysShow: {
                    return true;
                }
            }
            return pageData.IsUnlockedByDefault || progressState.UnlockedWikiPages.Contains(pageData.AssetId);
        }

        /// <summary>
        /// Updates the list of available tabs.
        /// </summary>
        static public unsafe BitSet32 UpdateTabList(ref WikiContentList tabList, WikiContentList[] tabPageData, int tabCount) {
            Assert.True(tabCount <= tabPageData.Length && tabCount <= MaxTabs, "Too many tabs!");

            BitSet32 originalBits = tabList.Mask;

            tabList.Mask.Clear();
            tabList.Count = 0;
            for(int tabIndex = 0; tabIndex < tabCount; tabIndex++) {
                if (tabPageData[tabIndex].Count > 0) {
                    tabList.Mask.Set(tabIndex);
                    tabList.Indices[tabList.Count++] = (sbyte)tabIndex;
                }
            }

            return originalBits ^ tabList.Mask;
        }
    
        /// <summary>
        /// Updates the lists of available tabs and pages.
        /// </summary>
        static public WikiContentUpdateResult UpdateAvailableContent(WikiContent content, PlayerProgressState progressState) {
            WikiContentUpdateResult result = default;

            for(int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                if (UpdatePageList(ref content.TabPages[tabIndex], content.Tabs[tabIndex], progressState, content.ContentFilter) == WikiListUpdateResult.Changed) {
                    result.PageListsUpdated.Set(tabIndex);
                }
            }

            result.AvailableTabsUpdated = UpdateTabList(ref content.AvailableTabs, content.TabPages, content.Tabs.Length);
            return result;
        }

        #endregion // List Updates

        #region Indices

        /// <summary>
        /// Returns the id of the given wiki tab.
        /// </summary>
        static public int LookupTabId(WikiContent content, StringHash32 tabName) {
            for (int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                if (tabData.AssetId == tabName) {
                    return tabIndex;
                }
            }

            StringHash32 extendedTabName = tabName.FastConcat("_Tab");
            for (int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                if (tabData.AssetId == extendedTabName) {
                    return tabIndex;
                }
            }

            Log.Error("[WikiUtility] Tab with name '{0}' not found in loaded set of Wiki Content", tabName);
            return -1;
        }

        /// <summary>
        /// Returns the address of the given wiki page within the given tab.
        /// </summary>
        static public int LookupPageIndex(WikiContent content, int tabIndex, StringHash32 pageName) {
            WikiTabData tabData = content.Tabs[tabIndex];
            for (int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                WikiPageData pageData = tabData.Pages[pageIndex];
                if (pageData.AssetId == pageName) {
                    return pageIndex;
                }
            }

            Log.Error("[WikiUtility] Page with name '{0}' not found in loaded set of Wiki Content", pageName);
            return -1;
        }

        /// <summary>
        /// Returns the address of the given wiki page.
        /// </summary>
        static public WikiPageAddress LookupPageAddress(WikiContent content, StringHash32 pageName) {
            for(int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                for (int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                    WikiPageData pageData = tabData.Pages[pageIndex];
                    if (pageData.AssetId == pageName) {
                        return new WikiPageAddress() {
                            TabId = (sbyte) tabIndex,
                            PageId = (sbyte) pageIndex
                        };
                    }
                }
            }

            Log.Error("[WikiUtility] Page with name '{0}' not found in loaded set of Wiki Content", pageName);
            return new WikiPageAddress() {
                PageId = -1,
                TabId = -1
            };
        }

        /// <summary>
        /// Returns the address of the wiki page for the given observation type.
        /// </summary>
        static public WikiPageAddress LookupObservationPageAddress(WikiContent content, ObservationType observationType) {
            for (int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                for (int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                    WikiPageData pageData = tabData.Pages[pageIndex];
                    if (pageData.IsObservationPage && pageData.ObservationType == observationType) {
                        return new WikiPageAddress() {
                            TabId = (sbyte) tabIndex,
                            PageId = (sbyte) pageIndex
                        };
                    }
                }
            }

            Log.Error("[WikiUtility] Page with observation '{0}' not found in loaded set of Wiki Content", observationType);
            return new WikiPageAddress() {
                PageId = -1,
                TabId = -1
            };
        }

        /// <summary>
        /// Returns the address of the wiki page for the given property type.
        /// </summary>
        static public WikiPageAddress LookupPropertyPageAddress(WikiContent content, MaterialPropertyLabel propertyType) {
            for (int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                for (int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                    WikiPageData pageData = tabData.Pages[pageIndex];
                    if (pageData.IsPropertyPage && pageData.PropertyCheck.Label == propertyType) {
                        return new WikiPageAddress() {
                            TabId = (sbyte) tabIndex,
                            PageId = (sbyte) pageIndex
                        };
                    }
                }
            }

            Log.Error("[WikiUtility] Page with property '{0}' not found in loaded set of Wiki Content", propertyType);
            return new WikiPageAddress() {
                PageId = -1,
                TabId = -1
            };
        }

        /// <summary>
        /// Returns the address of the wiki page for the given material id.
        /// </summary>
        static public WikiPageAddress LookupMaterialPageAddress(WikiContent content, StringHash32 materialId) {
            for (int tabIndex = 0; tabIndex < content.Tabs.Length; tabIndex++) {
                WikiTabData tabData = content.Tabs[tabIndex];
                for (int pageIndex = 0; pageIndex < tabData.Pages.Length; pageIndex++) {
                    WikiPageData pageData = tabData.Pages[pageIndex];
                    if (pageData.IsMaterialPage && pageData.MaterialId == materialId) {
                        return new WikiPageAddress() {
                            TabId = (sbyte) tabIndex,
                            PageId = (sbyte) pageIndex
                        };
                    }
                }
            }

            Log.Error("[WikiUtility] Page with material '{0}' not found in loaded set of Wiki Content", materialId);
            return new WikiPageAddress() {
                PageId = -1,
                TabId = -1
            };
        }

        /// <summary>
        /// Returns the visual index of the given page index in the given tab.
        /// </summary>
        static public unsafe int LookupPageVisualIndex(WikiContent content, int tabIndex, int pageIndex) {
            Assert.True(tabIndex >= 0, "Tab index out of range");
            WikiContentList pageList = content.TabPages[tabIndex];
            if (!pageList.Mask.IsSet(pageIndex)) {
                return -1;
            }

            for(int i = 0; i < pageList.Count; i++) {
                if (pageList.Indices[i] == pageIndex) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns the visual index of the given content index in the given list.
        /// </summary>
        static public unsafe int GetVisualIndex(WikiContentList contentList, int index) {
            if (!contentList.Mask.IsSet(index)) {
                return -1;
            }

            for (int i = 0; i < contentList.Count; i++) {
                if (contentList.Indices[i] == index) {
                    return i;
                }
            }
            return -1;
        }

        #endregion // Indices

        #region Flush Changes

        static public unsafe void FlushContentChanges(WikiContent content, PlayerProgressState playerProgress) {
            if (content.ContentListsDirty) {
                WikiContentUpdateResult result = UpdateAvailableContent(content, playerProgress);
                content.QueuedContentUpdated.AvailableTabsUpdated |= result.AvailableTabsUpdated;
                content.QueuedContentUpdated.PageListsUpdated |= result.PageListsUpdated;
                content.ContentListsDirty = false;
            }
        }

        #endregion // Flush Changes

        #region Context

        static public void ClearContexts(WikiContent content) {
            content.ContentFilter = BaseWikiContentFilter.Instance;
            content.ResearchContext = default;
        }

        static public void SyncContextWithScene(WikiContent content) {
            WikiContentFilterComponent filter = Find.Any<WikiContentFilterComponent>();
            content.ContentFilter = filter ? filter : BaseWikiContentFilter.Instance;
            content.ResearchContext = WikiResearchContextUtility.Resolve();
        }

        /// <summary>
        /// Evaluates if a material property has been "retired".
        /// </summary>
        static public bool IsMaterialPropertyExcluded(MaterialPropertyLabel propertyLabel, PlayerProgressState playerProgress) {
            switch(propertyLabel) {
                case MaterialPropertyLabel.ConductorNaive:
                case MaterialPropertyLabel.InsulatorNaive:
                    return playerProgress.ThermalChamberUnlocked;
                case MaterialPropertyLabel.Conductor:
                case MaterialPropertyLabel.Insulator:
                case MaterialPropertyLabel.Semiconductor:
                    return !playerProgress.ThermalChamberUnlocked;

                case MaterialPropertyLabel.DEPRECATED_HeatVulnerable:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Evaluates if an observation/property should be visible in the wiki.
        /// </summary>
        static public bool IsMaterialPropertyVisible(MaterialPropertyLabel propertyLabel, PlayerProgressState playerProgress) {
            switch (propertyLabel) {
                // conductive observations always available
                case MaterialPropertyLabel.Conductive:
                case MaterialPropertyLabel.NonConductive:
                    return true;

                // heat observations only available once thermal chamber is unlocked
                case MaterialPropertyLabel.HeatActivated:
                case MaterialPropertyLabel.HeatDeactivated:
                case MaterialPropertyLabel.HeatUnaffected:
                case MaterialPropertyLabel.HeatResistant: 
                    return playerProgress.ThermalChamberUnlocked;

                // doping observations only available once doping chamber is unlocked
                case MaterialPropertyLabel.AtomicRadiusCompliant:
                case MaterialPropertyLabel.ValenceOneLessThan:
                case MaterialPropertyLabel.ValenceOneMoreThan:
                    return playerProgress.DopingChamberUnlocked;

                // special observations available chapter 9 onward
                case MaterialPropertyLabel.LightEmitting:
                case MaterialPropertyLabel.HighMobility:
                    return playerProgress.SpecialPropertiesUnlocked;

                // voltage resistance only available once big battery unlocked
                case MaterialPropertyLabel.VoltageResistant:
                    return playerProgress.BigBatteryUnlocked;

                // naive properties rely on lack of thermal chamber
                case MaterialPropertyLabel.ConductorNaive:
                case MaterialPropertyLabel.InsulatorNaive:
                    return !playerProgress.ThermalChamberUnlocked;

                // proper electrical properties only available once thermal chamber unlocked
                case MaterialPropertyLabel.Conductor:
                case MaterialPropertyLabel.Insulator:
                case MaterialPropertyLabel.Semiconductor:
                    return playerProgress.ThermalChamberUnlocked;

                // high voltage requires big battery
                case MaterialPropertyLabel.HighVoltageSemiconductor:
                    return playerProgress.BigBatteryUnlocked;

                // high temperature requires thermal chamber
                case MaterialPropertyLabel.HiTempConductor:
                case MaterialPropertyLabel.HiTempSemiConductor:
                    return playerProgress.ThermalChamberUnlocked;

                // doping requires doping chamber
                case MaterialPropertyLabel.PDopantFor:
                case MaterialPropertyLabel.NDopantFor:
                    return playerProgress.DopingChamberUnlocked;

                // special properties available chapter 9 onward
                case MaterialPropertyLabel.LightEmittingSemiconductor:
                case MaterialPropertyLabel.HighMobilitySemiconductor:
                    return playerProgress.SpecialPropertiesUnlocked;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns the current material research progress to be displayed in the wiki.
        /// </summary>
        static public MaterialPropertyRecord GetMaterialRecord(StringHash32 materialId, PlayerProgressState playerProgress, WikiResearchContext researchContext) {
            playerProgress.MaterialProperties.TryGetValue(materialId, out MaterialPropertyRecord record);
            if (researchContext.Present) {
                researchContext.MinigameState.SandboxProperties.TryGetValue(materialId, out MaterialPropertyRecord sandboxRecord);
                MaterialPropertyRecordUtility.Merge(ref record, sandboxRecord);
            }
            return record;
        }

        static public PageAvailabilityOverride GetDefaultPageAvailability(PlayerProgressState playerProgress, WikiTabData tabData, WikiPageData pageData) {
            if (pageData.IsPropertyPage) {
                if (IsMaterialPropertyVisible(pageData.PropertyCheck.Label, playerProgress)) {
                    return PageAvailabilityOverride.AlwaysShow;
                }
                return PageAvailabilityOverride.AlwaysHide;
            }

            if (pageData.IsObservationPage) {
                switch(pageData.ObservationType) {
                    case ObservationType.Electrical:
                        return PageAvailabilityOverride.AlwaysShow;
                    case ObservationType.Thermal:
                        return playerProgress.ThermalChamberUnlocked ? PageAvailabilityOverride.AlwaysShow : PageAvailabilityOverride.AlwaysHide;
                    case ObservationType.Dopant:
                        return playerProgress.ThermalChamberUnlocked ? PageAvailabilityOverride.AlwaysShow : PageAvailabilityOverride.AlwaysHide;
                    case ObservationType.Special:
                        return IsMaterialPropertyVisible(MaterialPropertyLabel.HighMobility, playerProgress)
                            || IsMaterialPropertyVisible(MaterialPropertyLabel.VoltageResistant, playerProgress)
                            ? PageAvailabilityOverride.AlwaysShow : PageAvailabilityOverride.AlwaysHide;
                }
            }

            return PageAvailabilityOverride.Default;
        }

        #endregion // Context
    }

    public struct WikiPageAddress {
        public sbyte TabId;
        public sbyte PageId;
    }

    public enum WikiListUpdateResult {
        NoChange,
        Changed
    }

    public struct WikiContentUpdateResult {
        public BitSet32 AvailableTabsUpdated;
        public BitSet32 PageListsUpdated;
    }
}
