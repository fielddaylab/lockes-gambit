using System.Collections.Generic;
using BeauUtil;
using SpaceFab.Materials;

namespace SpaceFab.Supply {
    public static class SupplyRequirementUtility {
        public enum DopantIssue : byte {
            None,
            MissingSemiconductor, // semiconductor not gathered
            Mismatch         // semiconductor gathered, but does not match
        }

        /// <summary>Evaluation result for a single table row. Dopants are evaluated against the same semiconductor column.</summary>
        public struct RowEval {
            public bool PrimaryFound, NFound, PFound;
            public StringHash32 Primary, N, P;
            public int Score {
                get { return (PrimaryFound ? 1 : 0) + (NFound ? 1 : 0) + (PFound ? 1 : 0); }
            }
        }

        #region Omit

        /// <summary>Checks whether the contract's Supply omit list includes an insulator property.</summary>
        public static bool IsInsulatorOmitted(ContractDef contract) {
            MaterialPropertyCheck[] omitted = contract != null ? contract.OmitFromSupplyRequirements() : null;
            if (omitted == null) return false;
            for (int i = 0; i < omitted.Length; i++) {
                if (omitted[i].Label == MaterialPropertyLabel.Insulator
                    || omitted[i].Label == MaterialPropertyLabel.InsulatorNaive) {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRowOmitted(in ContractRequirementResolver.MaterialRow row, bool omitInsulator) {
            if (!omitInsulator || row.Properties.Count == 0) return false;
            MaterialPropertyLabel first = row.Properties.Get(0);

            return first == MaterialPropertyLabel.Insulator || first == MaterialPropertyLabel.InsulatorNaive;
        }

        #endregion Omit

        #region Evaluation

        /// <summary>
        /// Evaluates a row based on gathered materials. Each column represents a semiconductor paired with its
        /// designated N/P dopants. If no column is fully fulfilled, returns the column with the highest progress score.
        /// </summary>
        public static RowEval EvaluateRow(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records,
            in ContractRequirementResolver.MaterialRow row, HashSet<StringHash32> gathered) {
            RowEval best = default;
            int bestScore = -1;
            int fullScore = 1 + ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 ? 1 : 0)
                + ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 ? 1 : 0);

            for (int i = 0; i < row.ColumnRange.Length; i++) {
                ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                if (!gathered.Contains(col.MaterialId) || !ContractRequirementResolver.HasDiscoveredAllProperties(records, col.MaterialId, row.Properties)) {
                    continue;
                }

                RowEval eval = default;
                eval.PrimaryFound = true;
                eval.Primary = col.MaterialId;

                if ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 && !col.DopantN.IsEmpty && gathered.Contains(col.DopantN)
                    && ContractRequirementResolver.HasDiscoveredDopantProperty(records, col.DopantN, MaterialPropertyLabel.NDopantFor, col.MaterialId)) {
                    eval.NFound = true;
                    eval.N = col.DopantN;
                }
                if ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 && !col.DopantP.IsEmpty && gathered.Contains(col.DopantP)
                    && ContractRequirementResolver.HasDiscoveredDopantProperty(records, col.DopantP, MaterialPropertyLabel.PDopantFor, col.MaterialId)) {
                    eval.PFound = true;
                    eval.P = col.DopantP;
                }

                if (eval.Score > bestScore) {
                    best = eval;
                    bestScore = eval.Score;
                }
                if (bestScore == fullScore) break;
            }

            return best;
        }

        /// <summary>Counts the number of satisfied slots (material rows + dopant rows).</summary>
        public static int CountSatisfied(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator, HashSet<StringHash32> gathered) {
            if (table.Rows == null) return 0;
            int total = 0;
            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;
                total += EvaluateRow(table, records, row, gathered).Score;
            }
            return total;
        }

        #endregion Evaluation

        #region Error Panel Queries

        public static bool HasDopantRequirement(in ContractRequirementResolver.RequirementsTable table) {
            if (table.Rows == null) return false;
            for (int r = 0; r < table.Rows.Count; r++) {
                if (table.Rows[r].Dopants != 0) return true;
            }
            return false;
        }

        /// <summary>Collects semiconductors from dopant row columns that have been gathered and have all required properties discovered.</summary>
        public static void FindGatheredSemiconductors(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, HashSet<StringHash32> gathered, List<StringHash32> output) {
            output.Clear();
            if (table.Rows == null) return;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (row.Dopants == 0) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    StringHash32 id = table.Columns[row.ColumnRange.Offset + i].MaterialId;
                    if (gathered.Contains(id)
                        && ContractRequirementResolver.HasDiscoveredAllProperties(records, id, row.Properties)) {
                        output.Add(id);
                    }
                }
            }
        }

        /// <summary>
        /// Diagnose the compatibility of a dopant with gathered semiconductors.
        /// None if it is a designated dopant in the table and has a matching context.
        /// </summary>
        public static DopantIssue Diagnose(in ContractRequirementResolver.RequirementsTable table,
            StringHash32 id, in MaterialPropertyRecord record, List<StringHash32> gatheredSemiconductors) {
            bool isDopant = MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.NDopantFor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.PDopantFor);
            
            if (!isDopant || !HasDopantRequirement(table)) return DopantIssue.None;

            if (gatheredSemiconductors.Count == 0) return DopantIssue.MissingSemiconductor;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (row.Dopants == 0) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                    if (!gatheredSemiconductors.Contains(col.MaterialId)) continue;

                    if ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 && col.DopantN == id
                        && MaterialPropertyRecordUtility.Has(record, MaterialPropertyLabel.NDopantFor, col.MaterialId)) {
                        return DopantIssue.None;
                    }
                    if ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 && col.DopantP == id
                        && MaterialPropertyRecordUtility.Has(record, MaterialPropertyLabel.PDopantFor, col.MaterialId)) {
                        return DopantIssue.None;
                    }
                }
            }
            return DopantIssue.Mismatch;
        }

        /// <summary>Checks whether the material qualifies as a candidate for any slot based on its discovered properties.</summary>
        public static bool IsCandidate(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator, StringHash32 id) {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                    if (col.MaterialId == id
                        && ContractRequirementResolver.HasDiscoveredAllProperties(records, id, row.Properties)) {
                        return true;
                    }
                    if ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 && col.DopantN == id
                        && ContractRequirementResolver.HasDiscoveredDopantProperty(records, id, MaterialPropertyLabel.NDopantFor, col.MaterialId)) {
                        return true;
                    }
                    if ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 && col.DopantP == id
                        && ContractRequirementResolver.HasDiscoveredDopantProperty(records, id, MaterialPropertyLabel.PDopantFor, col.MaterialId)) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Checks whether the material appears as a column material or designated dopant in any non-omitted table row.</summary>
        public static bool IsInTable(in ContractRequirementResolver.RequirementsTable table, bool omitInsulator, StringHash32 id) {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                    if (col.MaterialId == id) return true;
                    if ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 && col.DopantN == id) return true;
                    if ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 && col.DopantP == id) return true;
                }
            }
            return false;
        }

        #endregion Error Panel Queries
    }
}