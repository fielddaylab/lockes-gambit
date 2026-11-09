using BeauPools;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using SpaceFab.Materials;
using SpaceFab.Research;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SpaceFab.Supply {
    /// <summary>
    /// Rebuilds the shopping list's row column for the current contract.
    /// Frees prior rows, allocs one per required material property, fills
    /// each row's slot with a gathered material that satisfies it (player-
    /// confirmed knowledge only), lays them out vertically, and resizes the
    /// panel to fit. Mirrors WikiCharacteristicsLoadUtility's shape.
    /// </summary>
    public static class ShoppingListLoadUtility {
        // TODO: migrate to use our custom Layout elements to handle this layout
        // Padding (px) added above and below the row column when resizing
        // the panel.
        private const float OverlayPadding = 16f;

        // TODO: migrate to use our custom Layout elements to handle this layout
        // Gap (px) between adjacent rows.
        private const float RowGap = 4f;

        // Reused scratch buffers — single-threaded ECS, so static reuse
        // avoids per-rebuild allocation.
        private static readonly HashSet<StringHash32> s_CollectedScratch = new HashSet<StringHash32>();
        private static readonly HashSet<StringHash32> s_ReservedScratch = new HashSet<StringHash32>();
        //private static readonly List<StringHash32> s_FulfillScratch = new List<StringHash32>(4);

        // Frees any prior rows, then builds one row per requirement in the
        // current contract. No contract => zero rows + minimum panel size.
        public static void Rebuild(ShoppingListLayoutState layout, SupplyRouteCollection routes, PlayerProgressState progressState, ContractState contractState) {
            if (layout == null || layout.Rows == null)
            {
                Log.Msg("[SupplyBug] crucial element is null");
                return;
            }

            // 1. Free prior rows.
            ResetRows(layout);

            // 2. Resolve the current contract.
            layout.LastContractId = contractState.ContractId;
            ContractDef contract = ResolveCurrentContract(contractState);
            if (contract == null || contractState.CachedRequirements.Rows == null) {
                ResizePanel(layout, 0f);

                Log.Msg("[SupplyBug] contract is null");
                return;
            }

            // 3. Gather the material ids the player has collected across all finalized routes.
            GatherCollectedMaterials(s_CollectedScratch, routes);
            SupplyRequirementUtility.FindReservedDopants(contractState.CachedRequirements,
                progressState.MaterialProperties, s_CollectedScratch, s_ReservedScratch);


            // 4. Evaluate each table row and add row to shopping list.
            bool omitInsulator = SupplyRequirementUtility.IsInsulatorOmitted(contract);
            ContractRequirementResolver.RequirementsTable table = contractState.CachedRequirements;
            Find.GlobalAsset(out ResearchObservationChipAssets chipAssets);

            int totalSuccess = 0;
            int totalChecks = 0;

            for (int r = 0; r < table.Rows.Count; r++) {
                ContractRequirementResolver.MaterialRow tableRow = table.Rows[r];
                if (SupplyRequirementUtility.IsRowOmitted(tableRow, omitInsulator)) {
                    continue;
                }

                SupplyRequirementUtility.RowEval eval = SupplyRequirementUtility.EvaluateRow(table, progressState.MaterialProperties, tableRow, s_CollectedScratch, s_ReservedScratch);

                AddPrimaryRow(layout, tableRow, eval, chipAssets);
                totalChecks++;
                if (eval.PrimaryFound) totalSuccess++;

                if ((tableRow.Dopants & ContractRequirementResolver.DopantRows.N) != 0) {
                    AddDopantRow(layout, MaterialPropertyLabel.NDopantFor, eval.NFound, eval.N);
                    totalChecks++;
                    if (eval.NFound) totalSuccess++;
                }
                if ((tableRow.Dopants & ContractRequirementResolver.DopantRows.P) != 0) {
                    AddDopantRow(layout, MaterialPropertyLabel.PDopantFor, eval.PFound, eval.P);
                    totalChecks++;
                    if (eval.PFound) totalSuccess++;
                }
            }

            bool success = totalSuccess == totalChecks;
            layout.ShoppingListLabel.text = $"Material Requirements ({totalSuccess}/{totalChecks})";
            bool confirmActive = success && routes.TempRouteIndex < 0;

            layout.ConfirmButton.gameObject.SetActive(confirmActive);
            if (confirmActive) {
                Find.State(out SupplyRouteDrawingState drawing);

                layout.ConfirmButton.TextGraphic.text = "CONFIRM";

                SupplyProgressMeterUtility.ComputeAggregate(routes, drawing, out int risk, out int cost, out int time, out int activeMask);
                bool buttonEnabled = true;
                if (time > contractState.ContractDefinition.ExpectedDuration()) {
                    layout.ConfirmButton.TextGraphic.text = "OUT OF TIME";
                    buttonEnabled = false;
                }

                int maxCost = contractState.ContractDefinition.Payout() - contractState.ContractDefinition.MinimumProfit();
                if (cost > maxCost) {
                    layout.ConfirmButton.TextGraphic.text = "OUT OF MONEY";
                    buttonEnabled = false;
                }

                layout.ConfirmButton.Interactable = buttonEnabled;
            }

            // 5. Lay out + resize.
            UpdateExpandedY(layout);
        }

        private static void UpdateExpandedY(ShoppingListLayoutState layout)
        {
            layout.ExpandedYValue = layout.CollapseYValue + layout.ActiveRowCount * layout.RowStep;
        }

        // Resolves the player's active contract through its contract-assets
        // wrapper. Null when no contract is loaded.
        private static ContractDef ResolveCurrentContract(ContractState contractState) {
            return contractState.ContractDefinition;
        }

        // True if this requirement is in the contract's Supply omit list,
        // matched by asset reference. Null/empty omit list => nothing omitted.
        private static bool IsOmittedFromSupply(MaterialPropertyCheck check, MaterialPropertyCheck[] omitted) {
            if (omitted == null) return false;
            for (int i = 0; i < omitted.Length; i++) {
                if (ReferenceEquals(omitted[i], check)) return true;
            }
            return false;
        }

        // Allocs one row for the given requirement, sets its property chip
        // label/icon, and fills its slot with the first gathered material
        // that satisfies the check (by player-confirmed knowledge).
        private static ShoppingListRow AllocRow(ShoppingListLayoutState layout) {
            Assert.True(layout.ActiveRowCount < layout.Rows.Length, "Contract needs more shopping list rows than setup");

            ShoppingListRow row = layout.Rows[layout.ActiveRowCount++];
            row.gameObject.SetActive(true);
            return row;
        }

        private static void SetLabelOffset(ShoppingListRow row, bool hasSubLabel) {
            RectTransform rect = row.MaterialLabel.rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, hasSubLabel ? -5f : 0f);
        }

        // Add Conductor / Insulator / Semiconductor row
        private static void AddPrimaryRow(ShoppingListLayoutState layout, ContractRequirementResolver.MaterialRow tableRow,
            SupplyRequirementUtility.RowEval eval, ResearchObservationChipAssets chipAssets) {
            ShoppingListRow row = AllocRow(layout);
            if (row == null) {
                return;
            }

            using (PooledStringBuilder labelBuilder = PooledStringBuilder.Create())
            using (PooledStringBuilder subLabelBuilder = PooledStringBuilder.Create()) {
                ContractRequirementResolver.GenerateLabel(tableRow.Properties, labelBuilder, subLabelBuilder,
                    out MaterialPropertyLabel baseLabel, chipAssets, out Sprite _);

                ObservationType type = MaterialObservationChamberLookup.GetChamberType(baseLabel);
                row.SetProperty(labelBuilder.Builder.ToString(), type);
                row.SecondaryLabel.text = subLabelBuilder.Builder.ToString();
                SetLabelOffset(row, subLabelBuilder.Builder.Length > 0);
            }

            row.SetSlot(eval.PrimaryFound ? ResolveMaterialIcon(eval.Primary) : null);
        }

        // Add N / P dopant row
        private static void AddDopantRow(ShoppingListLayoutState layout, MaterialPropertyLabel label, bool found, StringHash32 dopantId) {
            ShoppingListRow row = AllocRow(layout);
            if (row == null) {
                return;
            }

            ObservationType type = MaterialObservationChamberLookup.GetChamberType(label);
            row.SetProperty(MaterialPropertyLabelDisplay.GetPropertyName(label), type);
            SetLabelOffset(row, false);

            row.SetSlot(found ? ResolveMaterialIcon(dopantId) : null);
        }
        
        // private static bool AddRow(ShoppingListLayoutState layout, PlayerProgressState progress, ContractState contractState, MaterialPropertyCheck check) {
        //     if (layout.ActiveRowCount >= layout.Rows.Length) {
        //         Debug.LogError("Contract needs more shoppinglist rows than setup in prefab!");
        //         return false;
        //     }

        //     ShoppingListRow row = layout.Rows[layout.ActiveRowCount++];
        //     row.gameObject.SetActive(true);

        //     string label = MaterialPropertyLabelDisplay.GetPropertyName(check.Label);
        //     ObservationType type = MaterialObservationChamberLookup.GetChamberType(check.Label);
        //     row.SetProperty(label, type);

        //     bool found;
        //     StringHash32 materialId;
        //     if (check.Label == MaterialPropertyLabel.NDopantFor || check.Label == MaterialPropertyLabel.PDopantFor) {
        //         found = SupplyRequirementUtility.TryFindGatheredDopant(contractState.CachedRequirements,
        //             progress.MaterialProperties, s_CollectedScratch, check.Label, out materialId);
        //     } else {
        //         ContractRequirementResolver.PropertyLabelList props = default;
        //         props.Add(check.Label);
        //         found = SupplyRequirementUtility.TryFindGatheredMaterial(contractState.CachedRequirements,
        //             progress.MaterialProperties, s_CollectedScratch, props, out materialId);
        //     }

        //     row.SetSlot(found ? ResolveMaterialIcon(materialId) : null);
        //     return found;
        // }

        // Resolves a material id to its shopping-list slot sprite via the
        // material's ResearchMaterialView (same source Supply uses for node
        // icons). Null if no view is registered.
        private static Sprite ResolveMaterialIcon(StringHash32 materialId) {
            MaterialAsset material = Find.NamedAsset<MaterialAsset>(materialId);
            if (material == null) return null;
            return material.GemSprite;
        }

        // Collects every non-empty material hash across all finalized routes
        // into output (cleared first). Reads the fixed MaterialHashes buffer
        // on each route's stats, so it must run unsafe.
        private static unsafe void GatherCollectedMaterials(HashSet<StringHash32> output, SupplyRouteCollection routes) {
            output.Clear();
            if (routes == null || routes.RouteStats == null) return;

            for (int shipIdx = 0; shipIdx < routes.RouteStats.Length; shipIdx++) {
                if (shipIdx == routes.TempRouteIndex) {
                    continue;
                }

                // Copy to a stack local so the fixed buffer can be read without
                // pinning the heap array element.
                SupplyRouteStats stats = routes.RouteStats[shipIdx];
                for (int matIdx = 0; matIdx < SupplyRouteData.MaxCapacity; matIdx++) {
                    uint hash = stats.MaterialHashes[matIdx];
                    if (hash == 0) continue;
                    output.Add(new StringHash32(hash));
                }
            }
        }

        // TODO: migrate to use our custom Layout elements to handle this layout
        // Lays the rows out vertically, centered on the container's local
        // Y=0, row 0 at the top. Returns the total content height (sum of
        // row heights + gaps).
        private static float LayoutVerticalCentered(List<ShoppingListRow> rows, float gap) {
            if (rows == null || rows.Count == 0) return 0f;

            // 1. Sum heights (including gaps).
            float totalHeight = 0f;
            for (int i = 0; i < rows.Count; i++) {
                RectTransform rect = rows[i] != null ? rows[i].transform as RectTransform : null;
                totalHeight += rect != null ? rect.rect.height : 0f;
            }
            totalHeight += gap * (rows.Count - 1);

            // 2. Walk top-to-bottom from +totalHeight/2, centering each row.
            float cursor = totalHeight * 0.5f;
            for (int i = 0; i < rows.Count; i++) {
                RectTransform rect = rows[i] != null ? rows[i].transform as RectTransform : null;
                if (rect == null) continue;
                float height = rect.rect.height;
                Vector3 pos = rect.anchoredPosition3D;
                pos.y = cursor - height * 0.5f;
                rect.anchoredPosition3D = pos;
                cursor -= height + gap;
            }

            return totalHeight;
        }

        // Resizes the panel's height to fit the row column plus padding.
        private static void ResizePanel(ShoppingListLayoutState layout, float contentHeight) {
            // if (layout.PanelRect == null) return;
            // Vector2 size = layout.PanelRect.sizeDelta;
            // size.y = contentHeight + 2f * OverlayPadding;
            // layout.PanelRect.sizeDelta = size;
        }

        // Returns every pooled row to the pool and clears the active list.
        public static void ResetRows(ShoppingListLayoutState layout) {
            for (int i = 0; i < layout.Rows.Length; i++) {
                if (layout.Rows[i] != null) {
                    layout.Rows[i].gameObject.SetActive(false);
                }
            }
            layout.ActiveRowCount = 0;
        }
    }
}
