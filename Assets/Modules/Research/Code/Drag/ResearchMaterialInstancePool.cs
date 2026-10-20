using FieldDay;
using FieldDay.SharedState;
using SpaceFab.Materials;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Object pool for ResearchMaterialInstance. Pre-allocates InitialPoolSize
    /// instances under PoolRoot on register, hands them out on drag start, and
    /// reclaims them on drag end. Expands on demand if a drag is allocated
    /// while the free list is empty; never shrinks during a session.
    /// </summary>
    public class ResearchMaterialInstancePool : SharedStateComponent, IRegistrationCallbacks {
        public GameObject InstancePrefab;
        public Transform PoolRoot;
        public int InitialPoolSize = 8;

        [NonSerialized] public Stack<ResearchMaterialDragInstance> Free;
        [NonSerialized] public List<ResearchMaterialDragInstance> Active;

        public void OnRegister() {
            Free = new Stack<ResearchMaterialDragInstance>(InitialPoolSize);
            Active = new List<ResearchMaterialDragInstance>(InitialPoolSize);
            for (int i = 0; i < InitialPoolSize; i++) {
                ResearchMaterialDragInstance instance = ResearchMaterialInstanceUtility.Instantiate(this);
                if (instance != null) {
                    Free.Push(instance);
                }
            }
        }

        public void OnDeregister() {
            if (Active != null) {
                for (int i = 0; i < Active.Count; i++) {
                    if (Active[i] != null) {
                        UnityEngine.Object.Destroy(Active[i].gameObject);
                    }
                }
                Active.Clear();
            }
            if (Free != null) {
                while (Free.Count > 0) {
                    ResearchMaterialDragInstance instance = Free.Pop();
                    if (instance != null) {
                        UnityEngine.Object.Destroy(instance.gameObject);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Allocation / release operations for ResearchMaterialInstancePool.
    /// Allocate pops the free list (or spawns a new instance if exhausted),
    /// configures Material + OriginSource + rig visuals, activates the
    /// GameObject, and tracks it on the active list. Release reverses all of
    /// that and parks the instance back under PoolRoot.
    /// </summary>
    public static class ResearchMaterialInstanceUtility {
        // Allocates an instance carrying the given material. originSource may
        // be null when the lift came from a slot rather than the tray.
        // Returns null only if the pool is misconfigured (no prefab / no root).
        public static ResearchMaterialDragInstance Allocate(ResearchMaterialInstancePool pool, MaterialAsset material, ResearchMaterialSource originSource) {
            if (pool == null || pool.InstancePrefab == null || pool.PoolRoot == null) {
                Debug.LogWarning("[ResearchMaterialInstancePool] Pool is not configured; cannot allocate.");
                return null;
            }

            ResearchMaterialDragInstance instance = pool.Free.Count > 0 ? pool.Free.Pop() : Instantiate(pool);
            if (instance == null) {
                return null;
            }

            instance.Material = material;
            instance.OriginSource = originSource;
            // Drag-instance labels are hidden in the prefab — pass null
            // for researchState since the known/unknown distinction only
            // affects the label.
            Find.State(out ChamberInterfacerState interfacerState);
            ChamberInterfacerUtility.GetActiveChamber(interfacerState);

            bool atomicView = interfacerState.ActiveChamber == ActiveChamberKind.Doping &&
                ChamberInterfacerUtility.GetCurrent(interfacerState, ChamberSlotKind.Primary) != null;

            if (!atomicView) {
                ResearchMaterialVisualRigUtility.ApplyPropertiesToRig(instance.Rig, material, null);
                MaterialAtomicViewUtility.Clear(instance.AtomicView);
                MaterialAtomicViewUtility.Clear(instance.PolyelementalAtomicView.MaterialAtoms[0]);
                MaterialAtomicViewUtility.Clear(instance.PolyelementalAtomicView.MaterialAtoms[1]);
            }
            else {
                Find.State(out ResearchMinigameState researchState);

                if (material.ConstituentElementNames.Length == 0) {
                    MaterialAtomicViewUtility.RenderMaterialAtom(instance.AtomicView, material, researchState);
                }
                else {
                    MaterialAtomicViewUtility.RenderMaterialAtom(instance.PolyelementalAtomicView.MaterialAtoms[0], material, researchState, 0);
                    MaterialAtomicViewUtility.RenderMaterialAtom(instance.PolyelementalAtomicView.MaterialAtoms[1], material, researchState, 1);
                }
            }
            instance.gameObject.SetActive(true);
            pool.Active.Add(instance);
            return instance;
        }

        // Returns an instance to the pool. Clears state, hides the GameObject,
        // re-parents under PoolRoot. Safe to call on an instance that has
        // already been released (no-op if not on the active list).
        public static void Release(ResearchMaterialInstancePool pool, ResearchMaterialDragInstance instance) {
            if (pool == null || instance == null) return;

            int idx = pool.Active.IndexOf(instance);
            if (idx < 0) return;

            int last = pool.Active.Count - 1;
            if (idx != last) {
                pool.Active[idx] = pool.Active[last];
            }
            pool.Active.RemoveAt(last);

            instance.Material = null;
            instance.OriginSource = null;
            ResearchMaterialVisualRigUtility.ClearRig(instance.Rig);
            MaterialAtomicViewUtility.Clear(instance.AtomicView);
            MaterialAtomicViewUtility.Clear(instance.PolyelementalAtomicView.MaterialAtoms[0]);
            MaterialAtomicViewUtility.Clear(instance.PolyelementalAtomicView.MaterialAtoms[1]);

            instance.transform.SetParent(pool.PoolRoot, false);
            instance.gameObject.SetActive(false);
            pool.Free.Push(instance);
        }

        // Spawns a new instance under PoolRoot, deactivated. Internal: used
        // by both initial fill and on-demand expansion.
        internal static ResearchMaterialDragInstance Instantiate(ResearchMaterialInstancePool pool) {
            GameObject go = UnityEngine.Object.Instantiate(pool.InstancePrefab, pool.PoolRoot);
            go.SetActive(false);
            ResearchMaterialDragInstance instance = go.GetComponent<ResearchMaterialDragInstance>();
            if (instance == null) {
                Debug.LogWarning("[ResearchMaterialInstancePool] InstancePrefab has no ResearchMaterialInstance component.", go);
                UnityEngine.Object.Destroy(go);
                return null;
            }
            return instance;
        }
    }
}
