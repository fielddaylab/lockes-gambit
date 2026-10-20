using System;
using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class SupplyResultPanel : SharedStateComponent, IScenePreload {
        public ResultsPanelLayout PanelLayout;
        public RiskInlayPanel RiskPanel;
        public GuiCounter TimeCounter;
        public GuiCounter RevenueCounter;
        
        public IEnumerator<WorkSlicer.Result?> Preload() {
            PanelLayout.Contract.ContextContinueButton.OnClick.Register(Commit);
            return null;
        }

        private void Commit() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.Confirmed;
        }

        public void Show() {
            Find.State(out SupplyRouteCollection routes, out SupplyMinigameState minigameState, out SupplyShipIndex ships, out SupplyRouteDrawingState draw);

            int cost = 0,
                time = 0,
                risk = 0;

            for (int i = 0; i < ships.ShipCount; i++) {
                cost += routes.RouteStats[i].Cost;
                time = Math.Max(time, routes.RouteStats[i].Time);
                risk += routes.RouteStats[i].Risk;
            }

            minigameState.Cost = cost;
            minigameState.TotalCycles = time;
            minigameState.Risk = risk;

            minigameState.FoundValidSolution = true;

            // Refresh the route mirror before exporting - ExportState writes it out, and it still
            // holds whatever was imported at scene entry until something captures the live routes.
            SupplyRouteSaveUtility.Capture(routes, draw, ships, minigameState);

            ref SupplySaveState saveState = ref Find.State<MinigameSaveStates>().Supply;
            SupplyStateUtility.ExportState(ref saveState, minigameState);

            RevenueCounter.SetValue(cost);
            TimeCounter.SetValue(time);
            RiskPanel.SetRisk(risk);

            Find.State(out ChapterState chapterState);

            if (risk <= 0) {
                RiskPanel.SetText(RiskPanel.DefaultNoRiskHeader, RiskPanel.DefaultNoRiskDescription);
            } else {
                string hazardDescription = chapterState.ChapterDefinition.HazardDescription;
                if (string.IsNullOrEmpty(hazardDescription)) {
                    hazardDescription = RiskPanel.DefaultRiskDescription;
                }
                string hazardHeader = chapterState.ChapterDefinition.HazardHeader;
                RiskPanel.SetText(hazardHeader, hazardDescription);
            }

            PanelLayout.Prepare();
            PanelLayout.AnimationRoutine.Replace(this, DisplayRoutine(this));
        }

        static private IEnumerator DisplayRoutine(SupplyResultPanel resultPanel) {
            yield return resultPanel.PanelLayout.FadeIn();
            yield return 0.1f;
            yield return Routine.Combine(
                resultPanel.PanelLayout.FadeInContextPanel()
            );
            yield return 0.5f;

            yield return TimeMeterStyle.DrainIntoMeter(resultPanel.TimeCounter, resultPanel.PanelLayout.Contract.TimeIndicators);
            yield return MoneyMeterStyle.DrainIntoMeter(resultPanel.RevenueCounter, resultPanel.PanelLayout.Contract.RevenueIndicators);
            resultPanel.PanelLayout.Finish();
        }
    }
}