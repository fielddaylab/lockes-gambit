using BeauUtil;
using FieldDay;
using FieldDay.Components;
using UnityEngine;

namespace SpaceFab.Onboarding {
    /// <summary>
    /// Marks a GameObject as a tutorial-addressable element. The Id is what Leaf calls
    /// reference when summoning a highlight on this element. ElementTag self-registers
    /// with ElementTagLookup on Awake (not on enable) so the highlight system can resolve
    /// id -> component in O(1) even while the element is disabled; it deregisters only on
    /// destroy. One ElementTag may sit on a UI RectTransform host, a world Collider2D host,
    /// or both — the cached references are looked up on Awake so the highlight system
    /// doesn't GetComponent per call. World highlights size off the Collider2D's bounds,
    /// so the designer controls the highlight footprint by shaping the collider rather
    /// than by the sprite's rendered size.
    /// </summary>
    public class ElementTag : BatchedComponent, IRegistrationCallbacks {
        // SerializedHash32 because the id is authored in the inspector and the original
        // source string needs to be recoverable for debug logs / collision messages.
        public SerializedHash32 Id;

        [Header("Targets")]
        // Assign one of these in the inspector. Selection priority when the highlight
        // resolves a target: RectTransform -> SpriteRenderer -> Collider. Any field left
        // null at OnRegister is filled via GetComponent against this GameObject; explicit
        // assignment is preferred for parents that should point at a child target.
        // If all three are null after fallback, HighlightElement on this id logs a warning.
        public RectTransform RectTransform;
        public SpriteRenderer SpriteRenderer;
        public Collider2D Collider;
        public bool ColliderOverride = false;

        // Lookup registration lives on Awake/OnDestroy rather than OnRegister/OnDeregister
        // (which fire on every enable/disable) so a disabled element stays addressable —
        // a highlight can still resolve and act on it while the GameObject is inactive.
        private void Awake() {
            // Cache target references once. Done here rather than in OnRegister because the
            // highlight system reads them as soon as the tag is in the lookup, and lookup
            // registration now happens in Awake, before the first OnEnable.
            if (RectTransform == null) {
                RectTransform = GetComponent<RectTransform>();
            }
            if (SpriteRenderer == null) {
                SpriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (Collider == null) {
                Collider = GetComponent<Collider2D>();
            }

            RegisterCurrentId();
        }

        private void OnDestroy() {
            // Only pull out of the lookup on a genuine destroy. During game shutdown the
            // lookup is being torn down anyway, so skip the work and avoid touching a
            // possibly-deregistered shared state.
            if (!Game.IsShuttingDown) {
                DeregisterCurrentId();
            }
        }

        public void OnRegister() {
        }

        public void OnDeregister() {
        }

        /// <summary>
        /// Reassigns this tag's id at runtime, updating the lookup atomically: deregisters
        /// the old id, swaps in the new one, registers under the new id. Pass default to
        /// clear the tag from the lookup (e.g. when a pooled object is being returned).
        /// </summary>
        public void SetId(StringHash32 newIdHash) {
            StringHash32 currentHash = Id.Hash();
            if (currentHash == newIdHash) { return; }

            DeregisterCurrentId();
            // SerializedHash32 has an implicit operator from StringHash32, so this assignment
            // round-trips the hash without preserving a source string. Source-string recovery
            // is only needed for the inspector-authored case (warnings / debug logs); runtime
            // SetId callers pass hashes whose source string isn't meaningful here.
            Id = newIdHash;
            RegisterCurrentId();
        }

        /// <summary>
        /// Reassigns this tag's id at runtime from a source string, preserving that string on the
        /// SerializedHash32 so the id reads back as text (e.g. "design:input-a") in the inspector
        /// rather than a bare hash. Same atomic lookup swap as SetId(StringHash32). Pass null/empty
        /// to clear. Use this overload whenever the source string is known at the call site.
        /// </summary>
        public void SetId(string newIdSource) {
            if (string.IsNullOrEmpty(newIdSource)) {
                SetId(default(StringHash32));
                return;
            }

            // Construct from the string so the SerializedHash32 retains its source for the
            // inspector. The hash compare against the current id avoids a redundant
            // deregister/register when the id is unchanged.
            SerializedHash32 newId = new SerializedHash32(newIdSource);
            if (Id.Hash() == newId.Hash()) { return; }

            DeregisterCurrentId();
            Id = newId;
            RegisterCurrentId();
        }

        private void RegisterCurrentId() {
            if (Id.IsEmpty) { return; }
            ElementTagLookup lookup = Find.State<ElementTagLookup>();
            if (lookup != null) {
                ElementTagLookupUtility.Register(lookup, this);
            }
        }

        private void DeregisterCurrentId() {
            if (Id.IsEmpty) { return; }
            if (Game.SharedState.Has<ElementTagLookup>()) {
                ElementTagLookup lookup = Find.State<ElementTagLookup>();
                ElementTagLookupUtility.Deregister(lookup, this);
            }
        }
    }
}
