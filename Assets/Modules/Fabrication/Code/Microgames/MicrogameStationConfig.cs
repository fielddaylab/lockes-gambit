using FieldDay.Assets;
using UnityEngine;

namespace SpaceFab.Fabrication
{
    [CreateAssetMenu(menuName = "SpaceFab/Fabrication/Station Config")]
    public class MicrogameStationConfig : NamedAsset
    {
        public Sprite[] PhotolithographyMasks;
        public Sprite[] PhotolithographyOutlines;

        public GameObject[] EtchPatterns;
        public GameObject[] SputterPatterns;
        public GameObject[] IonPatterns;
    }
}