using System;
using System.Collections.Generic;
using System.Text;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Collections;
using SpaceFab.Materials;
using SpaceFab.Research;
using UnityEngine;

namespace SpaceFab {
    static public class ContractRequirementResolver {

        #region Types

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
            public int RowIndex;
            public OffsetLengthU16 ColumnRange;
            public DopantRows Dopants;
            public RowType RowType;
            public PropertyLabelList Properties;
        }
        
        public struct MaterialColumn {
            public StringHash32 MaterialId;
            public StringHash32 DopantN;
            public StringHash32 DopantP;
        }

        [Flags]
        public enum DopantRows : byte {
            N = 1,
            P = 2
        }

        public enum RowType : byte {
            Default = 0,
            Extended = 1
        }

        public struct RequirementsTable {
            public WorkList<MaterialRow> Rows;
            public WorkList<MaterialColumn> Columns;
            public int DefaultRows;
            public bool HasExtendedRow;
        }

        public enum Satisfaction : byte
        {
            NotSatisfied,
            Satisfied,
            FullySatisfied
        }

        #endregion // Type

        #region Table Construction

        static public void BuildRequirements(ref RequirementsTable table, int chapterIndex, ContractDef contract) {
            Assert.NotNull(table.Rows, "Table not initialized");
            Assert.NotNull(table.Columns, "Table not initialized");
            Assert.NotNullOrDestroyed(contract, "Cannot evaluate null contract");
            
            table.Rows.Clear();
            table.Columns.Clear();

            if (contract == null) {
                table.DefaultRows = 0;
                table.HasExtendedRow = false;
                return;
            }

            PropertyLabelList conductorProps = default;
            PropertyLabelList insulatorProps = default;
            PropertyLabelList semiconductorProps = default;
            DopantRows dopants = default;

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
                        dopants |= DopantRows.N;
                        break;
                    }
                    case MaterialPropertyLabel.PDopantFor: {
                        dopants |= DopantRows.P;
                        break;
                    }
                }
            }

            int usedDefaultRows = 0;
            bool usedExtendedRow = false;

            using (TempReferenceBuffer<MaterialAsset> chapterMaterials = ExtractNonExcludedMaterials(chapterIndex)) {
                if (conductorProps.Count > 0) {
                    MaterialRow conductorRow = default;
                    conductorRow.RowIndex = usedDefaultRows++;
                    conductorRow.Properties = conductorProps;
                    conductorRow.RowType = RowType.Default;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, conductorProps, !hasNaiveDef)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching conductors for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            MaterialColumn column;
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
                    MaterialRow insulatorRow = default;
                    insulatorRow.RowIndex = usedDefaultRows++;
                    insulatorRow.RowType = RowType.Default;
                    insulatorRow.Properties = insulatorProps;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, insulatorProps, !hasNaiveDef)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching insulators for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            MaterialColumn column;
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
                    MaterialRow semiconductorRow = default;

                    if (dopants != 0) {
                        semiconductorRow.RowIndex = 0;
                        semiconductorRow.RowType = RowType.Extended;
                        usedExtendedRow = true;
                    } else {
                        semiconductorRow.RowIndex = usedDefaultRows++;
                        semiconductorRow.RowType = RowType.Default;
                    }

                    semiconductorRow.Properties = semiconductorProps;
                    semiconductorRow.Dopants = dopants;

                    int columnOffset = table.Columns.Count;
                    int columnCount = 0;

                    using (TempReferenceBuffer<MaterialAsset> rowMaterials = ExtractMatchingMaterials(chapterMaterials, semiconductorProps, false)) {
                        Assert.True(rowMaterials.Count <= ContractRequirementSubRow.MaxSlots, "Too many matching semiconductors for contract {0}", contract.name);
                        for (int i = 0; i < rowMaterials.Count; i++) {
                            MaterialAsset rowMaterial = rowMaterials[i];

                            MaterialColumn column = default;
                            column.MaterialId = rowMaterial.AssetId;

                            if ((dopants & DopantRows.N) != 0) {
                                column.DopantN = FindDopantId(chapterMaterials, rowMaterial, MaterialPropertyLabel.NDopantFor);
                            }
                            if ((dopants & DopantRows.P) != 0) {
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

            table.DefaultRows = usedDefaultRows;
            table.HasExtendedRow = usedExtendedRow;
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

        static private TempReferenceBuffer<MaterialAsset> ExtractMatchingMaterials(TempReferenceBuffer<MaterialAsset> materialSet, PropertyLabelList properties, bool excludeDopants) {
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

        static private StringHash32 FindDopantId(TempReferenceBuffer<MaterialAsset> materialSet, MaterialAsset baseMaterial, MaterialPropertyLabel dopantType) {
            Assert.True(dopantType == MaterialPropertyLabel.NDopantFor || dopantType == MaterialPropertyLabel.PDopantFor);

            for(int i = 0; i < materialSet.Count; i++) {
                MaterialAsset material = materialSet[i];
                if (material == baseMaterial) {
                    continue;
                }

                for(int j = 0; j < material.Properties.Length; j++) {
                    if (material.Properties[j] == dopantType) {
                        foreach(var context in material.Contexts) {
                            if (context == baseMaterial) {
                                return material.AssetId;
                            }
                        }
                    }
                }
            }

            Assert.Fail("No dopant {1} specified for material {0} even though required by material requirement table", baseMaterial.name, dopantType);
            return default;
        }

        static private bool HasAllProperties(MaterialAsset material, PropertyLabelList properties) {
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

        #endregion // Table Construction

        #region Full Contract Checks

        static public Satisfaction IsSatisfied(in RequirementsTable table, Dictionary<StringHash32, MaterialPropertyRecord> recordMap) {
            bool hasUnfulfilledSlots = false;
            bool hasUnfulfilledRows = false;
            
            for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++) {
                MaterialRow rowData = table.Rows[rowIndex];
                
                bool hasFinishedColumn = false;
                bool hasAnyPrimary = false;
                bool hasAnyN = false;
                bool hasAnyP = false;

                for (int columnIndex = 0; columnIndex < rowData.ColumnRange.Length; columnIndex++) {
                    MaterialColumn columnData = table.Columns[columnIndex + rowData.ColumnRange.Offset];
                    
                    StringHash32 materialId = columnData.MaterialId;
                    bool hasPrimary = HasDiscoveredAllProperties(recordMap, materialId, rowData.Properties);

                    hasAnyPrimary |= hasPrimary;

                    bool hasFullColumn = hasPrimary;
                    bool hasN = true;

                    if ((rowData.Dopants & DopantRows.N) != 0) {
                        hasN = hasPrimary && HasDiscoveredDopantProperty(recordMap, columnData.DopantN, MaterialPropertyLabel.NDopantFor, materialId);
                        
                        hasFullColumn &= hasN;
                        hasAnyN |= hasN;
                    }
                    if ((rowData.Dopants & DopantRows.P) != 0) {
                        bool hasP = hasPrimary && HasDiscoveredDopantProperty(recordMap, columnData.DopantP, MaterialPropertyLabel.PDopantFor, materialId);
                        
                        hasFullColumn &= hasP;
                        hasAnyP |= hasP;
                    }

                    hasFinishedColumn |= hasFullColumn;
                    hasUnfulfilledSlots |= !hasFullColumn;
                }

                hasUnfulfilledRows |= !hasFinishedColumn;
            }

            if (!hasUnfulfilledRows) {
                if (hasUnfulfilledSlots) {
                    return Satisfaction.Satisfied;
                }
                return Satisfaction.FullySatisfied;
            }
            return Satisfaction.NotSatisfied;
        }

        #endregion // Full Contract Checks

        #region Material Checks

        #endregion // Material Checks

        #region Utilities

        static public bool HasDiscoveredAllProperties(Dictionary<StringHash32, MaterialPropertyRecord> recordMap, StringHash32 materialId, PropertyLabelList properties) {
            recordMap.TryGetValue(materialId, out MaterialPropertyRecord record);
            for (int i = 0; i < properties.Count; i++) {
                MaterialPropertyLabel label = properties.Get(i);
                if (!MaterialPropertyRecordUtility.Has(record, label, null)) {
                    return false;
                }
            }

            return true;
        }

        static public bool HasDiscoveredDopantProperty(Dictionary<StringHash32, MaterialPropertyRecord> recordMap, StringHash32 materialId, MaterialPropertyLabel label, StringHash32 contextMaterial) {
            Assert.True(label == MaterialPropertyLabel.NDopantFor || label == MaterialPropertyLabel.PDopantFor);
            recordMap.TryGetValue(materialId, out MaterialPropertyRecord record);
            return MaterialPropertyRecordUtility.Has(record, label, contextMaterial);
        }
    
        #endregion // Utilities

        #region Visuals

        static public unsafe void GenerateLabel(PropertyLabelList properties, StringBuilder labelBuilder, StringBuilder subLabelBuilder, out MaterialPropertyLabel basePropertyOutput, ResearchObservationChipAssets chipAssets, out Sprite icon) {
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

        #endregion // Visuals
    }
}