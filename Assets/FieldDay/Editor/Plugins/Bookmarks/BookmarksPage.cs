using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bookmarking {
    /// <summary>
    /// Page of asset bookmarks.
    /// </summary>
    [CreateAssetMenu(menuName = "Bookmarks Page", order = 5)]
    public sealed class BookmarksPage : ScriptableObject {
        #region Help Text

        private const string ContextsListHelpString = @"List of contexts, separated by commas, semicolons, or line breaks.";
        private const string ContextsFormatHelpString = @"Contexts are of one of several formats.
<b>""user-""</b> represents a specific user, based on the operating system's reported user name.
<b>""scene-""</b> represents a specific open scene, based on the scene name.
<b>""role-""</b> represents a specific developer role, based on the project setup.";

        private const string ShowContextHelpString = ContextsListHelpString + @"

If set, this page will default to being hidden unless any of the given contexts are present.

" + ContextsFormatHelpString;
        private const string HideContextHelpString = ContextsListHelpString + @"

If set, this page will always be hidden if any of the given contexts are present.
<i>Note that this takes precedence over the ShowDuringContexts field.</i>

" + ContextsFormatHelpString;

        #endregion // Help Text

        [Header("Ordering")]
        [Tooltip("Pages with lower values are displayed before higher values.")] public int SortOrder = 0;
        [Tooltip("If checked, cannot be edited directly from the Bookmarks window")] public bool IsLocked;

        [Header("Contexts")]
        [Tooltip(ShowContextHelpString)] [TextArea(1, 2)] public string ShowDuringContexts;
        [Tooltip(HideContextHelpString)] [TextArea(1, 2)] public string HideDuringContexts;

        [Header("Contents")]
        [Tooltip("Displays before the list of items. Try to keep it short.")] [TextArea(2, 16)] public string ReadMe;
        public BookmarkItem[] Items = Array.Empty<BookmarkItem>();
        [Tooltip("If set, all results from a project search with the given filter will be appended to the page")] public string ProjectSearchFilter;

        [NonSerialized] internal UnityEngine.Object[] FilteredObjects;
        [NonSerialized] internal string CachedName;
        [NonSerialized] internal string CachedGuid;
        [NonSerialized] internal bool IsHidden;
    }

    /// <summary>
    /// Asset bookmark.
    /// </summary>
    [Serializable]
    public struct BookmarkItem {
        [Tooltip("Scenes will be opened, other objects will be opened for editing.")] public UnityEngine.Object Object;
        [Tooltip("Will open in the default browser.")] public string URL;
        [Tooltip("Will open a menu item")] public string MenuPath;
        [Tooltip("If set, the item will displayed with this name instead.")] public string CustomName;
    }

    /// <summary>
    /// Utility functions for dealing with asset bookmarks.
    /// </summary>
    static public partial class BookmarksUtility {
        #region Page Contents

        /// <summary>
        /// Returns if the given page contains a reference to the given asset
        /// </summary>
        static public bool ContainsReference(BookmarksPage page, UnityEngine.Object obj) {
            return IndexOfReference(page, obj) >= 0;
        }

        static private int IndexOfReference(BookmarksPage page, UnityEngine.Object obj) {
            for (int i = 0; i < page.Items.Length; i++) {
                if (page.Items[i].Object == obj) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Adds a reference to the given asset to the given page.
        /// Duplicates are not allowed on the same page.
        /// Returns if it was successfully added.
        /// </summary>
        static public bool AddReference(BookmarksPage page, UnityEngine.Object obj, string customName = null) {
            if (IndexOfReference(page, obj) >= 0) {
                return false;
            }

            if (!EditorUtility.IsPersistent(obj)) {
                Debug.LogErrorFormat("[BookmarksUtility] Object '{0}' is not persistent, and cannot be added to a bookmark page", obj.name);
                return false;
            }

            Undo.RecordObject(page, "Adding reference");
            ArrayUtility.Add(ref page.Items, new BookmarkItem() {
                Object = obj,
                CustomName = customName
            });
            EditorUtility.SetDirty(page);
            return true;
        }

        /// <summary>
        /// Removes a reference to the given asset from the given page.
        /// Returns if it was successfully removed.
        /// </summary>
        static public bool RemoveReference(BookmarksPage page, UnityEngine.Object obj) {
            int index = IndexOfReference(page, obj);
            if (index >= 0) {
                Undo.RecordObject(page, "Removing reference");
                ArrayUtility.RemoveAt(ref page.Items, index);
                EditorUtility.SetDirty(page);
                return true;
            }

            return false;
        }

        #endregion // Page Contents

        #region Page Find

        /// <summary>
        /// Retrieves a sorted array of all BookmarksPage assets.
        /// </summary>
        static public BookmarksPage[] LoadAllPages() {
            string[] guids = AssetDatabase.FindAssets("t:BookmarksPage");
            BookmarksPage[] pages = new BookmarksPage[guids.Length];
            for (int i = 0; i < guids.Length; i++) {
                pages[i] = (BookmarksPage)GetInstanceFromGUID(guids[i]);
            }
            Array.Sort(pages, (a, b) => {
                if (a.SortOrder != b.SortOrder) {
                    return a.SortOrder.CompareTo(b.SortOrder);
                }
                return a.name.CompareTo(b.name);
            });

            foreach (var page in pages) {
                page.CachedName = ObjectNames.NicifyVariableName(page.name);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(page, out page.CachedGuid, out long _);

                if (!string.IsNullOrEmpty(page.ProjectSearchFilter)) {
                    page.FilteredObjects = FilterAssets(page.ProjectSearchFilter);
                } else {
                    page.FilteredObjects = null;
                }
            }
            return pages;
        }

        static private UnityEngine.Object[] FilterAssets(string filter) {
            string[] guids = AssetDatabase.FindAssets(filter);
            UnityEngine.Object[] objects = new UnityEngine.Object[guids.Length];
            for (int i = 0; i < guids.Length; i++) {
                objects[i] = GetInstanceFromGUID(guids[i]);
            }
            return objects;
        }

        static private UnityEngine.Object GetInstanceFromGUID(string guid) {
            if (s_CachedLoadMainAsset == null) {
                s_CachedLoadMainAsset = (GetObjectFromGUIDDelegate)typeof(AssetDatabase).GetMethod("LoadMainAssetAtGUID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.CreateDelegate(typeof(GetObjectFromGUIDDelegate));
            }
            GUID guidStruct = new GUID(guid);
            return s_CachedLoadMainAsset(guidStruct);

        }

        private delegate UnityEngine.Object GetObjectFromGUIDDelegate(GUID guid);
        static private GetObjectFromGUIDDelegate s_CachedLoadMainAsset;

        #endregion // Page Find
    }
}