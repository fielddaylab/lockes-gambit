using FieldDay;
using FieldDay.Assets;
using UnityEngine;

namespace SpaceFab.Supply {
    [CreateAssetMenu(menuName = "SpaceFab/Supply/Ship Asset")]
    public sealed class SupplyShipAsset : NamedAsset {
        public string DisplayName;

        [Header("Display")]
        public Sprite BodyImage;
        public Sprite BodyOutline;

        [Header("Icon")]
        public Sprite Icon;
        public Color32 IconColor;

        [Header("Colors")]
        public ColorPalette2 Colors;
        public Color32 LineColor;

        [Header("Stats")]
        [Range(0, 2)] public int Speed;
        [Range(1, 3)] public int Capacity;
    }

    public struct SupplyShipStats {
        public int Speed;
        public int Capacity;
    }
}