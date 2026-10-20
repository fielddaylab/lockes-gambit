using BeauUtil;
using FieldDay;
using Leaf.Runtime;
using SpaceFab;
using UnityEngine;

namespace SpaceFab.UI {
    /// <summary>
    /// Leaf-callable commands for the shared wiki UI. Thin wrappers over WikiUtility that resolve
    /// state once and no-op when called outside a scene that hosts the wiki.
    /// </summary>
    public static class WikiScripting {
        // Collapse the wiki to its icon. No-op when no wiki is present in the scene, or when it's
        // already collapsed / mid-transition (WikiUtility.Close handles the latter two).
        [LeafMember("CloseWiki")]
        public static void Leaf_CloseWiki() {
            WikiUtility.Close(Find.State<WikiViewState>());
        }

        // Open (expanding if collapsed) the wiki to a specific tab + page by id. Each id may be the
        // asset name (e.g. "Materials_Tabs", "Diode") or the authored display title (e.g.
        // "Materials") — the resolver tries asset name first, then title. Case-sensitive. Unknown
        // ids are dropped by the resolver. No-op when no wiki is present.
        [LeafMember("OpenWikiTo")]
        public static void Leaf_OpenWikiTo(string tabId, string pageId) {
            WikiUtility.OpenTo(Find.State<WikiViewState>(), new StringHash32(tabId), new StringHash32(pageId));
        }

        // Select a tab by id (does not expand the wiki — use OpenWikiTo for that). Id is the tab asset
        // name, case-sensitive. Drops the request if the id doesn't match an authored tab. No-op
        // when no wiki is present.
        [LeafMember("SetTabById")]
        public static void Leaf_SetTabById(string tabId) {
            WikiUtility.ChangeSelection(Find.State<WikiViewState>(), new StringHash32(tabId), default);
        }

        // Select a page by id within the active tab (does not expand the wiki — use OpenWikiTo for
        // that). Id is the page asset name, case-sensitive. Drops the request if the id isn't a
        // page in the active tab or the page is locked. No-op when no wiki is present.
        [LeafMember("SetPageById")]
        public static void Leaf_SetPageById(string pageId) {
            WikiUtility.ChangeSelection(Find.State<WikiViewState>(), default, new StringHash32(pageId));
        }

        // Select a tab by id (does not expand the wiki — use OpenWikiTo for that). Id is the tab asset
        // name, case-sensitive. Drops the request if the id doesn't match an authored tab. No-op
        // when no wiki is present.
        [LeafMember("GetTabId")]
        public static StringHash32 Leaf_GetTabId() {
            if (Game.SharedState.TryGet(out WikiViewState state)) {
                Find.State(out WikiContent content);
                if (state.CurrentTabId >= 0) {
                    return content.Tabs[state.CurrentTabId].AssetId;
                }
            }
            return default;
        }
    }
}
