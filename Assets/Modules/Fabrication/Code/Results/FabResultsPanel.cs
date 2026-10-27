using BeauRoutine;
using FieldDay;
using FieldDay.SharedState;
using SpaceFab.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SpaceFab.Fabrication.Sequence;
using System.Linq;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using FieldDay.Scenes;
using System.Collections.Generic;
using BeauUtil;

namespace SpaceFab.Fabrication
{
    public class FabResultsPanel : SharedStateComponent, IScenePreload {
        public ResultsPanelLayout PanelLayout;
        public FabResultsSpeedDisplay[] SpeedPanels;
        public Color FadedSpeedPanelColor;
        public GuiCounter TimeCounter;
        public GuiCounter RevenueCounter;

        [Header("Stations")]
        public DitherFadeGroup StationsFader;
        public RectTransform StationsRoot;
        public LayoutOptions StationsLayout;
        public StationResultDisplay[] Stations;

        public IEnumerator<WorkSlicer.Result?> Preload() {
            PanelLayout.Contract.ContextContinueButton.OnClick.Register(() => {
                Find.State<InterruptState>().FinalizeAttemptRequestedThisFrame = true;
            });
            StationsFader.SetAlpha(0);
            return null;
        }

        public unsafe void Show() {

            Find.State(out SequenceState sequence, out WaferState waferState, out FabricationMinigameState fabState);
            FabricationStep[] steps = sequence.Level.Sequence.Steps;

            float* stationPrecisions = stackalloc float[Stations.Length];
            float* stationCount = stackalloc float[Stations.Length];
            
            for (int i = 0; i < steps.Length; i++) {
                stationPrecisions[(int) steps[i].StepId] += waferState.StepPrecisions[i];
                stationCount[(int) steps[i].StepId]++;
            }

            for(int i = 0; i < Stations.Length; i++) {
                if (stationCount[i] == 0) {
                    Stations[i].gameObject.SetActive(false);
                    continue;
                }

                Stations[i].gameObject.SetActive(true);
                Stations[i].SetRating(stationPrecisions[i] / stationCount[i]);
            }

            using(var children = Positioning.QueryLayoutChildren(StationsRoot)) {
                Positioning.HorizontalLayout(children, StationsLayout, 0);
            }

            for(int i = 0; i < SpeedPanels.Length; i++) {
                FabResultsSpeedDisplay panel = SpeedPanels[i];
                if (i != fabState.TotalCycles - 1) {
                    panel.Icon.SetAlpha(0.5f);
                    foreach(var graphic in panel.ToFade) {
                        graphic.color = FadedSpeedPanelColor;
                    }
                }
            }

            TimeCounter.SetValue(fabState.TotalCycles);
            RevenueCounter.SetValue(fabState.TotalCost);

            PanelLayout.Prepare();
            PanelLayout.AnimationRoutine.Replace(this, DisplayRoutine(this));
        }

        static private IEnumerator DisplayRoutine(FabResultsPanel resultPanel) {
            yield return resultPanel.PanelLayout.FadeIn();
            yield return 0.1f;
            yield return Routine.Combine(
                resultPanel.PanelLayout.FadeInContextPanel(),
                Tween.ZeroToOne(resultPanel.StationsFader.SetAlpha, 0.2f).DelayBy(0.3f)
            );

            yield return 0.5f;

            yield return TimeMeterStyle.DrainIntoMeter(resultPanel.TimeCounter, resultPanel.PanelLayout.Contract.TimeIndicators);
            yield return MoneyMeterStyle.DrainIntoMeter(resultPanel.RevenueCounter, resultPanel.PanelLayout.Contract.RevenueIndicators);
            resultPanel.PanelLayout.Finish();
        }
    }
}
