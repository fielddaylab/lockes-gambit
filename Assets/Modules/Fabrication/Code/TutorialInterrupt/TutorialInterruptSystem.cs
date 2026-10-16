using FieldDay;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.StationControl;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication
{
    /// <summary>
    /// Drives the microgame retry popup while a completion is paused in StationControlPhase.AwaitingRetry.
    /// Shows MicrogameCanvasState's popup (with per-microgame text) and pauses the attempt timer when a
    /// precision-gate failure raises TutorialInterruptRequested; on the StationRestartButton press, resumes
    /// the timer, restarts the microgame, and hides the popup. Runs on Update at order 20 under AttemptMask
    /// (after StationControlSystem at 10 and SequenceSystem at 15), so it stays live while MicrogameMask is
    /// suspended during the pause.
    /// </summary>
    public class TutorialInterruptSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.Update, 20, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<TutorialInterruptState>()
                    .ReadWriteShared<StationControlState>()
                    .ReadWriteShared<TimeState>()
                    .ReadShared<MicrogameCanvasState>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out TutorialInterruptState interruptState,
                out StationControlState stationState,
                out MicrogameCanvasState canvasState,
                out TimeState timeState
                );

            // Register the restart-button listener exactly once per scene.
            if (!interruptState.ListenerRegistered && canvasState.StationRestartButton != null)
            {
                canvasState.StationRestartButton.OnClick.AddListener(HandleRestartClicked);
                interruptState.ListenerRegistered = true;
            }

            // Open the popup and pause the timer on a precision-gate failure.
            if (interruptState.TutorialInterruptRequested)
            {
                ShowPopup(stationState, canvasState);
                timeState.IsPaused = true;
            }

            // On restart: resume the timer, reset the microgame to a fresh play state, and hide the popup.
            // The restart flag is a latch set by the async button click — consume-and-clear it here (rather
            // than in the refresh system) so it survives until this system actually processes it, no matter
            // when in the frame the click landed.
            if (interruptState.RestartButtonPressedThisFrame)
            {
                interruptState.RestartButtonPressedThisFrame = false;
                timeState.IsPaused = false;
                StationControlUtility.RestartMicrogame(stationState);
                HidePopup(canvasState);
            }
        }

        // Populates the popup with the active station's retry text (direction-specific when authored) and
        // reveals the popup canvas group.
        static private void ShowPopup(StationControlState stationState, MicrogameCanvasState canvasState)
        {
            if (stationState.ActiveInterfacer != null)
            {
                RetryPopupLookup lookup = Find.GlobalAsset<RetryPopupLookup>();
                // raw precision: < 1 = overshoot (above target), >= 1 = undershoot (below target).
                RetryDirection direction = stationState.LastRawMicrogamePrecision < 1f
                    ? RetryDirection.Above
                    : RetryDirection.Below;
                RetryPopupSet set = RetryPopupLookupUtility.Lookup(stationState.ActiveInterfacer.Id, lookup, direction);
                if (set != null)
                {
                    canvasState.PopupMainText.text = set.MainText;
                    canvasState.PopupSecondaryText.text = set.SecondaryText;
                }
            }

            canvasState.PopupGroup.alpha = 1f;
            canvasState.PopupGroup.blocksRaycasts = true;
            // interactable must be true or the CanvasGroup disables its child buttons (the restart click
            // would never fire).
            canvasState.PopupGroup.interactable = true;
        }

        // Hides the popup canvas group.
        static private void HidePopup(MicrogameCanvasState canvasState)
        {
            canvasState.PopupGroup.alpha = 0f;
            canvasState.PopupGroup.blocksRaycasts = false;
            canvasState.PopupGroup.interactable = false;
        }

        // StationRestartButton.onClick handler. Raises the one-frame restart flag, consumed above.
        static private void HandleRestartClicked()
        {
            if (Game.SharedState.Has<TutorialInterruptState>())
            {
                Find.State<TutorialInterruptState>().RestartButtonPressedThisFrame = true;
            }
        }
    }
}
