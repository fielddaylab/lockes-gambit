using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Collections;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using SpaceFab.Research;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace SpaceFab {
    public sealed class ContractRequirementTable : MonoBehaviour {
        public ContractRequirementRow[] DefaultRows;
        public ContractRequirementRow ExtendedRow;

        [Header("Layout")]
        public LayoutSizeGroup Sizer;
        public LayoutOptions Spacing = LayoutOptions.Size(4, 1);

        [Header("Assets")]
        public Sprite UnfilledSlotIcon;
        public Sprite FilledSlotIcon;
        public Color32 RowTextDefaultColor;
        public Color32 RowTextFilledColor;
        public Color32 RowBackgroundDefaultColor;
        public Color32 RowBackgroundFilledColor;
        public Color32 RowFaintLineDefaultColor;
        public Color32 RowFaintLineFilledColor;
        public float SubLabelOffsetY = -6;
    }

    static public partial class ContractUIUtility {

        #region Visuals Generation

        static public void InitializeVisuals(in ContractRequirementResolver.RequirementsTable resolverState, ContractRequirementTable table) {
            for (int i = 0; i < table.DefaultRows.Length; i++) {
                table.DefaultRows[i].gameObject.SetActive(false);
            }
            table.ExtendedRow.gameObject.SetActive(false);

            Find.GlobalAsset(out ResearchObservationChipAssets chipAssets);
            
            using (PooledStringBuilder labelBuilder = PooledStringBuilder.Create())
            using (PooledStringBuilder subLabelBuilder = PooledStringBuilder.Create()) {
                for (int rowIndex = 0; rowIndex < resolverState.Rows.Count; rowIndex++) {
                    ContractRequirementResolver.MaterialRow rowData = resolverState.Rows[rowIndex];
                    ContractRequirementRow rowVisuals;

                    if (rowData.RowType == ContractRequirementResolver.RowType.Extended) {
                        rowVisuals = table.ExtendedRow;
                    } else {
                        rowVisuals = table.DefaultRows[rowData.RowIndex];
                    }

                    ContractRequirementSubRow subRowVisuals = rowVisuals.DefaultRow;
                    AssignSubRowCount(subRowVisuals, rowData.ColumnRange.Length);

                    labelBuilder.Builder.Clear();
                    subLabelBuilder.Builder.Clear();

                    ContractRequirementResolver.GenerateLabel(rowData.Properties, labelBuilder, subLabelBuilder, out MaterialPropertyLabel baseProperty, chipAssets, out Sprite icon);
                    AssignSubRowLabel(subRowVisuals, labelBuilder, subLabelBuilder, icon, table.SubLabelOffsetY);

                    int usedDopantRows = 0;
                    if ((rowData.Dopants & ContractRequirementResolver.DopantRows.N) != 0) {
                        Assert.True(rowVisuals.DopantRows.Length > 0);
                        subRowVisuals = rowVisuals.DopantRows[usedDopantRows++];
                        labelBuilder.Builder.Clear();
                        labelBuilder.Builder.Append("+ N-Type Dopant");
                        AssignSubRowCount(subRowVisuals, rowData.ColumnRange.Length);
                        AssignSubRowLabel(subRowVisuals, labelBuilder, null, null, table.SubLabelOffsetY);
                    }
                    if ((rowData.Dopants & ContractRequirementResolver.DopantRows.P) != 0) {
                        Assert.True(rowVisuals.DopantRows.Length > 1);
                        subRowVisuals = rowVisuals.DopantRows[usedDopantRows++];
                        labelBuilder.Builder.Clear();
                        labelBuilder.Builder.Append("+ P-Type Dopant");
                        AssignSubRowCount(subRowVisuals, rowData.ColumnRange.Length);
                        AssignSubRowLabel(subRowVisuals, labelBuilder, null, null, table.SubLabelOffsetY);
                    }

                    for (int dopantRowIndex = 0; dopantRowIndex < rowVisuals.DopantRows.Length; dopantRowIndex++) {
                        subRowVisuals = rowVisuals.DopantRows[dopantRowIndex];
                        bool isVisible = dopantRowIndex < usedDopantRows;
                        subRowVisuals.gameObject.SetActive(isVisible);
                        foreach (var virtualChild in subRowVisuals.VirtualChildren) {
                            virtualChild.SetActive(isVisible);
                        }
                        if (!isVisible) {
                            AssignSubRowCount(subRowVisuals, 0);
                        }
                    }

                    Positioning.SetHeightDelta(rowVisuals.Rect, rowVisuals.Heights[usedDopantRows]);
                    rowVisuals.gameObject.SetActive(true);
                }
            }

            table.Sizer.VerticalLayout(table.Spacing, 0);
        }

        #endregion // Visuals Generation

        #region Updates

        static public void UpdateVisualsWithCompletion(in ContractRequirementResolver.RequirementsTable resolverState, ContractRequirementTable table, Dictionary<StringHash32, MaterialPropertyRecord> recordMap) {
            for (int rowIndex = 0; rowIndex < resolverState.Rows.Count; rowIndex++) {
                ContractRequirementResolver.MaterialRow rowData = resolverState.Rows[rowIndex];
                ContractRequirementRow rowVisuals;
                
                if (rowData.RowType == ContractRequirementResolver.RowType.Extended) {
                    rowVisuals = table.ExtendedRow;
                } else {
                    rowVisuals = table.DefaultRows[rowData.RowIndex];
                }

                rowVisuals.Background.color = table.RowBackgroundDefaultColor;

                bool hasFinishedColumn = false;
                bool hasAnyPrimary = false;
                bool hasAnyN = false;
                bool hasAnyP = false;

                ContractRequirementSubRow rowPrimary = rowVisuals.DefaultRow;
                ContractRequirementSubRow rowN = null;
                ContractRequirementSubRow rowP = null;
                int usedSubRows = 0;

                if ((rowData.Dopants & ContractRequirementResolver.DopantRows.N) != 0) {
                    rowN = rowVisuals.DopantRows[usedSubRows++];
                }
                if ((rowData.Dopants & ContractRequirementResolver.DopantRows.P) != 0) {
                    rowP = rowVisuals.DopantRows[usedSubRows++];
                }

                for (int columnIndex = 0; columnIndex < rowData.ColumnRange.Length; columnIndex++) {
                    ContractRequirementResolver.MaterialColumn columnData = resolverState.Columns[columnIndex + rowData.ColumnRange.Offset];
                    
                    StringHash32 materialId = columnData.MaterialId;
                    bool hasPrimary = ContractRequirementResolver.HasDiscoveredAllProperties(recordMap, materialId, rowData.Properties);

                    rowPrimary.Slots[columnIndex].sprite = hasPrimary ? table.FilledSlotIcon : table.UnfilledSlotIcon;
                    hasAnyPrimary |= hasPrimary;

                    bool hasFullColumn = hasPrimary;
                    bool hasN = true;

                    if ((rowData.Dopants & ContractRequirementResolver.DopantRows.N) != 0) {
                        hasN = hasPrimary && ContractRequirementResolver.HasDiscoveredDopantProperty(recordMap, columnData.DopantN, MaterialPropertyLabel.NDopantFor, materialId);
                        rowN.Slots[columnIndex].sprite = hasN ? table.FilledSlotIcon : table.UnfilledSlotIcon;
                        rowN.SlotConnections[columnIndex].color = hasN ? table.RowTextFilledColor : table.RowTextFilledColor;
                        
                        hasFullColumn &= hasN;
                        hasAnyN |= hasN;
                    }
                    if ((rowData.Dopants & ContractRequirementResolver.DopantRows.P) != 0) {
                        bool hasP = hasPrimary && ContractRequirementResolver.HasDiscoveredDopantProperty(recordMap, columnData.DopantP, MaterialPropertyLabel.PDopantFor, materialId);
                        rowP.Slots[columnIndex].sprite = hasP ? table.FilledSlotIcon : table.UnfilledSlotIcon;
                        rowP.SlotConnections[columnIndex].color = hasP && hasN ? table.RowTextFilledColor : table.RowTextFilledColor;

                        hasFullColumn &= hasP;
                        hasAnyP |= hasP;
                    }

                    hasFinishedColumn |= hasFullColumn;
                }

                rowPrimary.Label.color = rowPrimary.SubLabel.color = hasAnyPrimary ? table.RowTextFilledColor : table.RowTextDefaultColor;
                if (rowN) {
                    rowN.Label.color = hasAnyN ? table.RowTextFilledColor : table.RowTextDefaultColor;
                }
                if (rowP) {
                    rowP.Label.color = hasAnyP ? table.RowTextFilledColor : table.RowTextDefaultColor;
                }
                rowVisuals.Background.color = hasFinishedColumn ? table.RowBackgroundFilledColor : table.RowBackgroundDefaultColor;
                foreach(var faintLine in rowVisuals.FaintLines) {
                    faintLine.color = hasFinishedColumn ? table.RowFaintLineFilledColor : table.RowFaintLineDefaultColor;
                }
            }
        }

        #endregion // Updates

        #region Wiki Hooks

        
        #endregion // Wiki Hooks
    }
}