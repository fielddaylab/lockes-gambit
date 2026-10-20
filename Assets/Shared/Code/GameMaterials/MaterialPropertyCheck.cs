using BeauUtil;
using FieldDay.Assets;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Materials
{
    public enum MaterialPropertyLabel : byte
    {
        // Observation block. Non-persistent: evidence collected in chambers,
        // never stored in PlayerProgressState. Add new observation values to
        // the end of this block to keep existing serialized assets stable.
        Conductive,
        NonConductive,

        HeatActivated,
        HeatDeactivated,
        HeatUnaffected,
        DEPRECATED_HeatVulnerable,
        HeatResistant,

        AtomicRadiusCompliant,
        ValenceOneLessThan,
        ValenceOneMoreThan,
        //FormsDiodeWithKnownNIn,
        //FormsDiodeWithKnownPIn,
        //IncreasesConductivityOf,
        //DoesNotIncreaseConductivityOf,

        LightEmitting,
        HighMobility,
        VoltageResistant,

        // Persistent property block. Confirmable; round-trips to
        // PlayerProgressState. Bit positions assigned by
        // MaterialPropertyLabelUtility.GetStaticBitIndex (for static labels);
        // dynamic labels use MaterialOrderAsset indexing instead.
        ConductorNaive,
        InsulatorNaive,
        Insulator,
        Conductor,
        Semiconductor,
        HiTempConductor,
        HiTempSemiConductor,
        PDopantFor,
        NDopantFor,
        LightEmittingSemiconductor,
        HighVoltageSemiconductor,
        HighMobilitySemiconductor,
    }

    public enum ChamberType
    { 
        Electrical,
        Thermal,
        Dopant,
    }

    public enum ObservationType
    {
        Electrical,
        Thermal,
        Dopant,
        Special,
        ConfirmedProperty,
        Component,
    }

    [CreateAssetMenu(menuName = "SpaceFab/Material Property")]
    public class MaterialPropertyCheck : ScriptableObject
    {
        public MaterialPropertyLabel Label;
        [AssetName(typeof(MaterialAsset))] public StringHash32 InComparisonTo;
    }

    /// <summary>
    /// Hardcoded observation-label → ObservationType lookup. Pure
    /// compile-time switch — no registry, no asset, no allocation. Used
    /// by the hypothesis decomposition + the observation chip widget to
    /// know which sprite pair an observation chip should render with.
    /// Returns ObservationType (not ChamberType) because some observation
    /// buckets (Special, ConfirmedProperty) don't correspond to a
    /// chamber.
    /// </summary>
    public static class MaterialObservationChamberLookup
    {
        public static ObservationType GetChamberType(MaterialPropertyLabel label)
        {
            if (label < MaterialPropertyLabel.HeatActivated || label == MaterialPropertyLabel.VoltageResistant)
            {
                return ObservationType.Electrical;
            }
            else if (label < MaterialPropertyLabel.AtomicRadiusCompliant)
            {
                return ObservationType.Thermal;
            }
            else if (label < MaterialPropertyLabel.LightEmitting)
            {
                return ObservationType.Dopant;
            }
            else if (label < MaterialPropertyLabel.ConductorNaive)
            {
                return ObservationType.Special;
            }
            else
            {
                return ObservationType.ConfirmedProperty;
            }
        }
    }

    /**
    /// <summary>
    /// Lighter footprint than MaterialPropertyCheck
    /// </summary>
    public class MaterialProperty
    {
        public MaterialPropertyLabel Label;
        public MaterialObservation[] RequiredObservations;
        [AssetName(typeof(MaterialAsset))] public StringHash32 InComparisonTo;

        public MaterialProperty(MaterialPropertyLabel label, MaterialObservation[] requiredObservations)
        {
            Label = label;
            RequiredObservations = requiredObservations;
            InComparisonTo = StringHash32.Null;
        }

        public MaterialProperty(MaterialPropertyLabel label, MaterialObservation[] requiredObservations, StringHash32 inComparisonTo)
        {
            Label = label;
            RequiredObservations = requiredObservations;
            InComparisonTo = inComparisonTo;
        }
    }
    */
}