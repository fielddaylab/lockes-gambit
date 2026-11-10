using FieldDay.Components;
using FieldDay.UI.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    /// <summary>
    /// One ship's breakdown row in the expanded progress meter: ship icon + name plus the
    /// ship's own Risk / Cost / Time, each shown as a static section icon followed by a
    /// numeric count (e.g. [risk icon] 3). Pure ref-holder; filled by
    /// SupplyProgressMeterUtility. Rows are authored on the prefab (one per ship slot) and
    /// toggled active by the utility.
    /// </summary>
    public class SupplyShipBreakdownRow : GuiWidget {
        public RectMask2D Mask;
        public LayoutOffset Slide;
        public float SlideDistance;
        public GuiCounter Cost;
        public GuiCounter Time;
        public GuiCounter Risk;
        public TMP_Text CostText;
        public TMP_Text TimeText;
        public TMP_Text RiskText;
    }
}
