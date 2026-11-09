using System.Collections.Generic;
using BeauUtil;
using SpaceFab.Materials;

namespace SpaceFab.Supply
{
    public static class SupplyRequirementUtility
    {
        public enum DopantRole : byte
        {
            None, // not a dopant
            Dopant, // used as a dopant (confirmed, valid context)
            NeedsResearch, // valid context but not confirmed
            MissingSemiconductor, // semiconductor not gathered
            Mismatch         // semiconductor gathered, but does not match
        }

        /// <summary>Evaluation result for a single table row. Dopants are evaluated against the same semiconductor column.</summary>
        public struct RowEval
        {
            public bool PrimaryFound, NFound, PFound;
            public StringHash32 Primary, N, P;
            public int Score
            {
                get { return (PrimaryFound ? 1 : 0) + (NFound ? 1 : 0) + (PFound ? 1 : 0); }
            }
        }

        #region Omit

        /// <summary>Checks whether the contract's Supply omit list includes an insulator property.</summary>
        public static bool IsInsulatorOmitted(ContractDef contract)
        {
            MaterialPropertyCheck[] omitted = contract != null ? contract.OmitFromSupplyRequirements() : null;
            if (omitted == null) return false;
            for (int i = 0; i < omitted.Length; i++)
            {
                if (omitted[i].Label == MaterialPropertyLabel.Insulator
                    || omitted[i].Label == MaterialPropertyLabel.InsulatorNaive)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsRowOmitted(in ContractRequirementResolver.MaterialRow row, bool omitInsulator)
        {
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
            in ContractRequirementResolver.MaterialRow row, HashSet<StringHash32> gathered, HashSet<StringHash32> reserved)
        {
            RowEval best = default;
            int bestScore = -1;
            int fullScore = 1 + ((row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 ? 1 : 0)
                + ((row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 ? 1 : 0);

            for (int i = 0; i < row.ColumnRange.Length; i++)
            {
                ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                if (!gathered.Contains(col.MaterialId) || reserved.Contains(col.MaterialId)
                    || !ContractRequirementResolver.HasDiscoveredAllProperties(records, col.MaterialId, row.Properties)) {
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
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator, HashSet<StringHash32> gathered, HashSet<StringHash32> reserved)
        {
            if (table.Rows == null) return 0;
            int total = 0;
            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;
                total += EvaluateRow(table, records, row, gathered, reserved).Score;
            }
            return total;
        }

        #endregion Evaluation

        #region Error Panel Queries

        public static bool HasDopantRequirement(in ContractRequirementResolver.RequirementsTable table)
        {
            if (table.Rows == null) return false;
            for (int r = 0; r < table.Rows.Count; r++) {
                if (table.Rows[r].Dopants != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Checks whether the material is the table-designated N or P dopant of a semiconductor
        /// column that is currently gathered and has all of its row properties discovered.
        /// When this returns true, the material is reserved for the dopant role and must not fill
        /// a primary (conductor/semiconductor) slot.
        /// </summary>
        public static bool IsReservedDopant(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, HashSet<StringHash32> gathered,
            StringHash32 materialId, out bool propertyDiscovered)
        {
            propertyDiscovered = false;
            bool reserved = false;
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (row.Dopants == 0) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                    if (col.MaterialId == materialId || !gathered.Contains(col.MaterialId)
                        || !ContractRequirementResolver.HasDiscoveredAllProperties(records, col.MaterialId, row.Properties)) {
                        continue;
                    }

                    bool isN = (row.Dopants & ContractRequirementResolver.DopantRows.N) != 0 && col.DopantN == materialId;
                    bool isP = (row.Dopants & ContractRequirementResolver.DopantRows.P) != 0 && col.DopantP == materialId;
                    if (!isN && !isP) {
                        continue;
                    }

                    reserved = true;
                    if ((isN && ContractRequirementResolver.HasDiscoveredDopantProperty(records, materialId, MaterialPropertyLabel.NDopantFor, col.MaterialId))
                        || (isP && ContractRequirementResolver.HasDiscoveredDopantProperty(records, materialId, MaterialPropertyLabel.PDopantFor, col.MaterialId))) {
                        propertyDiscovered = true;
                    }
                }
            }
            return reserved;
        }

        /// <summary>
        /// Checks whether the material is a primary column (conductor, insulator, or semiconductor candidate)
        /// of any table row that is not omitted from supply chain requirements.
        /// </summary>
        private static bool IsPrimaryColumn(in ContractRequirementResolver.RequirementsTable table, bool omitInsulator, StringHash32 materialId)
        {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) {
                    continue;
                }

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    if (table.Columns[row.ColumnRange.Offset + i].MaterialId == materialId) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether any semiconductor row has a gathered semiconductor with all row properties discovered.
        /// </summary>
        private static bool AnyGatheredDopantSemiconductor(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, HashSet<StringHash32> gathered, StringHash32 exclude)
        {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (row.Dopants == 0) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    StringHash32 semi = table.Columns[row.ColumnRange.Offset + i].MaterialId;
                    if (semi != exclude && gathered.Contains(semi)
                        && ContractRequirementResolver.HasDiscoveredAllProperties(records, semi, row.Properties)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Classifies a material's dopant-related role. The order of checks matters:
        /// 1) Reserved dopant: a gathered, fully discovered semiconductor designates this material as its N/P dopant.
        /// Returns "Dopant" if the dopant property is confirmed, otherwise "NeedsResearch".
        /// 2) Primary column: the material can fill a conductor/semiconductor slot. Skips dopant diagnosis and
        /// returns "None".
        /// 3) Pure dopant: the record has an N/P dopant property and the contract requires dopants. Returns "Mismatch"
        /// if some other semiconductor is gathered, or "MissingSemiconductor" if none is.
        /// Anything else returns "None".
        /// </summary>
        public static DopantRole ClassifyDopant(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator,
            HashSet<StringHash32> gathered, StringHash32 id, in MaterialPropertyRecord record)
        {
            if (IsReservedDopant(table, records, gathered, id, out bool discovered)) {
                return discovered ? DopantRole.Dopant : DopantRole.NeedsResearch;
            }

            if (IsPrimaryColumn(table, omitInsulator, id)) {
                return DopantRole.None;
            }

            bool isDopantRecord = MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.NDopantFor)
                || MaterialPropertyRecordUtility.HasAny(record, MaterialPropertyLabel.PDopantFor);
            if (!isDopantRecord || !HasDopantRequirement(table)) {
                return DopantRole.None;
            }

            return AnyGatheredDopantSemiconductor(table, records, gathered, id)
                ? DopantRole.Mismatch : DopantRole.MissingSemiconductor;
        }

        /// <summary>
        /// Collects every gathered material that is currently reserved as a dopant. Compute this once from the
        /// gathered set and reuse it, so that the checks do not change which materials are reserved.
        /// </summary>
        public static void FindReservedDopants(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, HashSet<StringHash32> gathered, HashSet<StringHash32> output)
        {
            output.Clear();
            foreach (StringHash32 id in gathered) {
                if (IsReservedDopant(table, records, gathered, id, out _)) {
                    output.Add(id);
                }
            }
        }

        /// <summary>
        /// Checks whether a primary row containing the material as a column is already filled by
        /// a different gathered material.
        /// </summary>
        public static bool IsRowFilledByOther(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator,
            HashSet<StringHash32> gathered, HashSet<StringHash32> reserved, StringHash32 materialId)
        {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;

                bool containsId = false;
                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    if (table.Columns[row.ColumnRange.Offset + i].MaterialId == materialId) {
                        containsId = true;
                        break;
                    }
                }
                if (containsId && EvaluateRow(table, records, row, gathered, reserved).PrimaryFound) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Checks whether the material qualifies as a candidate for any slot based on its discovered properties.</summary>
        public static bool IsCandidate(in ContractRequirementResolver.RequirementsTable table,
            Dictionary<StringHash32, MaterialPropertyRecord> records, bool omitInsulator, StringHash32 id)
        {
            if (table.Rows == null) return false;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow row = table.Rows[r];
                if (IsRowOmitted(row, omitInsulator)) continue;

                for (int i = 0; i < row.ColumnRange.Length; i++) {
                    ContractRequirementResolver.MaterialColumn col = table.Columns[row.ColumnRange.Offset + i];
                    if (col.MaterialId == id && ContractRequirementResolver.HasDiscoveredAllProperties(records, id, row.Properties)) {
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
        public static bool IsInTable(in ContractRequirementResolver.RequirementsTable table, bool omitInsulator, StringHash32 id)
        {
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