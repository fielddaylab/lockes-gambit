using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bookmarking {
    public sealed class BookmarksWindow : EditorWindow {
        #region Serialized 

        [SerializeField] private List<string> m_ExpandedGuids = new List<string>();

        [SerializeField] private BookmarksPage[] m_Pages;
        [SerializeField] private BookmarksRole[] m_Roles;
        [SerializeField] private Vector2 m_Scroll;
        [SerializeField] private bool m_HorizontalView;
        [SerializeField] private bool m_ShowHidden;
        [SerializeField] private string m_CurrentRole = null;

        #endregion // Serialized

        #region State

        [NonSerialized] private bool m_PagesDirty = true;
        [NonSerialized] private bool m_ContextsDirty = true;

        [NonSerialized] private HashSet<string> m_CurrentContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion // State

        #region GUI Objects

        [NonSerialized] private GUIStyle m_ButtonStyle;
        [NonSerialized] private GUIStyle m_ReadmeStyle;
        [NonSerialized] private GUIStyle m_PageFoldoutStyle;

        [NonSerialized] private string[] m_RoleNames;

        [NonSerialized] static private GUIContent s_CachedContent;

        private void InitializeResources() {
            if (s_CachedContent == null) {
                s_CachedContent = new GUIContent();
            }

            if (m_ButtonStyle == null) {
                m_ButtonStyle = new GUIStyle(EditorStyles.miniButtonLeft);
                m_ButtonStyle.imagePosition = ImagePosition.ImageLeft;
                m_ButtonStyle.alignment = TextAnchor.MiddleLeft;
                m_ButtonStyle.fixedHeight = 20;
            }

            if (m_ReadmeStyle == null) {
                m_ReadmeStyle = new GUIStyle(EditorStyles.miniLabel);
                m_ReadmeStyle.alignment = TextAnchor.MiddleLeft;
                m_ReadmeStyle.wordWrap = true;

            }
            if (m_PageFoldoutStyle == null) {
                m_PageFoldoutStyle = new GUIStyle(EditorStyles.foldout);
                m_PageFoldoutStyle.fixedHeight = 20;
                m_PageFoldoutStyle.alignment = TextAnchor.MiddleLeft;
                m_PageFoldoutStyle.imagePosition = ImagePosition.ImageLeft;
            }
        }

        #endregion // GUI Objects

        #region Events

        private void OnEnable() {
            titleContent = new GUIContent("Bookmarks", LoadIcon("d_Favorite"));
            minSize = new Vector2(300, 200);

            UpdateRoleList();
            UpdatePageList();

            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.sceneOpened += OnSceneOpened;

            m_ContextsDirty = true;
        }

        private void OnDisable() {
            m_Pages = null;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        private void Update() {
            if (ProcessChanges()) {
                Repaint();
            }
        }

        #endregion // Events

        #region Data

        private void UpdateRoleList() {
            m_Roles = BookmarksUtility.LoadAllRoles();

            m_RoleNames = new string[m_Roles.Length];
            for(int i = 0; i < m_Roles.Length; i++) {
                m_RoleNames[i] = m_Roles[i].CachedName;
            }

            Console.WriteLine("[BookmarksWindow] Updated roles list");
            m_ContextsDirty = true;
        }

        private void UpdatePageList() {
            m_Pages = BookmarksUtility.LoadAllPages();
            Console.WriteLine("[BookmarksWindow] Updated bookmarks list");
            m_ContextsDirty = true;
        }

        private void UpdateContexts() {
            m_CurrentContexts.Clear();

            int sceneCount = SceneManager.loadedSceneCount;
            for(int i = 0; i < sceneCount; i++) {
                var scene = SceneManager.GetSceneAt(i);
                string contextName = "scene-" + scene.name;
                m_CurrentContexts.Add(contextName);
            }

            try {
                string userName = Environment.UserName;
                if (!string.IsNullOrEmpty(userName)) {
                    m_CurrentContexts.Add("user-" + userName);
                }
            } catch {
            }

            if (!string.IsNullOrEmpty(m_CurrentRole)) {
                m_CurrentContexts.Add("role-" + m_CurrentRole);
            }

            Console.WriteLine("[BookmarksWindow] Found {0} contexts", m_CurrentContexts.Count);
            foreach(var context in m_CurrentContexts) {
                Console.WriteLine(context);
            }

            foreach(var page in m_Pages) {
                bool forceHide = false;
                bool show = true;
                if (!string.IsNullOrEmpty(page.ShowDuringContexts)) {
                    show = false;
                    foreach(var context in ExtractContexts(page.ShowDuringContexts)) {
                        if (m_CurrentContexts.Contains(context)) {
                            show = true;
                            break;
                        }
                    }
                }
                if (!string.IsNullOrEmpty(page.HideDuringContexts)) {
                    forceHide = false;
                    foreach (var context in ExtractContexts(page.HideDuringContexts)) {
                        if (m_CurrentContexts.Contains(context)) {
                            forceHide = true;
                            break;
                        }
                    }
                }

                page.IsHidden = forceHide || !show;
            }

            Console.WriteLine("[BookmarksWindow] Updated project contexts");
        }

        private bool ProcessChanges() {
            bool changed = false;

            if (m_PagesDirty) {
                UpdateRoleList();
                UpdatePageList();
                m_PagesDirty = false;
                changed = true;
            }

            if (m_ContextsDirty) {
                UpdateContexts();
                m_ContextsDirty = false;
                changed = true;
            }

            return changed;
        }

        #endregion // Data

        #region Rendering

        private void OnGUI() {
            EventType evtType = Event.current.type;

            InitializeResources();
            ProcessChanges();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar); {
                if (GUILayout.Button(IconContent("d_Refresh", "Refresh page list"), EditorStyles.toolbarButton)) {
                    m_PagesDirty = true;
                }

                GUIContent layoutChangeContent;
                if (m_HorizontalView) {
                    layoutChangeContent = IconContent("d_align_horizontally", "Items are aligned horizontally.");
                } else {
                    layoutChangeContent = IconContent("d_align_vertically", "Items are aligned vertically.");
                }
                if (GUILayout.Button(layoutChangeContent, EditorStyles.toolbarButton)) {
                    Undo.RecordObject(this, "changing horizontal");
                    m_HorizontalView = !m_HorizontalView;
                    EditorUtility.SetDirty(this);
                }

                GUIContent hideContent;
                if (m_ShowHidden) {
                    hideContent = IconContent("d_scenevis_visible_hover", "All pages are shown, even ones normally hidden by the current context");
                } else {
                    hideContent = IconContent("d_scenevis_hidden", "Some pages may be hidden based on the current context");
                }
                if (GUILayout.Button(hideContent, EditorStyles.toolbarButton)) {
                    Undo.RecordObject(this, "changing visibility");
                    m_ShowHidden= !m_ShowHidden;
                    EditorUtility.SetDirty(this);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();

            if (m_Pages.Length <= 0) {
                EditorGUILayout.HelpBox("No shortcut assets are in the project", MessageType.Info);
                return;
            }

            Vector2 newScroll = EditorGUILayout.BeginScrollView(m_Scroll);
            if (m_Scroll != newScroll) {
                Undo.RecordObject(this, "scrolling");
                m_Scroll = newScroll;
                EditorUtility.SetDirty(this);
            }

            EditorGUIUtility.SetIconSize(new Vector2(16, 16));

            int order = m_Pages[0].SortOrder;

            foreach(var page in m_Pages) {
                if (m_ShowHidden || !page.IsHidden) {
                    if (page.SortOrder >= order + 10) {
                        GUILayout.Space(2);
                        Rect lineRect = EditorGUILayout.GetControlRect(false, 2);
                        lineRect.height = 2;
                        EditorGUI.DrawRect(lineRect, Color.grey);
                        GUILayout.Space(2);
                    }
                    order = page.SortOrder;
                    RenderPage(page, evtType);
                }
            }

            EditorGUIUtility.SetIconSize(default);

            EditorGUILayout.EndScrollView();

            if (m_Roles.Length > 0) {
                Rect lineRect = EditorGUILayout.GetControlRect(false, 2);
                lineRect.height = 2;
                EditorGUI.DrawRect(lineRect, Color.grey);
                GUILayout.Space(2);
                int currentIndex = Array.IndexOf(m_RoleNames, m_CurrentRole);
                int popupIndex = EditorGUILayout.Popup("Role", currentIndex, m_RoleNames);
                if (popupIndex != currentIndex) {
                    m_CurrentRole = m_RoleNames[popupIndex];
                    m_ContextsDirty = true;
                }
            }
        }

        private void RenderPage(BookmarksPage page, EventType evtType) {
            bool isExpanded = m_ExpandedGuids.Contains(page.CachedGuid);
            GUIContent headerContent = s_CachedContent;
            int contentCount = page.Items.Length;
            if (page.FilteredObjects != null) {
                contentCount += page.FilteredObjects.Length;
            }
            headerContent.text = string.Format("{0} ({1})", page.CachedName, contentCount);
            if (page.IsHidden) {
                headerContent.image = LoadIcon("d_scenevis_hidden");
            } else if (page.IsLocked) {
                headerContent.image = LoadIcon("d_AssemblyLock");
            } else {
                headerContent.image = LoadIcon("d_ListView");
            }
            headerContent.tooltip = null;

            bool newExpended = EditorGUILayout.Foldout(isExpanded, headerContent, true, m_PageFoldoutStyle);
            if (newExpended != isExpanded) {
                Undo.RecordObject(this, "change expand");
                if (newExpended) {
                    m_ExpandedGuids.Add(page.CachedGuid);
                } else {
                    m_ExpandedGuids.Remove(page.CachedGuid);
                }
                EditorUtility.SetDirty(this);
            }

            // Jump
            Rect headerRect = GUILayoutUtility.GetLastRect();
            if (evtType == EventType.ContextClick) {
                if (headerRect.Contains(Event.current.mousePosition)) {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Jump to Page Definition"), false, () => {
                        Selection.activeObject = page;
                        EditorGUIUtility.PingObject(page);
                        AssetDatabase.OpenAsset(page);
                    });
                    menu.ShowAsContext();
                }
            }

            if (newExpended) {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (!string.IsNullOrEmpty(page.ReadMe)) {
                    GUILayout.Box(page.ReadMe, m_ReadmeStyle);
                    if (page.Items.Length > 0) {
                        EditorGUILayout.Space();
                    }
                }

                bool hasContent = contentCount > 0;

                if (hasContent) {
                    if (m_HorizontalView) {
                        EditorGUILayout.BeginHorizontal();
                    }
                }

                bool canLoadScenes = !EditorApplication.isPlayingOrWillChangePlaymode;

                for(int i = 0; i < page.Items.Length; i++) {
                    RenderItem(page, page.Items[i], evtType, canLoadScenes, false);
                }

                if (page.FilteredObjects != null) {
                    for(int i = 0; i < page.FilteredObjects.Length; i++) {
                        RenderItem(page, new BookmarkItem() {
                            Object = page.FilteredObjects[i]
                        }, evtType, canLoadScenes, true);
                    }
                }

                if (hasContent) {
                    if (m_HorizontalView) {
                        EditorGUILayout.EndHorizontal();
                    }
                }

                if (!hasContent) {
                    EditorGUILayout.LabelField("Drag and drop assets here", EditorStyles.miniLabel);
                }

                EditorGUILayout.EndVertical();

                Rect boxRect = GUILayoutUtility.GetLastRect();

                if (!page.IsLocked) {
                    if (evtType == EventType.DragUpdated || evtType == EventType.DragPerform) {
                        if (boxRect.Contains(Event.current.mousePosition)) {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            if (evtType == EventType.DragPerform) {
                                DragAndDrop.AcceptDrag();
                                bool displayedNotPersistentNotification = false;
                                foreach(var obj in DragAndDrop.objectReferences) {
                                    if (!EditorUtility.IsPersistent(obj)) {
                                        if (!displayedNotPersistentNotification) {
                                            displayedNotPersistentNotification = true;
                                            ShowNotification(new GUIContent("Cannot add non-persistent objects to a Bookmark page!"), 1.0);
                                        }
                                    }
                                    BookmarksUtility.AddReference(page, obj);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RenderItem(BookmarksPage page, BookmarkItem item, EventType evtType, bool canLoadScenes, bool isTemporaryItem) {
            ItemType type = GetItemType(item);

            if (type == ItemType.None) {
                return;
            }

            GUIContent content = s_CachedContent;
            string buttonName = item.CustomName;
            if (string.IsNullOrEmpty(buttonName)) {
                switch (type) {
                    case ItemType.Asset:
                    case ItemType.Scene:
                        buttonName = item.Object.name;
                        break;
                    case ItemType.Link:
                        buttonName = item.URL;
                        break;
                    case ItemType.MenuItem:
                        buttonName = item.MenuPath;
                        break;
                }
            }

            content.text = buttonName;

            switch (type) {
                case ItemType.Asset: {
                    content.image = EditorGUIUtility.ObjectContent(item.Object, item.Object.GetType()).image;
                    content.tooltip = "Open asset";
                    break;
                }
                case ItemType.Scene: {
                    if (Event.current.shift) {
                        content.image = LoadIcon("d_CreateAddNew");
                        content.tooltip = "Load scene (additive)";
                    } else {
                        content.image = LoadIcon("d_Scene");
                        content.tooltip = "Load scene (exclusive)";
                    }
                    break;
                }
                case ItemType.Link: {
                    content.image = LoadIcon("d_Linked");
                    content.tooltip = "Open URL in default browser";
                    break;
                }
                case ItemType.MenuItem: {
                    content.image = LoadIcon("d__Menu");
                    content.tooltip = "Open menu item";
                    break;
                }
            }

            bool wasGUIEnabled = GUI.enabled;
            GUI.enabled = type != ItemType.Scene || canLoadScenes;

            bool isContextClick = evtType == EventType.ContextClick || (evtType == EventType.MouseUp && (Event.current.button == 1 || Event.current.control));

            bool clicked = false;
            if (evtType == EventType.MouseDrag) {
                GUILayout.Box(content, m_ButtonStyle);
                Rect buttonRect = GUILayoutUtility.GetLastRect();
                bool isMouseInBounds = buttonRect.Contains(Event.current.mousePosition);
                if (isMouseInBounds) {
                    if (evtType == EventType.MouseDrag && (type == ItemType.Scene || type == ItemType.Asset) && Event.current.delta.magnitude > 8) {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] { item.Object };
                        DragAndDrop.StartDrag("Bookmark");
                    }
                }
            } else {
                clicked = GUILayout.Button(content, m_ButtonStyle);
            }

            if (clicked) {
                if (isContextClick) {
                    GenericMenu menu = new GenericMenu();
                    if (type == ItemType.Asset || type == ItemType.Scene) {
                        menu.AddItem(new GUIContent("Select asset"), false, () => {
                            Selection.activeObject = item.Object;
                            EditorGUIUtility.PingObject(item.Object);
                        });
                    }
                    if (type == ItemType.Link) {
                        menu.AddItem(new GUIContent("Copy link"), false, () => {
                            EditorGUIUtility.systemCopyBuffer = item.URL;
                        });
                    }
                    if (!page.IsLocked && !isTemporaryItem) {
                        menu.AddItem(new GUIContent("Remove from page?"), false, () => {
                            BookmarksUtility.RemoveReference(page, item.Object);
                        });
                    } else {
                        menu.AddDisabledItem(new GUIContent("Cannot remove from locked page"), false);
                    }
                    menu.ShowAsContext();
                } else {
                    switch (type) {
                        case ItemType.Link: {
                            Application.OpenURL(item.URL);
                            break;
                        }
                        case ItemType.MenuItem: {
                            EditorApplication.ExecuteMenuItem(item.MenuPath);
                            break;
                        }
                        case ItemType.Asset: {
                            if (Event.current.shift) {
                                Selection.activeObject = item.Object;
                                EditorGUIUtility.PingObject(item.Object);
                            }
                            AssetDatabase.OpenAsset(item.Object);
                            break;
                        }
                        case ItemType.Scene: {
                            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                string path = AssetDatabase.GetAssetOrScenePath(item.Object);
                                if (Event.current.shift) {
                                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                                } else {
                                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                                }
                            }
                            break;
                        }
                    }
                }
            }

            GUI.enabled = wasGUIEnabled;
        }

        static private ItemType GetItemType(BookmarkItem item) {
            if (!string.IsNullOrEmpty(item.URL)) {
                return ItemType.Link;
            } else if (!string.IsNullOrEmpty(item.MenuPath)) {
                return ItemType.MenuItem;
            } else if (item.Object is SceneAsset) {
                return ItemType.Scene;
            } else if (item.Object != null) {
                return ItemType.Asset;
            } else {
                return ItemType.None;
            }
        }

        private enum ItemType {
            None,
            Scene,
            Asset,
            Link,
            MenuItem
        }

        #endregion // Rendering

        #region Helpers

        static private GUIContent IconContent(string iconName, string tooltip = null) {
            if (s_CachedContent == null) {
                s_CachedContent = new GUIContent();
            }

            s_CachedContent.text = null;
            s_CachedContent.tooltip = tooltip;
            s_CachedContent.image = LoadIcon(iconName);
            return s_CachedContent;
        }

        static private Texture2D LoadIcon(string iconName) {
            return EditorGUIUtility.LoadRequired(iconName) as Texture2D;
        }

        static private readonly char[] ContextListSplitChars = new char[] { ';', ',', '\r', '\n' };

        static private IEnumerable<string> ExtractContexts(string contextList) {
            if (!string.IsNullOrEmpty(contextList)) {
                foreach (var contextString in contextList.Split(ContextListSplitChars, StringSplitOptions.RemoveEmptyEntries)) {
                    yield return contextString.Trim();
                }
            }
        }

        #endregion // Helpers

        #region Handlers

        private void OnSceneClosed(Scene scene) {
            m_ContextsDirty = true;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) {
            m_ContextsDirty = true;
        }

        #endregion // Handlers

        #region Shortcut

        [MenuItem("Window/Bookmarks %'", priority = 1270)]
        static private void OpenWindow() {
            if (!EditorWindow.HasOpenInstances<BookmarksWindow>()) {
                EditorWindow.GetWindow<BookmarksWindow>().Show();
            } else {
                EditorWindow.GetWindow<BookmarksWindow>().Focus();
            }
        }

        #endregion // Shortcut

        #region Initialization

        private const string DisplayedInitialPageSentinelFilePath = "Library/HasDisplayedProjectShortcutsPrompt.txt";
        private const string InitialPageHeader = "Bookmarks";
        private const string InitialPagePrompt = "The \"Bookmarks\" tab is available. Would you like to open it?";

        [InitializeOnLoadMethod]
        static private void QueueInitialPrompt() {
            EditorApplication.delayCall += TryPresentInitialPrompt;
        }

        static private void TryPresentInitialPrompt() {
            if (!File.Exists(DisplayedInitialPageSentinelFilePath)) {
                File.WriteAllText(DisplayedInitialPageSentinelFilePath, " ");
                if (EditorWindow.HasOpenInstances<BookmarksWindow>()) {
                    return;
                }
                if (EditorUtility.DisplayDialog(InitialPageHeader, InitialPagePrompt, "Yes", "No")) {
                    OpenWindow();
                }
            }
        }

        #endregion // Initialization
    }
}