using BeauRoutine;
using FieldDay.Components;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab
{
    public class LoadIcon : BatchedComponent
    {
        public CanvasGroup LoadGroup;
        public TMP_Text LoadingText;
        public LayoutOffset LoadingTextOffset;

        [Header("Flavor Text")]
        public TMP_Text FlavorText;
        public CanvasGroup FlavorTextFader;
        public LayoutOffset FlavorTextOffset;

        [Header("Flavor Image")]
        public Image FlavorImage;
        public DitherFadeGroup FlavorImageFader;

        [Header("Config")]
        public Sprite DefaultFlavorImage;
    }
}