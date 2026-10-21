using BeauRoutine;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Music;
using FieldDay.Scripting;
using FieldDay.Systems;
using FieldDay.UI.Widgets;
using SpaceFab.Design.Visuals;
using SpaceFab.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design {
    public sealed class SimTableLayout : MonoBehaviour {
        public SuiteHeader[] InputHeaders;
        public SuiteHeader[] OutputHeaders;
        public SuiteRowV2[] Rows;
        public RectTransform RowHighlight;

        [Header("Interactions")]
        public GuiButton TestButton;

        [Header("Config")]
        public Vector2 DefaultRowIconSize = new Vector2(16, 16);
        public Vector2 LargeRowIconSize = new Vector2(24, 24);
        public float SingleColumnMarkerX;
        public float DoubleColumnMarkerX;

        [NonSerialized] public SimTableColumnMapping Mapping;
    }

    public unsafe struct SimTableColumnMapping {
        public fixed byte InputColumns[2];
        public fixed byte OutputColumns[2];
        public byte InputCount;
        public byte OutputCount;
    }

    public enum SimTableColumnType : byte {
        Empty,
        InputA,
        InputB,
        OutputX,
        OutputY
    }

    static public partial class SimTableUtility {

        private const TestSuiteColumnMask BothInputColumns = TestSuiteColumnMask.InputA | TestSuiteColumnMask.InputB;
        private const TestSuiteColumnMask BothOutputColumns = TestSuiteColumnMask.OutputX | TestSuiteColumnMask.OutputY;

        #region Construction

        static public void ConstructTable(SimTableLayout layout, TestSuiteData suiteData, SuiteVisualConfig config) {
            ConstructColumnMapping(ref layout.Mapping, suiteData.ColumnMask, layout);
            ConstructHeaders(layout, suiteData, config);
            PopulateSharedRowData(layout, suiteData, config);

            for(int i = 0; i < suiteData.Rows.Length; i++) {
                PopulateRowData(layout.Rows[i], layout.Mapping, suiteData.Rows[i], config);
            }
        }

        static private unsafe void ConstructColumnMapping(ref SimTableColumnMapping mapping, TestSuiteColumnMask columnMask, SimTableLayout layout) {
            int inputCount = 0,
                outputCount = 0;
            
            if ((columnMask & BothInputColumns) == BothInputColumns) {
                mapping.InputColumns[1] = (byte) SimTableColumnType.InputA;
                mapping.InputColumns[0] = (byte) SimTableColumnType.InputB;
                inputCount = 2;
            } else if ((columnMask & TestSuiteColumnMask.InputA) != 0) {
                mapping.InputColumns[0] = (byte) SimTableColumnType.InputA;
                mapping.InputColumns[1] = (byte) SimTableColumnType.Empty;
                inputCount = 1;
            } else if ((columnMask & TestSuiteColumnMask.InputB) != 0) {
                mapping.InputColumns[0] = (byte) SimTableColumnType.InputB;
                mapping.InputColumns[1] = (byte) SimTableColumnType.Empty;
                inputCount = 1;
            }

            if ((columnMask & BothOutputColumns) == BothOutputColumns) {
                mapping.OutputColumns[0] = (byte) SimTableColumnType.OutputX;
                mapping.OutputColumns[1] = (byte) SimTableColumnType.OutputY;
                outputCount = 2;
            } else if ((columnMask & TestSuiteColumnMask.OutputX) != 0) {
                mapping.OutputColumns[0] = (byte) SimTableColumnType.OutputX;
                mapping.OutputColumns[1] = (byte) SimTableColumnType.Empty;
                outputCount = 1;
            } else if ((columnMask & TestSuiteColumnMask.OutputY) != 0) {
                mapping.OutputColumns[0] = (byte) SimTableColumnType.OutputY;
                mapping.OutputColumns[1] = (byte) SimTableColumnType.Empty;
                outputCount = 1;
            }

            Assert.True(inputCount > 0, "No inputs?");
            Assert.True(outputCount > 0, "No outputs?");
            Assert.True(inputCount <= 2, "Too many inputs?");
            Assert.True(outputCount <= 2, "Too many outputs?");

            mapping.InputCount = (byte) inputCount;
            mapping.OutputCount = (byte) outputCount;
        }

        static private unsafe void ConstructHeaders(SimTableLayout layout, TestSuiteData suiteData, SuiteVisualConfig config) {
            for(int i = 0; i < layout.InputHeaders.Length; i++) {
                PopulateHeader(layout.InputHeaders[i], (SimTableColumnType) layout.Mapping.InputColumns[i], config);
            }
            for (int i = 0; i < layout.OutputHeaders.Length; i++) {
                PopulateHeader(layout.OutputHeaders[i], (SimTableColumnType) layout.Mapping.OutputColumns[i], config);
            }
        }

        static private void PopulateHeader(SuiteHeader header, SimTableColumnType columnType, SuiteVisualConfig config) {
            if (columnType == SimTableColumnType.Empty) {
                header.Label.gameObject.SetActive(false);
                header.Icon.gameObject.SetActive(false);
                return;
            }

            switch (columnType) {
                case SimTableColumnType.InputA: {
                    header.Label.SetText(config.InputALabel);
                    break;
                }
                case SimTableColumnType.InputB: {
                    header.Label.SetText(config.InputBLabel);
                    break;
                }
                case SimTableColumnType.OutputX: {
                    header.Label.SetText(config.OutputXLabel);
                    break;
                }
                case SimTableColumnType.OutputY: {
                    header.Label.SetText(config.OutputYLabel);
                    break;
                }
            }

            header.Icon.gameObject.SetActive(true);
            header.Label.gameObject.SetActive(true);
        }
    
        static private unsafe void PopulateSharedRowData(SimTableLayout layout, TestSuiteData suiteData, SuiteVisualConfig config) {
            bool inputWide = layout.Mapping.InputCount > 1;
            bool outputWide = layout.Mapping.OutputCount > 1;

            float leftProgressWidth = inputWide ? layout.DoubleColumnMarkerX : layout.SingleColumnMarkerX;
            float rightProgressWidth = outputWide ? layout.DoubleColumnMarkerX : layout.SingleColumnMarkerX;

            for (int i = 0; i < suiteData.Rows.Length; i++) {
                SuiteRowV2 row = layout.Rows[i];
                Positioning.SetWidthDelta(row.LeftProgress.rectTransform, leftProgressWidth);
                Positioning.SetWidthDelta(row.RightProgress.rectTransform, rightProgressWidth);

                if (!inputWide) {
                    HideColumn(row.Inputs[1]);
                }
                if (!outputWide) {
                    HideColumn(row.Outputs[1]);
                }

                row.LeftProgress.enabled = false;
                row.RightProgress.enabled = false;

                row.ResultIcon.sprite = config.RowPending;
                Positioning.SetSizeDelta(row.ResultIcon.rectTransform, layout.DefaultRowIconSize);
            }

            for(int i = suiteData.Rows.Length; i < layout.Rows.Length; i++) {
                HideRow(layout.Rows[i]);
            }
        }

        static private unsafe void PopulateRowData(SuiteRowV2 row, SimTableColumnMapping mapping, TestData testData, SuiteVisualConfig config) {
            for(int i = 0; i < mapping.InputCount; i++) {
                SuiteColV2 col = row.Inputs[i];
                SimTableColumnType colType = (SimTableColumnType) mapping.InputColumns[i];
                FlowState desiredFlow = ExtractFlowState(testData, colType);

                col.Slot.sprite = SuiteVisualsDBUtility.GetInputSlotIconForFlowState(config, desiredFlow);
                col.Label.SetText(SuiteVisualsDBUtility.GetLabelForFlowState(config, desiredFlow));
            }

            for(int i = 0; i < mapping.OutputCount; i++) {
                SuiteColV2 col = row.Outputs[i];
                SimTableColumnType colType = (SimTableColumnType) mapping.OutputColumns[i];
                FlowState desiredFlow = ExtractFlowState(testData, colType);

                col.Slot.sprite = config.OutputEmpty;
                col.Label.SetText(SuiteVisualsDBUtility.GetLabelForFlowState(config, desiredFlow));
                col.Label.color = config.OutputEmptyTextColor;
            }
        }

        #endregion // Construction

        static private void HideRow(SuiteRowV2 row) {
            foreach (var col in row.Inputs) {
                HideColumn(col);
            }
            foreach (var col in row.Outputs) {
                HideColumn(col);
            }
            row.ResultIcon.gameObject.SetActive(false);
            row.LeftProgress.gameObject.SetActive(false);
            row.RightProgress.gameObject.SetActive(false);
        }

        static private void HideColumn(SuiteColV2 column) {
            column.Label.gameObject.SetActive(false);
            column.Slot.gameObject.SetActive(false);
        }

        #region Rows

        static public void SetRowAppearance(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config, TestRowVerdict verdict) {
            switch (verdict) {
                case TestRowVerdict.Untested: {
                    SimTableUtility.SetRowPending(layout, row, config);
                    break;
                }
                case TestRowVerdict.InProgress: {
                    SimTableUtility.SetRowInProgress(layout, row, config);
                    break;
                }
                case TestRowVerdict.Correct: {
                    SimTableUtility.SetRowSuccess(layout, row, config);
                    break;
                }
                case TestRowVerdict.Incorrect:
                case TestRowVerdict.Unstable: {
                    SimTableUtility.SetRowFailure(layout, row, config);
                    break;
                }
            }
        }
        static public void SetRowHighlighted(SimTableLayout layout, InputToggleState toggleState) {
            RectTransform parentTransform = (RectTransform)layout.Rows[toggleState.LastMatchedRowIndex].transform;
            layout.RowHighlight.localPosition = parentTransform.localPosition;
        }

        static public void SetRowPending(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config) {
            row.LeftProgress.enabled = false;
            row.RightProgress.enabled = false;
            row.ResultIcon.sprite = config.RowPending;
            Positioning.SetSizeDelta(row.ResultIcon.rectTransform, layout.DefaultRowIconSize);
        }

        static public void SetRowInProgress(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config) {
            row.LeftProgress.enabled = true;
            row.RightProgress.enabled = true;
            row.LeftProgress.color = config.RowPendingLineLeftColor;
            row.RightProgress.color = config.RowPendingLineRightColor;
            row.ResultIcon.sprite = config.RowPending;
            Positioning.SetSizeDelta(row.ResultIcon.rectTransform, layout.DefaultRowIconSize);
        }

        static public void SetRowSuccess(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config) {
            row.LeftProgress.enabled = true;
            row.RightProgress.enabled = true;
            row.LeftProgress.color = config.RowSuccessLineColor;
            row.RightProgress.color = config.RowSuccessLineColor;
            row.ResultIcon.sprite = config.RowSuccess;
            Positioning.SetSizeDelta(row.ResultIcon.rectTransform, layout.LargeRowIconSize);
        }

        static public void SetRowFailure(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config) {
            row.LeftProgress.enabled = true;
            row.RightProgress.enabled = true;
            row.LeftProgress.color = config.RowFailureLineColor;
            row.RightProgress.color = config.RowFailureLineColor;
            row.ResultIcon.sprite = config.RowFailure;
            Positioning.SetSizeDelta(row.ResultIcon.rectTransform, layout.LargeRowIconSize);
        }

        #endregion // Rows

        #region Columns

        static public unsafe void UpdateRowOutputs(SimTableLayout layout, SuiteRowV2 row, SuiteVisualConfig config, RowVerdictSet verdicts, TestData currentResults) {
            for(int i = 0; i < layout.Mapping.OutputCount; i++) {
                SuiteColV2 col = row.Outputs[i];
                SimTableColumnType columnType = ((SimTableColumnType) layout.Mapping.OutputColumns[i]);
                FlowState flow = FlowState.Empty;
                CellVerdict verdict = CellVerdict.Hidden;
                switch (columnType) {
                    case SimTableColumnType.OutputX: {
                        flow = currentResults.OutputX;
                        verdict = verdicts.OutputX;
                        break;
                    }
                    case SimTableColumnType.OutputY: {
                        flow = currentResults.OutputY;
                        verdict = verdicts.OutputY;
                        break;
                    }
                    default: {
                        Assert.Fail("Sim table column '{0}' is not an output column!", columnType);
                        break;
                    }
                }

                if (verdict == CellVerdict.Hidden || verdict == CellVerdict.InProgress) {
                    col.Slot.sprite = config.OutputEmpty;
                    col.Label.color = config.OutputEmptyTextColor;
                } else {
                    switch (flow) {
                        case FlowState.Unstable:
                        case FlowState.Empty: {
                            col.Slot.sprite = config.OutputEmpty;
                            col.Label.color = config.OutputEmptyTextColor;
                            break;
                        }
                        case FlowState.Hi: {
                            col.Slot.sprite = config.OutputHigh;
                            col.Label.color = config.OutputFilledTextColor;
                            break;
                        }
                        case FlowState.Lo: {
                            col.Slot.sprite = config.OutputLow;
                            col.Label.color = config.OutputFilledTextColor;
                            break;
                        }
                    }
                }
            }
        }

        #endregion // Columns

        static public FlowState ExtractFlowState(in TestData testData, SimTableColumnType column) {
            switch (column) {
                case SimTableColumnType.InputA: {
                    return testData.InputA;
                }
                case SimTableColumnType.InputB: {
                    return testData.InputB;
                }
                case SimTableColumnType.OutputX: {
                    return testData.OutputX;
                }
                case SimTableColumnType.OutputY: {
                    return testData.OutputY;
                }
                default: {
                    Assert.Fail("Cannot extract from column '{0}'", column);
                    return FlowState.Empty;
                }
            }
        }
    }
}
