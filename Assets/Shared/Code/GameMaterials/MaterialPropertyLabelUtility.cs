namespace SpaceFab.Materials
{
    /// <summary>
    /// Partitioning classifier over MaterialPropertyLabel. Tells callers which
    /// labels round-trip to PlayerProgressState and how their bits are laid out
    /// in MaterialPropertyRecord. Replaces the StaticProperty / DynamicProperty
    /// enums: the static/dynamic distinction lives here as a partition over the
    /// unified label enum, not as a parallel vocabulary.
    ///
    /// GetStaticBitIndex values are wire-format bit positions in
    /// MaterialPropertyRecord.StaticMask. They MUST remain stable - changing one
    /// invalidates every saved game. Reserve new indices by appending to the
    /// switch, never by reordering.
    /// </summary>
    public static class MaterialPropertyLabelUtility
    {
        /// <summary>
        /// True for labels that represent a confirmable Property (round-trips to
        /// PlayerProgressState). False for observation-only labels (evidence
        /// collected in chambers; never persisted).
        /// </summary>
        public static bool IsPersistent(MaterialPropertyLabel label)
        {
            switch (label)
            {
                case MaterialPropertyLabel.ConductorNaive:
                case MaterialPropertyLabel.InsulatorNaive:
                case MaterialPropertyLabel.Insulator:
                case MaterialPropertyLabel.Conductor:
                case MaterialPropertyLabel.Semiconductor:
                case MaterialPropertyLabel.HiTempConductor:
                case MaterialPropertyLabel.HiTempSemiConductor:
                case MaterialPropertyLabel.LightEmittingSemiconductor:
                case MaterialPropertyLabel.HighVoltageSemiconductor:
                case MaterialPropertyLabel.HighMobilitySemiconductor:
                case MaterialPropertyLabel.PDopantFor:
                case MaterialPropertyLabel.NDopantFor:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True for labels that are parameterized by a context material (e.g.
        /// PDopantFor X). Dynamic labels store their bit in
        /// MaterialPropertyRecord.DynamicMask_PDopant or DynamicMaskNDopant,
        /// indexed by MaterialOrderAsset position.
        /// </summary>
        public static bool IsDynamic(MaterialPropertyLabel label)
        {
            switch (label)
            {
                case MaterialPropertyLabel.PDopantFor:
                case MaterialPropertyLabel.NDopantFor:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Bit position in MaterialPropertyRecord.StaticMask for a static
        /// persistent label. Returns -1 for non-static-persistent labels
        /// (observations or dynamic labels). Bit positions are wire-format and
        /// must remain stable across versions.
        /// </summary>
        public static int GetStaticBitIndex(MaterialPropertyLabel label)
        {
            switch (label)
            {
                case MaterialPropertyLabel.ConductorNaive: return 0;
                case MaterialPropertyLabel.InsulatorNaive: return 1;
                case MaterialPropertyLabel.Insulator: return 2;
                case MaterialPropertyLabel.Conductor: return 3;
                case MaterialPropertyLabel.Semiconductor: return 4;
                case MaterialPropertyLabel.HiTempConductor: return 5;
                case MaterialPropertyLabel.HiTempSemiConductor: return 6;
                case MaterialPropertyLabel.LightEmittingSemiconductor: return 7;
                case MaterialPropertyLabel.HighVoltageSemiconductor: return 8;
                case MaterialPropertyLabel.HighMobilitySemiconductor: return 9;
                default: return -1;
            }
        }

        /// <summary>
        /// Reverse of GetStaticBitIndex: resolves a wire-format bit
        /// position back to its persistent static label. Returns true
        /// if the index maps to a known static label; false otherwise
        /// (e.g., bits 10-15 are reserved for future expansion).
        ///
        /// MUST stay in lockstep with GetStaticBitIndex — adding or
        /// reordering a case in either requires the same in both.
        /// </summary>
        public static bool TryGetStaticLabelAt(int bitIndex, out MaterialPropertyLabel label)
        {
            switch (bitIndex)
            {
                case 0: label = MaterialPropertyLabel.ConductorNaive; return true;
                case 1: label = MaterialPropertyLabel.InsulatorNaive; return true;
                case 2: label = MaterialPropertyLabel.Insulator; return true;
                case 3: label = MaterialPropertyLabel.Conductor; return true;
                case 4: label = MaterialPropertyLabel.Semiconductor; return true;
                case 5: label = MaterialPropertyLabel.HiTempConductor; return true;
                case 6: label = MaterialPropertyLabel.HiTempSemiConductor; return true;
                case 7: label = MaterialPropertyLabel.LightEmittingSemiconductor; return true;
                case 8: label = MaterialPropertyLabel.HighVoltageSemiconductor; return true;
                case 9: label = MaterialPropertyLabel.HighMobilitySemiconductor; return true;
                default: label = default; return false;
            }
        }

        /// <summary>
        /// Returns the base property for a more specific property, if one exists.
        /// HiTempConductor maps to Conductor, and semiconductor subtypes map to Semiconductor.
        /// </summary>
        public static bool TryGetBaseProperty(MaterialPropertyLabel label, out MaterialPropertyLabel baseLabel)
        {
            switch (label)
            {
                case MaterialPropertyLabel.HiTempConductor:
                    baseLabel = MaterialPropertyLabel.Conductor;
                    return true;
                case MaterialPropertyLabel.HiTempSemiConductor:
                case MaterialPropertyLabel.LightEmittingSemiconductor:
                case MaterialPropertyLabel.HighVoltageSemiconductor:
                case MaterialPropertyLabel.HighMobilitySemiconductor:
                    baseLabel = MaterialPropertyLabel.Semiconductor;
                    return true;
                default: baseLabel = default; return false;
            }
        }
    }
}
