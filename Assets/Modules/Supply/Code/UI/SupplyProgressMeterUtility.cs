using BeauRoutine;
using FieldDay;
using System.Collections;
using TMPro;
using UnityEngine;

namespace SpaceFab.Supply {
    /// <summary>
    /// Builds the Supply mini progress meter from the route collection and drives its
    /// expand/collapse transition. Aggregates Risk (sum), Cost (sum, against contract funds),
    /// and Time (max) across all ships, honoring the pending route currently being drawn
    /// (TempRouteStats for the drawing index, RouteStats for everything else).
    /// </summary>
    public static class SupplyProgressMeterUtility {
        // Expand/collapse fade duration (seconds).
        private const float TransitionDuration = 0.2f;

        #region Aggregate

        // Computes the aggregate risk/cost/time and the active-ship bitmask, applying the
        // temp-route override for the index currently being drawn.
        public static void ComputeAggregate(SupplyRouteCollection routes, SupplyRouteDrawingState drawing, out int risk, out int cost, out int time, out int activeMask) {
            risk = 0;
            cost = 0;
            time = 0;
            activeMask = 0;
            if (routes == null || routes.RouteStats == null || routes.Routes == null) {
                return;
            }

            for (int i = 0; i < SupplyRouteData.MaxShips; i++) {
                SupplyRouteStats stats = GetEffectiveStats(routes, drawing, i, out bool active);
                if (!active) {
                    continue;
                }
                activeMask |= 1 << i;
                risk += stats.Risk;
                cost += stats.Cost;
                if (stats.Time > time) {
                    time = stats.Time;
                }
            }
        }

        // Returns the stats that should represent ship i right now: the temp (pending) stats
        // when i is the route being drawn, otherwise the finalized stats. `active` reports
        // whether that route has any nodes.
        private static SupplyRouteStats GetEffectiveStats(SupplyRouteCollection routes, SupplyRouteDrawingState drawing, int i, out bool active) {
            bool tempInUse = drawing != null && drawing.RouteIndex >= 0;
            if (tempInUse && drawing.RouteIndex == i) {
                active = routes.TempRouteBuffer.NodeCount > 0;
                return routes.TempRouteStats;
            }
            active = routes.Routes[i].NodeCount > 0;
            return routes.RouteStats[i];
        }

        #endregion // Aggregate

        #region Refresh

        // Rebuilds the aggregate panel cells and the per-ship breakdown rows from the current
        // (pending-aware) route state.
        public static void Refresh(SupplyProgressMeterLayoutState layout, SupplyRouteCollection routes, SupplyRouteDrawingState drawing, SupplyShipIndex ships, ContractState contract) {
            if (layout == null) {
                return;
            }
            SupplyProgressMeterSpriteSet sprites = Find.GlobalAsset<SupplyProgressMeterSpriteSet>();
            if (sprites == null) {
                return;
            }

            ComputeAggregate(routes, drawing, out int risk, out int cost, out int time, out int activeMask);
            int funds = ResolvePayout(contract);
            int expectedDuration = ResolveExpectedDuration(contract);
            int minProfit = ResolveMinimumProfit(contract);

            // Aggregate "Result" panel.
            if (layout.AggregateView != null) {
                FillUniform(layout.AggregateView.RiskCells, risk, sprites.RiskBase, sprites.RiskFilled, sprites.RiskColor);
                FillAggregateTime(layout.AggregateView.TimeCells, time, expectedDuration, sprites);
                FillAggregateCost(layout.AggregateView.CostCells, funds, cost, minProfit, sprites);
            }
        }

        // Reads the active contract's payout via its asset wrapper (the funds the cost meter
        // counts down from). 0 when no contract is loaded.
        private static int ResolvePayout(ContractState contractState) {
            if (!contractState.ContractDefinition) {
                return 0;
            }
            return contractState.ContractDefinition.Payout();
        }

        private static int ResolveExpectedDuration(ContractState contractState) {
            if (!contractState.ContractDefinition) {
                return int.MaxValue;
            }
            return contractState.ContractDefinition.ExpectedDuration();
        }

        private static int ResolveMinimumProfit(ContractState contractState) {
            if (!contractState.ContractDefinition) {
                return 0;
            }
            return contractState.ContractDefinition.MinimumProfit();
        }

        #endregion // Refresh

        #region Cell rendering

        // Fills the first `count` cells with the given overlay sprite/color, clears the rest.
        // Every cell's base is set to baseSprite so empty cells share one look.
        private static void FillUniform(ProgressMeterCell[] cells, int count, Sprite baseSprite, Sprite sprite, Color color) {
            if (cells == null) {
                return;
            }
            for (int i = 0; i < cells.Length; i++) {
                ApplyBase(cells[i], baseSprite);
                ApplyCell(cells[i], i < count, sprite, color);
            }
        }

        // Aggregate cost: total filled cells = contract funds (clamped to authored length).
        // The trailing `cost` of those are red (spent); the leading remainder are yellow.
        private static void FillAggregateCost(ProgressMeterCell[] cells, int funds, int cost, int minProfit, SupplyProgressMeterSpriteSet sprites) {
            int length = cells != null ? cells.Length : 0;
            int filled = Mathf.Clamp(funds, 0, length);
            int red = Mathf.Clamp(cost, 0, filled);
            int yellow = filled - red;
            FillCostBars(cells, yellow, red, minProfit, sprites);
        }

        private static void FillAggregateTime(ProgressMeterCell[] cells, int time, int expectedDuration, SupplyProgressMeterSpriteSet sprites) {
            if (cells == null)
            {
                return;
            }
            
            for (int i = 0; i < cells.Length; i++)
            {
                ApplyBase(cells[i], sprites.TimeBase);
                Sprite overlay = i >= expectedDuration ? sprites.TimeOverSpent : sprites.TimeFilled;
                ApplyCell(cells[i], i < time, overlay, Color.white);
            }
        }

        // Lays out `yellowCount` remaining bars then `redCount` spent bars, clearing the rest.
        // Counts beyond the authored cell length are naturally clamped by the loop. Every
        // cell's base is set to CostBase so empty cells share one look.
        private static void FillCostBars(ProgressMeterCell[] cells, int yellowCount, int redCount, int minProfit, SupplyProgressMeterSpriteSet sprites) {
            if (cells == null) {
                return;
            }
            for (int i = 0; i < cells.Length; i++) {
                ApplyBase(cells[i], sprites.CostBase);
                if (i < yellowCount) {
                    ApplyCell(cells[i], true, sprites.CostFilled, Color.white);
                } else if (i < yellowCount + redCount) {
                    bool over = i < minProfit;
                    Sprite spent = over ? sprites.CostOverSpent : sprites.CostSpent;
                    Sprite xMark = over ? sprites.CostOverSpentX : sprites.CostSpentX;
                    ApplyCell(cells[i], true, spent, Color.white, xMark);
                } else {
                    ApplyCell(cells[i], false, null, default);
                }
            }
        }

        // Writes a section's numeric count into its TMP label (per-ship breakdown rows).
        private static void SetCount(TMP_Text text, int count) {
            if (text == null) {
                return;
            }
            text.text = count.ToString();
        }

        // Sets a cell's always-visible base sprite (the empty-cell look). A null base sprite
        // means this section has no authored empty-cell look, so the base image is hidden.
        private static void ApplyBase(ProgressMeterCell cell, Sprite baseSprite) {
            if (cell == null || cell.BaseImage == null) {
                return;
            }
            if (baseSprite == null) {
                cell.BaseImage.enabled = false;
                return;
            }
            cell.BaseImage.sprite = baseSprite;
            cell.BaseImage.enabled = true;
        }

        // Drives one cell's overlay: enabled with sprite+color when filled, hidden otherwise.
        private static void ApplyCell(ProgressMeterCell cell, bool filled, Sprite sprite, Color color, Sprite xMarkSprite = null) {
            if (cell == null || cell.OverlayImage == null) {
                return;
            }
            if (filled && sprite != null) {
                cell.OverlayImage.sprite = sprite;
                cell.OverlayImage.color = color;
                cell.OverlayImage.enabled = true;
                SetXMark(cell, xMarkSprite);
            } else {
                cell.OverlayImage.enabled = false;
                SetXMark(cell, null);
            }
        }

        private static void SetXMark(ProgressMeterCell cell, Sprite xMarkSprite) {
            if (cell.xMarkImage == null) {
                return;
            }
            if (xMarkSprite == null) {
                cell.xMarkImage.enabled = false;
                return;
            }
            cell.xMarkImage.sprite = xMarkSprite;
            cell.xMarkImage.enabled = true;
        }

        #endregion // Cell rendering
    }
}
