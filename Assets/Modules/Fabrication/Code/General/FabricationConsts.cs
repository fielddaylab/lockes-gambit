using BeauUtil;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication
{
    public static class FabricationConsts
    {
        // converts time elapsed during fabrication attempt (in seconds) into overarching cycles
        public static int CyclesPerSecond;

        // Well-known station ids. Authored on station prefabs via MicrogameStationInterfacer.m_Id.
        // The Defrag station is universal: visiting it does not advance the sequence or run an
        // alignment check; it clears the glitch flag on the current step's card.
        public static readonly StringHash32 DEFRAG_STATION_ID = "station:defrag";

        public static readonly StringHash32 FURNACE_STATION_ID = "station:furnace";
        public static readonly StringHash32 RESIST_STATION_ID = "station:resist";
        public static readonly StringHash32 PHOTOLITHOGRAPHY_STATION_ID = "station:photolithography";
        public static readonly StringHash32 ETCH_STATION_ID = "station:etch";
        public static readonly StringHash32 SPUTTER_STATION_ID = "station:sputter";
        public static readonly StringHash32 ION_STATION_ID = "station:ion";

        // Duration of the checkpoint-rollback lead-in pause, in seconds. Game-wide constant;
        // consumed by RestoreLeadIn. Tunable later if the visual treatment demands a different feel.
        public static readonly float CHECKPOINT_LEAD_IN_SECONDS = 1.0f;

        #region Input Mappings

        // World Interact

        public const KeyCode Up0 = KeyCode.W;
        public const KeyCode Up1 = KeyCode.UpArrow;

        public const KeyCode Down0 = KeyCode.S;
        public const KeyCode Down1 = KeyCode.DownArrow;

        public const KeyCode Activate = KeyCode.Space;

        // Skips the post-microgame process animation during ExitingMicrogame. Reuses the same
        // physical key as Down0 (Cancel) intentionally — Cancel is consumed only during
        // InMicrogame while Skip is consumed only during ExitingMicrogame, so the two never
        // overlap in time.
        public const KeyCode Skip = KeyCode.S;

        // Movement

        public const KeyCode Left0 = KeyCode.A;
        public const KeyCode Left1 = KeyCode.LeftArrow;

        public const KeyCode Right0 = KeyCode.D;
        public const KeyCode Right1 = KeyCode.RightArrow;

        #endregion // Input Mappings
    }

    public static class FabricationScriptTriggers
    {
        // Fired when a microgame signals completion, before any exit animation. A Leaf trigger node
        // responds and calls RequireMicrogamePrecision(threshold) to gate the exit on precision.
        // Table vars: "microgame" (station id string), "precision" (the result precision in [0,1]).
        public static readonly StringHash32 OnMicrogameCompleted = "OnFabMicrogameCompleted";
        public static readonly StringHash32 OnMicrogameExited = "OnFabMicrogameExited";
        public static readonly StringHash32 OnArriveAtStation = "OnFabArriveAtStation";
        public static readonly StringHash32 OnPreAttempt = "OnFabPreAttempt";
        public static readonly StringHash32 OnAttempt = "OnFabAttempt";

        public static readonly StringHash32 OnMicrogameEntered = "OnMicrogameEntered";

        public static readonly StringHash32 OnStunned = "OnFabStunned";
    }
}