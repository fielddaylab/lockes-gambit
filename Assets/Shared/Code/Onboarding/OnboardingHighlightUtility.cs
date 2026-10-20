using System.Collections.Generic;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using UnityEngine;

namespace SpaceFab.Onboarding {
    /// <summary>
    /// Public command surface for OnboardingHighlightState. Show / Release / ReleaseAll
    /// are the only ways to allocate or return highlights and to flip the focus-lock state.
    /// Focus gating reuses the existing InputUtility raycaster mask: lock-focused targets
    /// (and their descendants) are reparked on LayerMasks.TutorialFocus_Index, then
    /// InputUtility.SetClickableMaskCustom restricts the raycaster's eventMask to that
    /// layer until the last lock is released.
    /// </summary>
    public static class OnboardingHighlightUtility {
        // Show a highlight on the tag with id `id`. lockFocus=true also gates input so only
        // focus-locked elements receive clicks. margin < 0 means "use state.DefaultMargin".
        // attachToCanvas=true groups the highlight under HighlightCanvas instead of beside its
        // target. Idempotent on id — a second Show on an already-active id is a no-op.
        public static void Show(OnboardingHighlightState highlightState, StringHash32 id, bool lockFocus, float margin, bool attachToCanvas, HighlightStyle style) {
            Assert.NotNullOrDestroyed(highlightState);

            if (highlightState.ActiveById.ContainsKey(id)) {
                return;
            }

            ElementTagLookup lookup = Find.State<ElementTagLookup>();
            if (lookup == null || !ElementTagLookupUtility.TryGet(lookup, id, out ElementTag tag)) {
                Debug.LogWarning(string.Format("[Onboarding] HighlightElement: no ElementTag for id '{0}'.", id.ToDebugString()));
                return;
            }

            float resolvedMargin = margin >= 0f ? margin : highlightState.DefaultMargin;

            // attachToCanvas groups the highlight under HighlightCanvas (drawing above its target's
            // siblings) instead of parenting it beside the target. A null canvasRect selects the
            // legacy sibling-parent path; warn if the flag was set but no canvas is assigned.
            RectTransform canvasRect = null;
            if (attachToCanvas) {
                if (highlightState.HighlightCanvas != null) {
                    canvasRect = (RectTransform) highlightState.HighlightCanvas.transform;
                } else {
                    Debug.LogWarning(string.Format("[Onboarding] HighlightElement: attachToCanvas requested for id '{0}' but HighlightCanvas is unassigned; falling back to sibling parenting.", id.ToDebugString()));
                }
            }

            Highlight highlight = highlightState.Pool.Alloc();
            highlight.Bind(tag, resolvedMargin, canvasRect, style);
            highlightState.ActiveById[id] = highlight;

            if (lockFocus) {
                BeginFocusLock(highlightState, id, tag);
            }
        }

        // Release the highlight currently associated with `id`, returning it to the pool.
        // If that highlight held a focus lock, the lock is also released; if no locks
        // remain after, the raycaster's clickable mask is restored to default.
        public static void Release(OnboardingHighlightState highlightState, StringHash32 id) {
            Assert.NotNullOrDestroyed(highlightState);

            if (!highlightState.ActiveById.TryGetValue(id, out Highlight highlight)) {
                return;
            }

            highlight.Release();
            highlightState.Pool.Free(highlight);
            highlightState.ActiveById.Remove(id);

            if (highlightState.LockedTagIds.Remove(id)) {
                ElementTagLookup lookup = Find.State<ElementTagLookup>();
                if (lookup != null && ElementTagLookupUtility.TryGet(lookup, id, out ElementTag tag)) {
                    RestoreLayerRecursive(tag.gameObject, highlightState);
                }

                if (highlightState.LockedTagIds.Count == 0) {
                    RestoreInputMask();
                }
            }
        }

        // Release every active highlight and clear all focus locks. Restores the
        // raycaster mask exactly once at the end if any lock was active.
        public static void ReleaseAll(OnboardingHighlightState highlightState) {
            Assert.NotNullOrDestroyed(highlightState);

            bool hadLocks = highlightState.LockedTagIds.Count > 0;
            ElementTagLookup lookup = Find.State<ElementTagLookup>();

            // Snapshot the keys: Release mutates ActiveById, so we can't enumerate live.
            // (Allocating a temp list here is fine — ReleaseAll is rare.)
            List<StringHash32> ids = new List<StringHash32>(highlightState.ActiveById.Keys);
            for (int i = 0; i < ids.Count; i++) {
                StringHash32 id = ids[i];
                if (!highlightState.ActiveById.TryGetValue(id, out Highlight highlight)) { continue; }

                highlight.Release();
                highlightState.Pool.Free(highlight);

                if (highlightState.LockedTagIds.Contains(id) && lookup != null
                    && ElementTagLookupUtility.TryGet(lookup, id, out ElementTag tag)) {
                    RestoreLayerRecursive(tag.gameObject, highlightState);
                }
            }

            highlightState.ActiveById.Clear();
            highlightState.LockedTagIds.Clear();
            highlightState.SavedLayersByInstanceId.Clear();

            if (hadLocks) {
                RestoreInputMask();
            }
        }

        // ---- Focus lock helpers ----

        // Park the target hierarchy on the TutorialFocus layer and, on the first lock,
        // narrow the raycaster mask so only that layer is clickable. Subsequent locks
        // just add more parked hierarchies under the same mask.
        private static void BeginFocusLock(OnboardingHighlightState highlightState, StringHash32 id, ElementTag tag) {
            if (!highlightState.LockedTagIds.Add(id)) {
                return;
            }

            bool wasFirstLock = highlightState.LockedTagIds.Count == 1;
            ApplyTutorialLayerRecursive(tag.gameObject, highlightState);

            if (wasFirstLock) {
                // TODO: apply lock
                Log.Warn("[OnboardingHighlightUtility] Input mask should be applied - AUTUMN HAS NOT REIMPLEMENTED THIS");
                // LayerMasks.TutorialFocus_Mask
            }
        }

        private static void RestoreInputMask() {
            // TODO: undo lock
            Log.Warn("[OnboardingHighlightUtility] Input mask should be restored - AUTUMN HAS NOT REIMPLEMENTED THIS");
        }

        // Walk root + descendants. For each node we haven't already saved, record the
        // current layer and park the node on TutorialFocus. Skip nodes whose layer is
        // already TutorialFocus — they were placed there by a prior lock or by hand.
        private static void ApplyTutorialLayerRecursive(GameObject root, OnboardingHighlightState highlightState) {
            ApplyTutorialLayerSingle(root, highlightState);
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++) {
                ApplyTutorialLayerRecursive(t.GetChild(i).gameObject, highlightState);
            }
        }

        private static void ApplyTutorialLayerSingle(GameObject go, OnboardingHighlightState highlightState) {
            int currentLayer = go.layer;
            if (currentLayer == LayerMasks.TutorialFocus_Index) {
                return;
            }
            int instanceId = go.GetInstanceID();
            if (highlightState.SavedLayersByInstanceId.ContainsKey(instanceId)) {
                return;
            }
            highlightState.SavedLayersByInstanceId[instanceId] = currentLayer;
            go.layer = LayerMasks.TutorialFocus_Index;
        }

        // Reverse of ApplyTutorialLayerRecursive. For each saved entry under this hierarchy,
        // restore the prior layer. Nodes that were never saved are left alone (defensive
        // against children that were added or reparented after the lock was applied).
        private static void RestoreLayerRecursive(GameObject root, OnboardingHighlightState highlightState) {
            RestoreLayerSingle(root, highlightState);
            Transform t = root.transform;
            for (int i = 0; i < t.childCount; i++) {
                RestoreLayerRecursive(t.GetChild(i).gameObject, highlightState);
            }
        }

        private static void RestoreLayerSingle(GameObject go, OnboardingHighlightState highlightState) {
            int instanceId = go.GetInstanceID();
            if (highlightState.SavedLayersByInstanceId.TryGetValue(instanceId, out int priorLayer)) {
                go.layer = priorLayer;
                highlightState.SavedLayersByInstanceId.Remove(instanceId);
            }
        }
    }
}
