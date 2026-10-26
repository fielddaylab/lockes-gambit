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
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace SpaceFab {
    public sealed class ContractRequirementTable : MonoBehaviour {
        public struct PropertyLabelList {
            public unsafe fixed byte Properties[4];
            public int Count;

            public unsafe void Add(MaterialPropertyLabel label) {
                Assert.True(Count < 4);
                Properties[Count++] = (byte)label;
            }

            public unsafe MaterialPropertyLabel Get(int index) {
                Assert.True(index >= 0 && index < Count);
                return (MaterialPropertyLabel)Properties[index];
            }
        }

        public struct MaterialRow {
            public ContractRequirementRow Row;
            public OffsetLengthU16 ColumnRange;
            public DopantRows Dopants;
            public PropertyLabelList Properties;
        }
        
        public struct MaterialColumn {
            public StringHash32 MaterialId;
            public StringHash32[] DopantN;
            public StringHash32[] DopantP;
        }

        [Flags]
        public enum DopantRows : byte {
            N = 1,
            P = 2
        }

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

        [NonSerialized] public WorkList<MaterialRow> Rows = new WorkList<MaterialRow>(4);
        [NonSerialized] public WorkList<MaterialColumn> Columns = new WorkList<MaterialColumn>(24);
        [NonSerialized] public int UsedDefaultRows = 0;
        [NonSerialized] public bool UsedExtendedRow = false;
    }

    static public partial class ContractUIUtility {
        
        #region Data Extraction

        static public void BuildRequirementData(ContractRequirementTable table, int chapterIndex, ContractDef contract) {
            table.Rows.Clear();
            table.Columns.Clear();

            if (contract == null) {
                table.UsedDefaultRows = 0;
                table.UsedExtendedRow = false;
                return;
            }

            ContractRequirementTable.PropertyLabelList conductorProps = default;
            ContractRequirementTable.PropertyLabelList insulatorProps = default;
            ContractRequirementTable.PropertyLabelList semiconductorProps = default;
            ContractRequirementTable.DopantRows dopants = default;

            bool hasNaiveDef = false;

            foreach (var req in contract.RequiredMaterialProperties()) {
                switch (req.Label) {
                    case MaterialPropertyLabel.ConductorNaive: {
                        hasNaiveDef = true;
                        conductorProps.Add(req.Label);
                        break;
                    }
                    case MaterialPropertyLabel.Conductor:
                    case MaterialPropertyLabel.HiTempConductor: {
                        conductorProps.Add(req.Label);
                        break;
                    }
                    case MaterialPropertyLabel.InsulatorNaive: {
                        hasNaiveDef = true;
                        insulatorProps.Add(req.Label);
                        break;
                    }
                    case MaterialPropertyLabel.Insulator: {
                        insulatorProps.Add(req.Label);
                        break;
                    }
                    case MaterialPropertyLabel.Semiconductor:
                    case MaterialPropertyLabel.HighMobilitySemiconductor:
                    case MaterialPropertyLabel.HighVoltageSemiconductor:
                    case MaterialPropertyLabel.HiTempSemiConductor:
                    case MaterialPropertyLabel.LightEmittingSemiconductor: {
                        semiconductorProps.Add(req.Label);
                        break;
                    }
                    case MaterialPropertyLabel.NDopantFor: {
                        dopants |= ContractRequirementTable.DopantRows.N;
                        break;
                    }
                    case MaterialPropertyLabel.PDopantFor: {
                        dopants |= ContractRequirementTable.DopantRows.P;
                        break;
                    }
                }
            }

            int usedDefaultRows = 0;
            bool usedExtendedRow = false;

            using (TempReferenceBuffer<MaterialAsset> chapterMaterials = ExtractNonExcludedMaterials(chapterIndex)) {
                if (conductorProps.Count > 0) {
                    ContractRequirementTable.MaterialRow conductorRow = default;
                    conductorRow.Row = table.DefaultRows[usedDefaultRows++];
                    conductorRow.Properties = conductorProps;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, conductorProps, !hasNaiveDef)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching conductors for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            ContractRequirementTable.MaterialColumn column;
                            column.MaterialId = rowMaterial.AssetId;
                            column.DopantN = default;
                            column.DopantP = default;
                            table.Columns.Add(column);

                            columnCount++;
                        }
                    }

                    conductorRow.ColumnRange = new OffsetLengthU16((ushort)columnOffset, (ushort)columnCount);
                    table.Rows.Add(conductorRow);
                }

                if (insulatorProps.Count > 0) {
                    ContractRequirementTable.MaterialRow insulatorRow = default;
                    insulatorRow.Row = table.DefaultRows[usedDefaultRows++];
                    insulatorRow.Properties = insulatorProps;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, insulatorProps, !hasNaiveDef)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching insulators for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            ContractRequirementTable.MaterialColumn column;
                            column.MaterialId = rowMaterial.AssetId;
                            column.DopantN = default;
                            column.DopantP = default;
                            table.Columns.Add(column);

                            columnCount++;
                        }
                    }

                    insulatorRow.ColumnRange = new OffsetLengthU16((ushort)columnOffset, (ushort)columnCount);
                    table.Rows.Add(insulatorRow);
                }

                if (semiconductorProps.Count > 0) {
                    ContractRequirementTable.MaterialRow semiconductorRow = default;

                    if (dopants != 0) {
                        semiconductorRow.Row = table.ExtendedRow;
                        usedExtendedRow = true;
                    } else {
                        semiconductorRow.Row = table.DefaultRows[usedDefaultRows++];
                    }

                    semiconductorRow.Properties = semiconductorProps;
                    semiconductorRow.Dopants = dopants;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, semiconductorProps, false)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching semiconductors for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            ContractRequirementTable.MaterialColumn column = default;
                            column.MaterialId = rowMaterial.AssetId;

                            if ((dopants & ContractRequirementTable.DopantRows.N) != 0) {
                                column.DopantN = FindDopantId(chapterMaterials, rowMaterial, MaterialPropertyLabel.NDopantFor);
                            }
                            if ((dopants & ContractRequirementTable.DopantRows.P) != 0) {
                                column.DopantP = FindDopantId(chapterMaterials, rowMaterial, MaterialPropertyLabel.PDopantFor);
                            }
                            table.Columns.Add(column);

                            columnCount++;
                        }
                    }

                    semiconductorRow.ColumnRange = new OffsetLengthU16((ushort)columnOffset, (ushort)columnCount);
                    table.Rows.Add(semiconductorRow);
                }
            }

            table.UsedDefaultRows = usedDefaultRows;
            table.UsedExtendedRow = usedExtendedRow;
        }

        static private TempReferenceBuffer<MaterialAsset> ExtractNonExcludedMaterials(int chapterIndex) {
            TempReferenceBuffer<MaterialAsset> tempBuffer = TempReferenceBuffer<MaterialAsset>.Create();
            ChapterManifest manifest = Find.GlobalAsset<ChapterManifest>();
            foreach (var materialId in manifest.ResidentData[chapterIndex].ResearchableMaterials) {
                MaterialAsset material = Find.NamedAsset<MaterialAsset>(materialId);
                tempBuffer.Add(material);
            }
            return tempBuffer;
        }

        static private TempReferenceBuffer<MaterialAsset> ExtractMatchingMaterials(TempReferenceBuffer<MaterialAsset> materialSet, ContractRequirementTable.PropertyLabelList properties, bool excludeDopants) {
            TempReferenceBuffer<MaterialAsset> tempBuffer = TempReferenceBuffer<MaterialAsset>.Create(materialSet.Count);
            for(int i = 0; i < materialSet.Count; i++) {
                MaterialAsset material = materialSet[i];
                if (excludeDopants && IsDopant(material)) {
                    continue;
                }

                if (HasAllProperties(material, properties)) {
                    tempBuffer.Add(material);
                }
            }
            return tempBuffer;
        }

        static private StringHash32[] FindDopantId(TempReferenceBuffer<MaterialAsset> materialSet, MaterialAsset baseMaterial, MaterialPropertyLabel dopantType) {
            Assert.True(dopantType == MaterialPropertyLabel.NDopantFor || dopantType == MaterialPropertyLabel.PDopantFor);

            List<StringHash32> dopants = new();
            for(int i = 0; i < materialSet.Count; i++) {
                MaterialAsset material = materialSet[i];
                if (material == baseMaterial) {
                    continue;
                }

                for(int j = 0; j < material.Properties.Length; j++) {
                    if (material.Properties[j] == dopantType) {
                        foreach(var context in material.Contexts) {
                            if (context == baseMaterial) {
                                dopants.Add(material.AssetId);
                            }
                        }
                    }
                }
            }

            Assert.False(dopants.Count == 0, "No dopant {1} specified for material {0} even though required by material requirement table", baseMaterial.name, dopantType);
            return dopants.ToArray();
        }

        static private bool HasAllProperties(MaterialAsset material, ContractRequirementTable.PropertyLabelList properties) {
            for(int i = 0; i < properties.Count; i++) {
                MaterialPropertyLabel checkedProp = properties.Get(i);
                bool hasCheckedProp = false;
                foreach(var prop in material.Properties) {
                    if (checkedProp == prop) {
                        hasCheckedProp = true;
                        break;
                    }
                }

                if (!hasCheckedProp) {
                    return false;
                }
            }

            return true;
        }

        static private bool IsDopant(MaterialAsset material) {
            foreach(var prop in material.Properties) {
                if (prop == MaterialPropertyLabel.NDopantFor | prop == MaterialPropertyLabel.PDopantFor) {
                    return true;
                }
            }

            return false;
        }

        #endregion // Data Extraction

        #region Visuals Generation

        static public void InitializeVisuals(ContractRequirementTable table) {
            for (int i = 0; i < table.DefaultRows.Length; i++) {
                table.DefaultRows[i].gameObject.SetActive(false);
            }
            table.ExtendedRow.gameObject.SetActive(false);

            Find.GlobalAsset(out ResearchObservationChipAssets chipAssets);

            using (PooledStringBuilder labelBuilder = PooledStringBuilder.Create())
            using (PooledStringBuilder subLabelBuilder = PooledStringBuilder.Create()) {
                for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++) {
                    ContractRequirementTable.MaterialRow rowData = table.Rows[rowIndex];
                    ContractRequirementRow rowVisuals = rowData.Row;

                    ContractRequirementSubRow subRowVisuals = rowVisuals.DefaultRow;
                    AssignSubRowCount(subRowVisuals, rowData.ColumnRange.Length);

                    labelBuilder.Builder.Clear();
                    subLabelBuilder.Builder.Clear();

                    GenerateLabel(rowData.Properties, labelBuilder, subLabelBuilder, out MaterialPropertyLabel baseProperty, chipAssets, out Sprite icon);
                    AssignSubRowLabel(subRowVisuals, labelBuilder, subLabelBuilder, icon, table.SubLabelOffsetY);

                    int usedDopantRows = 0;
                    if ((rowData.Dopants & ContractRequirementTable.DopantRows.N) != 0) {
                        Assert.True(rowVisuals.DopantRows.Length > 0);
                        subRowVisuals = rowVisuals.DopantRows[usedDopantRows++];
                        labelBuilder.Builder.Clear();
                        labelBuilder.Builder.Append("+ N-Type Dopant");
                        AssignSubRowCount(subRowVisuals, rowData.ColumnRange.Length);
                        AssignSubRowLabel(subRowVisuals, labelBuilder, null, null, table.SubLabelOffsetY);
                    }
                    if ((rowData.Dopants & ContractRequirementTable.DopantRows.P) != 0) {
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
                    rowData.Row.gameObject.SetActive(true);
                }
            }

            table.Sizer.VerticalLayout(table.Spacing, 0);
        }

        static private unsafe void GenerateLabel(ContractRequirementTable.PropertyLabelList properties, StringBuilder labelBuilder, StringBuilder subLabelBuilder, out MaterialPropertyLabel basePropertyOutput, ResearchObservationChipAssets chipAssets, out Sprite icon) {
            labelBuilder.Clear();
            subLabelBuilder.Clear();

            MaterialPropertyLabel baseLabel = 0;
            MaterialPropertyLabel* secondaryLabels = stackalloc MaterialPropertyLabel[4];
            int secondaryLabelCount = 0;

            for (int i = 0; i < properties.Count; i++) {
                MaterialPropertyLabel label = properties.Get(i);
                switch (label) {
                    case MaterialPropertyLabel.ConductorNaive:
                    case MaterialPropertyLabel.Conductor:
                        baseLabel = MaterialPropertyLabel.Conductor;
                        break;
                    case MaterialPropertyLabel.HiTempConductor: {
                        baseLabel = MaterialPropertyLabel.Conductor;
                        secondaryLabels[secondaryLabelCount++] = label;
                        break;
                    }
                    case MaterialPropertyLabel.Insulator:
                    case MaterialPropertyLabel.InsulatorNaive:
                        baseLabel = MaterialPropertyLabel.Insulator;
                        break;
                    case MaterialPropertyLabel.Semiconductor:
                        baseLabel = MaterialPropertyLabel.Semiconductor;
                        break;
                    case MaterialPropertyLabel.HiTempSemiConductor:
                    case MaterialPropertyLabel.HighMobilitySemiconductor:
                    case MaterialPropertyLabel.HighVoltageSemiconductor:
                    case MaterialPropertyLabel.LightEmittingSemiconductor:
                        baseLabel = MaterialPropertyLabel.Semiconductor;
                        secondaryLabels[secondaryLabelCount++] = label;
                        break;

                    default: {
                        Assert.Fail("Unable to form label for property {0}", label);
                        break;
                    }
                }
            }

            basePropertyOutput = baseLabel;

            switch (baseLabel) {
                case MaterialPropertyLabel.Conductor: {
                    labelBuilder.Append("Conductor");
                    break;
                }
                case MaterialPropertyLabel.Insulator: {
                    labelBuilder.Append("Insulator");
                    break;
                }
                case MaterialPropertyLabel.Semiconductor: {
                    labelBuilder.Append("Semiconductor");
                    break;
                }

                default: {
                    Assert.Fail("Unable to form label for property {0}", baseLabel);
                    break;
                }
            }

            Unsafe.Quicksort(secondaryLabels, secondaryLabelCount);

            for (int i = 0; i < secondaryLabelCount; i++) {
                if (i > 0) {
                    subLabelBuilder.Append(", ");
                }
                switch (secondaryLabels[i]) {
                    case MaterialPropertyLabel.HiTempConductor:
                    case MaterialPropertyLabel.HiTempSemiConductor: {
                        subLabelBuilder.Append("Hi-Temp");
                        break;
                    }
                    case MaterialPropertyLabel.HighMobilitySemiconductor: {
                        subLabelBuilder.Append("Hi-Mobility");
                        break;
                    }
                    case MaterialPropertyLabel.HighVoltageSemiconductor: {
                        subLabelBuilder.Append("Hi-Voltage");
                        break;
                    }
                    case MaterialPropertyLabel.LightEmittingSemiconductor: {
                        subLabelBuilder.Append("Light-Emitting");
                        break;
                    }
                }
            }

            chipAssets.TryGetIcon(basePropertyOutput, out icon);
        }


        #endregion // Visuals Generation

        #region Updates

        static public void UpdateVisualsWithCompletion(ContractRequirementTable table, Dictionary<StringHash32, MaterialPropertyRecord> recordMap) {
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++) {
                ContractRequirementTable.MaterialRow rowData = table.Rows[rowIndex];
                ContractRequirementRow rowVisuals = rowData.Row;

                rowVisuals.Background.color = table.RowBackgroundDefaultColor;

                bool hasFinishedColumn = false;
                bool hasAnyPrimary = false;
                bool hasAnyN = false;
                bool hasAnyP = false;

                ContractRequirementSubRow rowPrimary = rowVisuals.DefaultRow;
                ContractRequirementSubRow rowN = null;
                ContractRequirementSubRow rowP = null;
                int usedSubRows = 0;

                if ((rowData.Dopants & ContractRequirementTable.DopantRows.N) != 0) {
                    rowN = rowVisuals.DopantRows[usedSubRows++];
                }
                if ((rowData.Dopants & ContractRequirementTable.DopantRows.P) != 0) {
                    rowP = rowVisuals.DopantRows[usedSubRows++];
                }

                for (int columnIndex = 0; columnIndex < rowData.ColumnRange.Length; columnIndex++) {
                    ContractRequirementTable.MaterialColumn columnData = table.Columns[columnIndex + rowData.ColumnRange.Offset];
                    
                    StringHash32 materialId = columnData.MaterialId;
                    bool hasPrimary = HasDiscoveredAllProperties(recordMap, materialId, rowData.Properties);

                    rowPrimary.Slots[columnIndex].sprite = hasPrimary ? table.FilledSlotIcon : table.UnfilledSlotIcon;
                    hasAnyPrimary |= hasPrimary;

                    bool hasFullColumn = hasPrimary;
                    bool hasN = true;

                    if ((rowData.Dopants & ContractRequirementTable.DopantRows.N) != 0) {
                        hasN = hasPrimary && columnData.DopantN.Any(dopant => HasDiscoveredDopantProperty(recordMap, dopant, MaterialPropertyLabel.NDopantFor, materialId));
                        rowN.Slots[columnIndex].sprite = hasN ? table.FilledSlotIcon : table.UnfilledSlotIcon;
                        rowN.SlotConnections[columnIndex].color = hasN ? table.RowTextFilledColor : table.RowTextFilledColor;
                        
                        hasFullColumn &= hasN;
                        hasAnyN |= hasN;
                    }
                    if ((rowData.Dopants & ContractRequirementTable.DopantRows.P) != 0) {
                        bool hasP = hasPrimary && columnData.DopantP.Any(dopant => HasDiscoveredDopantProperty(recordMap, dopant, MaterialPropertyLabel.PDopantFor, materialId));
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

        static private bool HasDiscoveredAllProperties(Dictionary<StringHash32, MaterialPropertyRecord> recordMap, StringHash32 materialId, ContractRequirementTable.PropertyLabelList properties) {
            recordMap.TryGetValue(materialId, out MaterialPropertyRecord record);
            for (int i = 0; i < properties.Count; i++) {
                MaterialPropertyLabel label = properties.Get(i);
                if (!MaterialPropertyRecordUtility.Has(record, label, null)) {
                    return false;
                }
            }

            return true;
        }

        static private bool HasDiscoveredDopantProperty(Dictionary<StringHash32, MaterialPropertyRecord> recordMap, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterial) {
            Assert.True(label == MaterialPropertyLabel.NDopantFor || label == MaterialPropertyLabel.PDopantFor);
            recordMap.TryGetValue(materialId, out MaterialPropertyRecord record);
            return MaterialPropertyRecordUtility.Has(record, label, contextMaterial);
        }

        #endregion // Updates

        #region Wiki Hooks

        
        #endregion // Wiki Hooks
    }
}