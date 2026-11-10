using BeauUtil.Debugger;
using FieldDay;
using SpaceFab.Fabrication.Sequence;
using UnityEngine;

namespace SpaceFab.Fabrication {


    public class WaferDisplay : MonoBehaviour {
        public SpriteRenderer WaferTint;
        public SpriteRenderer WaferBase;
    }

    public static class WaferDisplayUtility {
        /**
        public static Color GetSemiColor() {
            // get AssetId for material the player gathered to fill the Semiconductor requirement
            MaterialAsset usedMaterial = Find.NamedAsset<MaterialAsset>(assetId);

            ResearchMaterialView view = Find.NamedAsset<ResearchMaterialView>(usedMaterial.AssetId);
            return view.AtomColor[0];
        }

        public static Color GetMetalColor() {
            // get AssetId for material the player gathered to fill the Metal requirement
            MaterialAsset usedMaterial = Find.NamedAsset<MaterialAsset>(assetId);

            ResearchMaterialView view = Find.NamedAsset<ResearchMaterialView>(usedMaterial.AssetId);
            return view.AtomColor[0];
        }
        **/

        public static WaferDisplayData LookupWaferDisplay(SequenceChunk layer, SequenceStepID step) {
            WaferDisplayLookup lookup = Find.GlobalAsset<WaferDisplayLookup>();
            foreach (WaferDisplayData data in lookup.WaferDisplayEntries) {
                if (data.Layer.Equals(layer) && data.Step.Equals(step)) {
                    return data;
                }
            }
            Log.Error("[WaferDisplayUtility] No wafer display data found for state {0}, {1}!", layer.ToString(), step.ToString());
            return new WaferDisplayData();
        }

        // TODO: get initial color (raw base, semi tint)
        // TODO: get gathered material color for tint
        // TODO: mark final step (metal, sputtered) as "TintOnTop"   
        public static void UpdateWaferDisplay(WaferDisplay display, FabricationStep stepCompleted) {
            WaferDisplayData data = LookupWaferDisplay(stepCompleted.Chunk, stepCompleted.StepId);
            UpdateWaferDisplay(display, data.BaseSprite, data.TintSprite, new Color(0.3f, 0.3f, 0.3f), false);
        }

        public static void UpdateWaferDisplay(WaferDisplay display, Sprite baseSprite, Sprite tintSprite, Color32 tintColor, bool tintOnTop) {
            display.WaferBase.sprite = baseSprite;
            display.WaferTint.sprite = tintSprite;
            display.WaferTint.color = tintColor;
            if (tintOnTop) {
                display.WaferTint.sortingOrder = -4;
            } else {
                display.WaferBase.sortingOrder = -2;
            }

        }
    }
}