using System;
using BeauRoutine;
using BeauUtil;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    [RequireComponent(typeof(Mask), typeof(MaskableGraphic))]
    public sealed class DitherFadeGroup : MonoBehaviour {
        [NonSerialized] public Mask Mask;
        [NonSerialized] public MaskableGraphic Graphic;

        public void SetAlpha(float alpha) {
            this.CacheComponent(ref Mask);
            this.CacheComponent(ref Graphic);

            bool enabled = alpha < 1;
            Mask.enabled = Graphic.enabled = enabled;

            Color graphicColor = Graphic.color;
            graphicColor.a = alpha;
            Graphic.color = graphicColor;
        }
    }
}