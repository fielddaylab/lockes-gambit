using FieldDay;
using UnityEngine;

namespace SpaceFab.UI {
    public sealed class HorizontalMeterLayout : MonoBehaviour {
        public RectTransform Root;
        public float OriginalRotation;
        public float AlternateRotation;
        public float PositionOffset;
        public LayoutOptions Layout;

        [ContextMenu("Apply Layout")]
        public void Apply() {
            using (var children = Positioning.QueryLayoutChildren(Root)) {
                Positioning.HorizontalLayout(children, Layout, PositionOffset);
                for(int i = 0; i < children.Count; i++) {
                    children[i].localEulerAngles = new Vector3(0, 0, (i % 2) == 0 ? OriginalRotation : AlternateRotation);
                }
            }
        }
    }
}