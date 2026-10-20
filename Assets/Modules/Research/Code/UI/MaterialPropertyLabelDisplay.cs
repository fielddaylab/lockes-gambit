using BeauUtil;
using FieldDay;
using SpaceFab.Materials;

namespace SpaceFab.Research {
    /// <summary>
    /// Maps MaterialPropertyLabel enum values to human-readable strings for
    /// the hypothesis / observation UI. Authoring is intentionally
    /// a switch for now — a NamedAsset / localization table can replace
    /// this once the label vocabulary stabilizes.
    /// </summary>
    public static class MaterialPropertyLabelDisplay {
        // Chip label for an observation. Falls back to the enum name when
        // a label hasn't been given a friendly string yet.
        public static string GetObservationName(MaterialPropertyLabel label) {
            switch (label) {
                case MaterialPropertyLabel.Conductive: return "Conducts electricity";
                case MaterialPropertyLabel.NonConductive: return "Prevents current flow";
                case MaterialPropertyLabel.HeatActivated: return "Heat increases current";
                case MaterialPropertyLabel.HeatDeactivated: return "Heat decreases current";
                case MaterialPropertyLabel.HeatUnaffected: return "Current unchanged by heat";
                case MaterialPropertyLabel.HeatResistant: return "Resists HIGH heat";
                case MaterialPropertyLabel.AtomicRadiusCompliant: return "Atom fits";
                case MaterialPropertyLabel.LightEmitting: return "Emits light when active";
                case MaterialPropertyLabel.HighMobility: return "High electron mobility";
                case MaterialPropertyLabel.VoltageResistant: return "Withstands extreme voltage";
                case MaterialPropertyLabel.ValenceOneLessThan: return "1 less valence electron";
                case MaterialPropertyLabel.ValenceOneMoreThan: return "1 more valence electron";
                default: return label.ToString();
            }
        }

        // Header phrasing for a persistent property. Used in the hypothesis
        // panel's "FIND A ..." title.
        public static string GetPropertyName(MaterialPropertyLabel label, StringHash32 contextId = default) {
            switch (label) {
                case MaterialPropertyLabel.ConductorNaive: return "CONDUCTOR";
                case MaterialPropertyLabel.InsulatorNaive: return "INSULATOR";
                case MaterialPropertyLabel.Insulator: return "INSULATOR";
                case MaterialPropertyLabel.Conductor: return "CONDUCTOR";
                case MaterialPropertyLabel.Semiconductor: return "SEMICONDUCTOR";
                case MaterialPropertyLabel.HiTempConductor: return "HIGH-TEMP CONDUCTOR";
                case MaterialPropertyLabel.HiTempSemiConductor: return "HIGH-TEMP SEMICONDUCTOR";
                case MaterialPropertyLabel.PDopantFor: return GetContextualPropertyName("P-TYPE DOPANT", contextId);
                case MaterialPropertyLabel.NDopantFor: return GetContextualPropertyName("N-TYPE DOPANT", contextId);
                case MaterialPropertyLabel.LightEmittingSemiconductor: return "LIGHT-EMITTING SEMICONDUCTOR";
                case MaterialPropertyLabel.HighVoltageSemiconductor: return "HIGH VOLTAGE SEMICONDUCTOR";
                case MaterialPropertyLabel.HighMobilitySemiconductor: return "HIGH MOBILITY SEMICONDUCTOR";
                default: return label.ToString().ToUpperInvariant();
            }
        }

        private static string GetContextualPropertyName(string prefix, StringHash32 contextId) {
            if (contextId.IsEmpty) {
                return prefix;
            }

            return prefix + " FOR " + Find.NamedAsset<MaterialAsset>(contextId).DisplayName.ToUpperInvariant();
        }
    }
}
