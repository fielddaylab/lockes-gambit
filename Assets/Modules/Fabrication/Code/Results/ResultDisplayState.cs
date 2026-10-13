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

namespace SpaceFab.Fabrication
{
    public class ResultDisplayState : SharedStateComponent, IRegistrationCallbacks
    {
        [HideInInspector] public bool DisplayRequestedThisFrame;

        public CanvasGroup ResultsGroup;
        public Image Background;
        public RectTransform StationDisplayRow;
        public GuiButton RetryButton, ContinueButton;
        public CanvasInputLayer InputLayer;

        public ResultDisplaySection Heading, Accuracy, Time, ProductionTime;
        public StationResultDisplay[] StationResults;

        public Routine ResultsTransitionRoutine;

        public void OnDeregister()
        {
            ResultsTransitionRoutine.Stop();

            RetryButton.OnClick.RemoveAllListeners();
            ContinueButton.OnClick.RemoveAllListeners();
        }

        public void OnRegister()
        {
            ResultDisplayStateUtility.SetEnabledResultsGroup(this, false);

            RetryButton.OnClick.AddListener(OnRetryClicked);
            ContinueButton.OnClick.AddListener(OnFinalizeClicked);
        }

        public void OnRetryClicked()
        {
            Debug.Log("Retry clicked!");
            
            Find.State<InterruptState>().ResetRequestedThisFrame = true;
            ResultDisplayStateUtility.SetEnabledResultsGroup(this, false);
        }

        public void OnFinalizeClicked()
        {
            Debug.Log("Finalize clicked!");
            Find.State<InterruptState>().FinalizeAttemptRequestedThisFrame = true;
        }
    }

    public static class ResultDisplayStateUtility
    {
        public static void SetEnabledResultsGroup(ResultDisplayState displayState, bool isEnabled)
        {
            displayState.ResultsGroup.alpha = isEnabled ? 1 : 0;
            displayState.ResultsGroup.blocksRaycasts = isEnabled;
            displayState.ResultsGroup.interactable = isEnabled;

            if (isEnabled) {
                displayState.InputLayer.TryPushPriority();
            } else {
                displayState.InputLayer.TryPopPriority();
            }
        }

        public static void ShowResults(ResultDisplayState displayState)
        {
            displayState.ResultsTransitionRoutine.Replace(ShowResultsRoutine(displayState));            
        }

        public static void HideResults(ResultDisplayState displayState)
        {
            displayState.ResultsTransitionRoutine.Replace(HideResultsRoutine(displayState));
        }

        private static IEnumerator ShowResultsRoutine(ResultDisplayState displayState)
        {
            displayState.Accuracy.gameObject.SetActive(false);
            displayState.Time.gameObject.SetActive(false);
            displayState.ProductionTime.gameObject.SetActive(false);

            foreach(StationResultDisplay station in displayState.StationResults)
                station.gameObject.SetActive(false);
            displayState.RetryButton.gameObject.SetActive(false);
            displayState.ContinueButton.gameObject.SetActive(false);

            // Set display color
            WaferState waferState = Find.State<WaferState>();
            Find.GlobalAsset(out ResultDisplayConfig config);

            bool success = WaferStateUtility.GetAggregatedPrecision(waferState) > 0.8f; // TODO: adjust value
            displayState.Background.color = success ? config.SuccessBackgroundColor : config.FailureBackgroundColor;
            displayState.Heading.Text.text = success ? "WAFER COMPLETE" : "WAFER FAILED";
            displayState.Heading.Background.color = success ? config.SuccessHeaderColor : config.FailureHeaderColor;

            Color sectionColor = success ? config.SuccessSectionColor : config.FailureSectionColor;
            displayState.Accuracy.Background.color = sectionColor;
            displayState.Time.Background.color = sectionColor;
            displayState.ProductionTime.Background.color = sectionColor;

            yield return 1f;

            SetEnabledResultsGroup(displayState, true);
            
            yield return 0.5f;

            // Show ratings for each station
            Find.State(out SequenceState sequence);
            FabricationStep[] steps = sequence.Level.Sequence.Steps;

            int activeStepsCount = steps.Length < 6 ? steps.Length : 6;
            var rect = displayState.StationDisplayRow;
            Vector2 size = rect.sizeDelta;
            size.x = activeStepsCount * 90f - 10f;
            rect.sizeDelta = size;
            
            float[] stationPrecisions = new float[displayState.StationResults.Length];
            float[] stationCount = new float[displayState.StationResults.Length];
            
            for (int i = 0; i < steps.Length; i++)
            {
                stationPrecisions[(int)steps[i].StepId] += waferState.StepPrecisions[i];
                stationCount[(int)steps[i].StepId]++;
            }

            for (int i = 0; i < displayState.StationResults.Length; i++)
            {
                if (stationCount[i] == 0) {
                    displayState.StationResults[i].gameObject.SetActive(false);
                    continue;
                }

                float average = stationPrecisions[i] / stationCount[i];
                displayState.StationResults[i].SetRating(average, config);
                displayState.StationResults[i].gameObject.SetActive(true);
                yield return 0.5f;
            }

            // Show sections - accuracy/time/production time (cycles)
            TimeState timeState = Find.State<TimeState>();
            float time = TimeStateUtility.GetElapsed(timeState);
            FabricationMinigameState fabState = Find.State<FabricationMinigameState>();
            float secondssPerCycle = 30;
            int cycles = (int) Mathf.Ceil(time / secondssPerCycle);

            displayState.Accuracy.Text.text = $"{fabState.Precision * 100:F2}%";
            displayState.Time.Text.text = $"{time:F2}s";
            displayState.ProductionTime.Text.text = $"{fabState.TotalCycles} cycles";

            displayState.Accuracy.gameObject.SetActive(true);
            yield return 1f;

            displayState.Time.gameObject.SetActive(true);
            yield return 1f;

            displayState.ProductionTime.gameObject.SetActive(true);
            yield return 1f;

            // Show button
            GameObject button = success ? displayState.ContinueButton.gameObject : displayState.RetryButton.gameObject;
            button.SetActive(true);

            SpacefabGame.Events.Dispatch(GameEvents.FabSucceeded, EvtArgs.Create((fabState.Precision, time, cycles)));
            yield break;
        }

        private static IEnumerator HideResultsRoutine(ResultDisplayState displayState)
        {
            SetEnabledResultsGroup(displayState, false);
            yield break;
        }
    }
}
