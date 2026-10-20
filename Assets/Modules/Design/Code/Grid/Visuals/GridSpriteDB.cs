using FieldDay.Assets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Design.Visuals
{
    [CreateAssetMenu(menuName = "SpaceFab/Design/Grid Sprite DB")]
    public class GridSpriteDB : GlobalAsset
    {
        [Header("Metal")]
        public Sprite Metal;
        public PathLibrary MetalLibrary;

        [Header("Transistors")]
        public Sprite Transistor;
        public PathLibrary TransistorLibrary;
        public Color NColor;
        public Color PColor;
        public Sprite NSide;
        public Sprite PSide;
        public Sprite InvertedOverlay;
        public Sprite InvertedOverlayBase;

        [Header("Vias")]
        public Sprite Via;
        public Sprite ViaBase;
        public Sprite ViaHi;
        public Sprite ViaLo;
        public Sprite ViaUnstable;

        [Header("Gates")]
        public Sprite Gate;
        public Sprite GateHi;
        public Sprite GateLo;

        [Header("IO")]
        public Sprite InputConstantHigh;
        public Sprite InputConstantLow;

        [Header("Flow")]
        public Sprite FlowHiAbove;
        public Sprite FlowLoAbove;
        public Sprite FlowUnstableAbove;
        public Sprite FlowHiBelow;
        public Sprite FlowLoBelow;
        public Sprite FlowUnstableBelow;

        [Header("Input Toggle Overlay")]
        public Sprite InputToggleBackgroundHi;
        public Sprite InputToggleBackgroundLow;
        public Sprite InputToggleArrow;
        public Color InputToggleLoColor = Color.white;
        public Color InputToggleLoTextColor = Color.white;
        public Color InputToggleHiColor = Color.white;
        public Color InputToggleHiTextColor = Color.white;

        [Header("Output Tag Overlay")]
        public Sprite OutputToggleBackground;
        // Output overlays reuse the input-toggle arrow sprites but are driven by the cell's
        // simulated flow. Hi/Lo reuse the input-toggle colors; these two buckets cover the flow
        // states the input toggle never sees. Each bucket has a fill color (background) and a text
        // color (arrow + subtype label), mirroring the input-toggle color/text-color split.
        public Sprite OutputArrow;
        public Color OutputEmptyColor = Color.white;
        public Color OutputEmptyTextColor = Color.white;
        public Color OutputUnstableColor = Color.white;
        public Color OutputUnstableTextColor = Color.white;
    }

    public static class GridSpriteDBUtility
    {
        public static Sprite LookupViaSprite(GridSpriteDB spriteDB, FlowState state)
        {
            switch (state) {
                case FlowState.Empty:
                    return spriteDB.Via;
                case FlowState.Hi:
                    return spriteDB.ViaHi;
                case FlowState.Lo:
                    return spriteDB.ViaLo;
                case FlowState.Unstable:
                    return spriteDB.ViaUnstable;
                default:
                    return null;
            }
        }

        public static Sprite LookupGateSprite(GridSpriteDB spriteDB, FlowState state)
        {
            switch (state) {
                case FlowState.Empty:
                    return spriteDB.Gate;
                case FlowState.Hi:
                    return spriteDB.GateHi;
                case FlowState.Lo:
                    return spriteDB.GateLo;
                case FlowState.Unstable:
                    return spriteDB.Gate;
                default:
                    return null;
            }
        }

        // Tint colour applied to the input-toggle overlay's tinted renderers based on the
        // current Lo/Hi state. Falls back to white for non-binary states so a misconfigured
        // entry stays visible.
        public static Color LookupInputToggleColor(GridSpriteDB spriteDB, FlowState state)
        {
            if (spriteDB == null) { return Color.white; }
            if (state == FlowState.Hi) { return spriteDB.InputToggleHiColor; }
            if (state == FlowState.Lo) { return spriteDB.InputToggleLoColor; }
            return Color.white;
        }

        public static Sprite LookupInputBackground(GridSpriteDB spriteDB, FlowState state)
        {
            if (spriteDB == null) { return spriteDB.InputToggleBackgroundHi; }
            if (state == FlowState.Hi) { return spriteDB.InputToggleBackgroundHi; }
            if (state == FlowState.Lo) { return spriteDB.InputToggleBackgroundLow; }
            return null;
        }

        public static Color LookupInputToggleTextColor(GridSpriteDB spriteDB, FlowState state)
        {
            if (spriteDB == null) { return Color.white; }
            if (state == FlowState.Hi) { return spriteDB.InputToggleHiTextColor; }
            if (state == FlowState.Lo) { return spriteDB.InputToggleLoTextColor; }
            return Color.white;
        }

        // Fill color (background) for an output overlay at the given simulated flow state. Hi/Lo
        // reuse the input-toggle palette; Empty / Unstable use the output-specific colors. Falls
        // back to white when the DB is missing so a misconfigured overlay stays visible.
        public static Color LookupOutputFlowColor(GridSpriteDB spriteDB, FlowState state)
        {
            if (spriteDB == null) { return Color.white; }
            switch (state)
            {
                case FlowState.Hi: return spriteDB.InputToggleHiColor;
                case FlowState.Lo: return spriteDB.InputToggleLoColor;
                case FlowState.Unstable: return spriteDB.OutputUnstableColor;
                default: return spriteDB.OutputEmptyColor;
            }
        }

        // Text color (arrow + subtype label) for an output overlay at the given flow state. Parallel
        // to LookupOutputFlowColor but using the text-color variant of each bucket.
        public static Color LookupOutputFlowTextColor(GridSpriteDB spriteDB, FlowState state)
        {
            if (spriteDB == null) { return Color.white; }
            switch (state)
            {
                case FlowState.Hi: return spriteDB.InputToggleHiTextColor;
                case FlowState.Lo: return spriteDB.InputToggleLoTextColor;
                case FlowState.Unstable: return spriteDB.OutputUnstableTextColor;
                default: return spriteDB.OutputEmptyTextColor;
            }
        }
    }
}

