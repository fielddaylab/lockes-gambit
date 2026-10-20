using FieldDay.Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Materials
{
    public enum MaterialType
    {
        Silicon,
        Boron,
        Phosphorus,
        Copper,
        SiliconCarbide,
        GalliumNitride,
        GalliumArsenide,
        Tungsten,
        Magnesium,
        Diamond
    }

    [CreateAssetMenu(menuName = "SpaceFab/Game Material Asset")]
    public class MaterialAsset : NamedAsset
    {
        public string DisplayName;
        public string ShortName;

        public Sprite GemSprite;

        public string[] ConstituentElementNames;
        public int[] ValenceElectronCounts;
        public int[] AtomicRadii;
        public int[] SimpleAtomicRadii;

        public MaterialPropertyLabel[] Properties;
        public MaterialAsset[] Contexts;
    }
}
