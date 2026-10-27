using BeauRoutine.Internal;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Systems;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Materials;
using SpaceFab.Research;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class ShipListMaterialPreviewSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&UpdateMaterialPreviews, new SysUpdate(GameLoopPhase.LateUpdate, 200, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadShared<SupplyRouteCollection>()
                    .ReadShared<SupplyRouteDrawingState>());
        }

        static private unsafe void UpdateMaterialPreviews(float dt) {
            Find.State(out SupplyRouteCollection routes, out SupplyRouteDrawingState draw);
            ShipListPanel shipList = Find.Panel<ShipListPanel>();

            foreach(var routeIndex in routes.UpdatedRouteMask) {
                ShipListRow row = shipList.Rows[routeIndex];
                if (draw.RouteIndex == routeIndex && draw.PreviewDirty) {
                    continue;
                }

                SupplyRouteStats routeStats = routes.RouteStats[routeIndex];
                for(int i = 0; i < routeStats.MaterialCount; i++) {
                    StringHash32 materialId = new StringHash32(routeStats.MaterialHashes[i]);
                    MaterialAsset materialView = Find.NamedAsset<MaterialAsset>(materialId);
                    Image materialDisplay = row.SlotMaterials[i];
                    materialDisplay.sprite = materialView.GemSprite;
                    materialDisplay.color = Color.white;
                    materialDisplay.enabled = true;
                }

                for(int i = routeStats.MaterialCount; i < row.SlotMaterials.Length; i++) {
                    row.SlotMaterials[i].enabled = false;
                }

                routeStats.Time = Math.Min(routeStats.Time, SupplyRouteStats.MaxTime);

                row.StatsLayer.Cost.SetValue(routeStats.Cost);
                row.StatsLayer.Time.SetValue(routeStats.Time);
                row.StatsLayer.Risk.SetValue(routeStats.Risk);
            }

            if (draw.RouteIndex >= 0 && draw.PreviewDirty) {
                SupplyRouteStats absStats = routes.RouteStats[draw.RouteIndex];
                SupplyRouteStats previewStats = routes.TempRouteStats;
                ShipListRow row = shipList.Rows[draw.RouteIndex];

                int totalMaterials = Math.Max(absStats.MaterialCount, previewStats.MaterialCount);
                for(int i = 0; i < totalMaterials; i++) {
                    uint current = absStats.MaterialHashes[i];
                    uint preview = previewStats.MaterialHashes[i];

                    Image materialDisplay = row.SlotMaterials[i];
                    MaterialAsset materialView;

                    Color previewColor = Color.white;

                    Assert.True(preview != 0 || current != 0);
                    if (preview == current) {
                        materialView = Find.NamedAsset<MaterialAsset>(new StringHash32(current));
                    } else if (preview == 0) {
                        materialView = Find.NamedAsset<MaterialAsset>(new StringHash32(current));
                        previewColor = Color.gray;
                    } else {
                        materialView = Find.NamedAsset<MaterialAsset>(new StringHash32(preview));
                        previewColor = Color.white.WithAlpha(0.8f);
                    }

                    materialDisplay.sprite = materialView.GemSprite;
                    materialDisplay.color = previewColor;
                    materialDisplay.enabled = true;
                }

                for(int i = totalMaterials; i < row.SlotMaterials.Length; i++) {
                    row.SlotMaterials[i].enabled = false;
                }

                previewStats.Time = Math.Min(previewStats.Time, SupplyRouteStats.MaxTime);

                row.StatsLayer.Cost.SetValue(previewStats.Cost);
                row.StatsLayer.Time.SetValue(previewStats.Time);
                row.StatsLayer.Risk.SetValue(previewStats.Risk);
            }
        }
    }

    static public partial class SupplyChainUtility {
    }
}