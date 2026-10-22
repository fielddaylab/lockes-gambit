using BeauUtil;
using FieldDay.Assets;
using FieldDay.Collections;
using FieldDay.Components;
using ScriptableBake;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FieldDay.UI {
    [DisallowMultipleComponent]
    public abstract class BaseRaycasterInputLayer : BatchedComponent, IInputLayer, IRegistrationCallbacks {
        #region Inspector

        [SerializeField, InputGroupName] private StringHash32 m_GroupId;
        [SerializeField] private BaseRaycaster[] m_Raycasters;

        #endregion // Inspector

        [NonSerialized] protected InputLayerMask m_Mask;
        [NonSerialized] protected bool m_InputEnabled;
        [NonSerialized] protected bool m_IsPushed;
        [NonSerialized] protected bool m_PushQueued;
        [NonSerialized] protected bool m_IsRegistered;

        protected virtual void Awake() {
            m_Mask.GroupId = m_GroupId;
            m_Mask.LayerMask = (1 << gameObject.layer);

            m_Raycasters = m_Raycasters ?? Array.Empty<BaseRaycaster>();
            if (m_Raycasters.Length > 0) {
                m_InputEnabled = m_Raycasters[0].enabled;
                for(int i = 1; i < m_Raycasters.Length; i++) {
                    m_Raycasters[i].enabled = m_InputEnabled;
                }
            }
        }

        void IRegistrationCallbacks.OnRegister() {
            m_IsRegistered = true;
            Game.Gui.RegisterInputLayer(this);
            if (m_PushQueued && !m_IsPushed) {
                m_IsPushed = true;
                Game.Gui.PushPriority(this);
            }
        }

        void IRegistrationCallbacks.OnDeregister() {
            if (m_IsPushed) {
                Game.Gui.PopPriority(this);
                m_IsPushed = false;
            }
            Game.Gui.DeregisterInputLayer(this);
            m_IsRegistered = false;
        }

        public InputLayerMask InputMask {
            get { return m_Mask; }
            set { m_Mask = value; }
        }

        public bool IsInputEnabled() {
            return m_InputEnabled;
        }

        public bool TryPushPriority() {
            if (!m_IsPushed) {
                m_IsPushed = true;
                m_PushQueued = true;
                if (m_IsRegistered) {
                    Game.Gui.PushPriority(this);
                }
                return true;
            }

            return false;
        }

        public bool TryPopPriority() {
            if (m_IsPushed) {
                m_IsPushed = false;
                m_PushQueued = false;
                if (m_IsRegistered) {
                    Game.Gui.PopPriority(this);
                }
                return true;
            }

            return false;
        }

        void IInputLayer.UpdateInputEnabled(bool enabled) {
            if (m_InputEnabled != enabled) {
                m_InputEnabled = enabled;
                for (int i = m_Raycasters.Length; i-- > 0;) {
                    m_Raycasters[i].enabled = enabled;
                }
            }
        }

#if UNITY_EDITOR
        static private BaseRaycaster[] GatherRaycasters(GameObject root, IInputLayer ignore) {
            using(TempReferenceBuffer<BaseRaycaster> raycasterBuff = TempReferenceBuffer<BaseRaycaster>.Create()) {
                DeepTraverse(raycasterBuff, root, ignore);
                return raycasterBuff.ToArray();
            }
        }

        static private void DeepTraverse(TempReferenceBuffer<BaseRaycaster> buffer, GameObject go, IInputLayer ignore) {
            if (go.TryGetComponent(out IInputLayer inputLayer) && inputLayer != ignore) {
                return;
            }

            if (go.TryGetComponent(out BaseRaycaster raycaster)) {
                buffer.Add(raycaster);
            }

            int childCount = go.transform.childCount;
            int childIndex = 0;
            while(childIndex < childCount) {
                DeepTraverse(buffer, go.transform.GetChild(childIndex++).gameObject, ignore);
            }
        }

        [ContextMenu("Find Child Raycasters")]
        private void GenerateRaycasterList() {
            m_Raycasters = GatherRaycasters(gameObject, this);
            Baking.SetDirty(this);
        }

        private void Reset() {
            GenerateRaycasterList();
        }
#endif // UNITY_EDITOR
    }
}