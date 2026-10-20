using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bookmarking {
    /// <summary>
    /// Asset bookmark role.
    /// </summary>
    [CreateAssetMenu(menuName = "Bookmarks Role", order = 6)]
    public sealed class BookmarksRole : ScriptableObject {
        public int SortOrder = 0;

        [NonSerialized] internal string CachedName;
    }

    static public partial class BookmarksUtility {
        #region Role Find

        /// <summary>
        /// Retrieves a sorted array of all BookmarksRole assets.
        /// </summary>
        static public BookmarksRole[] LoadAllRoles() {
            string[] guids = AssetDatabase.FindAssets("t:BookmarksRole");
            BookmarksRole[] roles = new BookmarksRole[guids.Length];
            for (int i = 0; i < guids.Length; i++) {
                roles[i] = (BookmarksRole)GetInstanceFromGUID(guids[i]);
            }
            Array.Sort(roles, (a, b) => {
                if (a.SortOrder != b.SortOrder) {
                    return a.SortOrder.CompareTo(b.SortOrder);
                }
                return a.name.CompareTo(b.name);
            });

            foreach (var role in roles) {
                role.CachedName = ObjectNames.NicifyVariableName(role.name);
            }
            return roles;
        }

        #endregion // Role Find
    }
}