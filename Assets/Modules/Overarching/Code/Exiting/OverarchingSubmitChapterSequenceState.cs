using BeauRoutine;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Overarching
{
    public enum OverarchingSubmitChapterPhase
    {
        Waiting,
        Starting,
        ShutdownSequenceSystem,
        MoveToNextChapter,
        TransitionComplete
    }

    public class OverarchingSubmitChapterSequenceState : SharedStateComponent, IRegistrationCallbacks
    {
        public OverarchingSubmitChapterPhase Phase;
        public GuiButton SubmitButton;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            SubmitButton.OnClick.AddListener(() => {
                OverarchingTransitions.AdvanceContract();
                });

            // Hidden by default; OverarchingSubmitButtonUtility.Refresh reveals it once every
            // minigame has FoundValidSolution. Stays hidden until the first contract-load refresh.
            SubmitButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Logic paired with OverarchingSubmitChapterSequenceState.
    /// </summary>
    public static class OverarchingSubmitButtonUtility
    {
        // Shows the submit-chapter button only when every minigame is solved, hides it otherwise.
        // Not run per-frame — the player can't change minigame completion while in the Overarching
        // scene, so this is called only when completion could have changed: on contract load (which
        // also covers scene start, since entering the scene loads a contract).
        public static void Refresh(OverarchingSubmitChapterSequenceState submitState, MinigameSaveStates saveStates)
        {
            Assert.NotNullOrDestroyed(submitState);
            Assert.NotNullOrDestroyed(saveStates);

            bool allSolved = MinigameSaveUtility.AllSolved(saveStates);
            GameObject go = submitState.SubmitButton.gameObject;
            if (go.activeSelf != allSolved) {
                go.SetActive(allSolved);
            }
        }
    }
}
