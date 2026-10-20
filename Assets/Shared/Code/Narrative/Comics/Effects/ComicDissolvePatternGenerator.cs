#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace SpaceFab.Comic {
    internal class ComicDissolvePatternGenerator : ScriptableWizard {
        public Sprite[] Dots;
        public Material RenderMaterial;
        [Range(256, 1024)] public int Resolution = 512;
    }
}

#endif // UNITY_EDITOR