using BeauUtil;
using ScriptableBake;
using System;
using UnityEngine;

namespace FieldDay.Scenes {
    /// <summary>
    /// Changes this object's sibling order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChangeSiblingOrder : MonoBehaviour, IBaked {

        public const int Order = FlattenHierarchy.Order - 10;

        public int SiblingIndex;

        #region IBaked

#if UNITY_EDITOR

        int IBaked.Order {
            get { return Order; }
        }

        bool IBaked.Bake(BakeFlags flags, BakeContext context) {
            Baking.UnpackPrefabIfNecessary(transform);

            transform.SetSiblingIndex(SiblingIndex);
            Baking.Destroy(this, true);
            return true;
        }

#endif // UNITY_EDITOR

        #endregion // IBaked
    }
}