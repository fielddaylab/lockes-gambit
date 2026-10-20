using System;
using UnityEngine;

namespace SpaceFab.Supply {
    /// <summary>
    /// Aggregate "Result" panel view for the Supply mini progress meter. Pure ref-holder:
    /// authored cell arrays for the three sections, filled by SupplyProgressMeterUtility.
    /// The number of cells per section is fixed by what's authored on the prefab; the utility
    /// fills a prefix of each and clears the rest. Reuses ProgressMeterCell (base + overlay).
    /// </summary>
    public class SupplyProgressMeterView : MonoBehaviour {
        public Transform CostCellParent, RiskCellParent, TimeCellParent;
        public RectTransform CostCellHighlight, TimeCellHighlight, TimeCellCap;

        // Summed risk across ships, rendered as filled diamond cells.
        [NonSerialized] public ProgressMeterCell[] RiskCells;
 
        // Contract funds, rendered as remaining (yellow) then spent (red) bars.
        [NonSerialized] public ProgressMeterCell[] CostCells;
        
        // Max time across ships, rendered as filled dot cells.
        [NonSerialized] public ProgressMeterCell[] TimeCells;

        public void Start()
        {
            TimeCells = new ProgressMeterCell[TimeCellParent.childCount];
            CostCells = new ProgressMeterCell[CostCellParent.childCount];
            RiskCells = new ProgressMeterCell[RiskCellParent.childCount];

            Populate(TimeCells, TimeCellParent);
            Populate(CostCells, CostCellParent);
            Populate(RiskCells, RiskCellParent);
        }

        private void Populate(ProgressMeterCell[] cells, Transform parent)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = parent.GetChild(i).GetComponent<ProgressMeterCell>();
            }
        }
    }
}
