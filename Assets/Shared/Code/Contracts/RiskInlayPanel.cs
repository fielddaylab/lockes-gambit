using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceFab.Research;
using FieldDay;
using SpaceFab.UI;
using FieldDay.UI.Widgets;

namespace SpaceFab {
    public class RiskInlayPanel : MonoBehaviour
    {
        public Graphic Background;
        public TMP_Text Title;
        public TMP_Text Description;

        public GuiCounter Meter;

        [Header("Colors")]
        public ColorPalette2 SafePalette;
        public ColorPalette2 RiskyPalette;

        [Header("Content")]
        public string DefaultNoRiskHeader;
        [TextArea(2, 3)] public string DefaultRiskDescription;
        [TextArea(2, 3)] public string DefaultNoRiskDescription;

        public void SetRisk(int risk) {
            Meter.SetValue(risk, GuiWidgetUpdateFlags.Initialization);

            if (risk > 0) {
                Background.color = RiskyPalette.Background;
                Title.color = Description.color = RiskyPalette.Content;
            } else {
                Background.color = SafePalette.Background;
                Title.color = Description.color = SafePalette.Content;
            }
        }

        public void SetText(string header, string description) {
            Title.SetText(header);
            Description.SetText(description);
        }
    }
}
