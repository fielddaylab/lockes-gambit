using BeauPools;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using BeauUtil.Services;
using EasyAssetStreaming;
using FieldDay;
using FieldDay.Scenes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpaceFab
{
    public class TransitionStateMgr
    {
        private Routine m_SceneLoadRoutine;
        private Routine m_SaveRoutine;

        private string m_QueuedLoadingText;
        private float m_QueuedLoadingWait;

        private bool m_IsFlavorful;

        #region Constructor

        public TransitionStateMgr()
        {
            Game.Scenes.OnMainSceneUnloading.Register(HandleMainSceneUnloading);

            Game.Events.Register(GameEvents.ProfileSaveBegin, HandleProfileSaveBegin);
            Game.Events.Register(GameEvents.ProfileSaveSuccess, HandleProfileSaveSuccess);
            Game.Events.Register(GameEvents.ProfileSaveError, HandleProfileSaveError);
        }

        #endregion // Constructor

        #region Scene Loading

        private void HandleMainSceneUnloading() {
            m_SceneLoadRoutine.Replace(GameLoop.Host, LoadingIconTransition()).SetPhase(RoutinePhase.RealtimeUpdate);
        }

        private IEnumerator LoadingIconTransition() {
            Find.State(out FullscreenTransitionState fullscreenTransition, out SharedUIState uiState);
            while(!FullscreenTransitionUtility.IsTransitionFullyFadedOut(fullscreenTransition)) {
                yield return null;
            }
            yield return SpacefabGame.IsSceneInGame ? 0.25f : 1.5f;

            uiState.LoadIcon.gameObject.SetActive(true);

            uiState.LoadIcon.LoadGroup.alpha = 0;
            uiState.LoadIcon.FlavorImageFader.SetAlpha(0);
            uiState.LoadIcon.FlavorTextFader.alpha = 0;

            if (SpacefabGame.IsSceneInGame) {
                LoadingScreenFlavorSet info = GetFlavoredLoadingScreenInfo();

                uiState.LoadIcon.FlavorImage.sprite = info.Image;
                uiState.LoadIcon.FlavorImage.enabled = !!info.Image;
                uiState.LoadIcon.FlavorImage.material = info.Material;
                uiState.LoadIcon.FlavorText.SetText(info.Text);

                m_IsFlavorful = !string.IsNullOrEmpty(info.Text) || !!info.Image;
            } else {
                m_IsFlavorful = false;
            }

            uiState.LoadIcon.FlavorTextFader.gameObject.SetActive(m_IsFlavorful);
            uiState.LoadIcon.FlavorImageFader.gameObject.SetActive(m_IsFlavorful);
            uiState.LoadIcon.LoadingTextOffset.Offset0 = new Vector2(0, m_IsFlavorful ? -300 : 0);

            if (m_IsFlavorful) {
                uiState.LoadIcon.FlavorTextOffset.Offset0 = new Vector2(0, 32);
                yield return Routine.Combine(
                    uiState.LoadIcon.LoadGroup.FadeTo(1, 0.3f),
                    uiState.LoadIcon.FlavorTextFader.FadeTo(1, 0.5f).DelayBy(0.1f),
                    uiState.LoadIcon.FlavorTextOffset.Offset0To(default, 0.5f).DelayBy(0.1f).Ease(Curve.CubeOut),
                    Tween.ZeroToOne(uiState.LoadIcon.FlavorImageFader.SetAlpha, 0.5f).DelayBy(0.4f)
                );
            } else {
                m_IsFlavorful = false;
                yield return uiState.LoadIcon.LoadGroup.FadeTo(1, 0.3f);
            }
        }

        private IEnumerator FadeOutRoutine() {
            Find.State(out FullscreenTransitionState fullscreenTransition, out SharedUIState uiState);

            if (m_IsFlavorful) {
                yield return Routine.Combine(
                    uiState.LoadIcon.LoadGroup.FadeTo(0, 0.15f),
                    uiState.LoadIcon.FlavorTextFader.FadeTo(0, 0.15f),
                    Tween.OneToZero(uiState.LoadIcon.FlavorImageFader.SetAlpha, 0.15f)
                );
                uiState.LoadIcon.gameObject.SetActive(false);
                uiState.LoadIcon.FlavorImage.sprite = null;
                uiState.LoadIcon.FlavorImage.material = null;
                yield return 0.2f;
            } else {
                uiState.LoadIcon.gameObject.SetActive(false);
            }
        }

        public IEnumerator FadeOutLoadingScreen() {
            m_SceneLoadRoutine.Replace(GameLoop.Host, FadeOutRoutine()).SetPhase(RoutinePhase.RealtimeUpdate);
            return m_SceneLoadRoutine.Wait();
        }

        public float DequeueWaitTime() {
            float wait = m_QueuedLoadingWait;
            m_QueuedLoadingWait = 0;
            return wait;
        }

        private LoadingScreenFlavorSet GetFlavoredLoadingScreenInfo() {
            Find.State(out FullscreenTransitionState fullscreenTransition, out SharedUIState uiState);
            Find.State(out ChapterState chapterState, out ContractState contractState);

            if (chapterState.ChapterIndex == 0 && chapterState.LastSelectedContractIndex < 0) {
                return default;
            }

            return new LoadingScreenFlavorSet() {
                Text = RNG.Instance.Choose(
                    "FRA Logistics: \"Protect, preserve, persevere, we are the FRA.\"",
                    "\"Building up protection for the future, ensuring Auserine's safety starts here. FRA Logistics.\"",
                    "Frontier Food Services: \"The finest lab-grown food in all of Auserine!\"",
                    "\"The cornerstone for all food production within Frontier Robotics Space. Frontier Food Services.\"",
                    "FR Pharma: \"For a healthier and more productive Auserine.\"",
                    "\"FR Pharma serves you, through medical, pharmaceutical, and emergency service needs.\"",
                    "PAL-World: \"Save Creds. Live Longer.\"",
                    "\"Pally-World is Auserine's leading choice for any common good for a low-low price.\"",
                    "Frontline Insurance: \"We're on the Frontline so you don't have to!\"",
                    "\"The best and only insurance company in all of Auserine. Frontline Insurance.\"",
                    "\"Frontier founders were the first to set foot on Frosch, paving the way for Frontier Robotics and the future of the Auserine system!\"\n<align=\"right\">- Oscar Grayson",
                    "\"When you look up into the sky to see what the future holds, the smog stares right back at ya.\" - Common Saying on Wolfram"
                ),
                Image = uiState.LoadIcon.DefaultFlavorImage,
                Material = null
            };
        }

        #endregion // Scene Loading

        #region Profile Saving

        private void HandleProfileSaveBegin()
        {
            m_SaveRoutine.Stop();

            SharedUIState uiState = Find.State<SharedUIState>();
            m_SaveRoutine.Replace(SharedUIUtility.OnBeginSave(uiState));
        }

        private void HandleProfileSaveSuccess()
        {
            if (m_SaveRoutine.Exists())
            {
                m_SaveRoutine.OnComplete(() =>
                {
                    SharedUIState uiState = Find.State<SharedUIState>();
                    m_SaveRoutine.Replace(SharedUIUtility.OnSaveSuccess(uiState));
                });
            }
            else
            {
                SharedUIState uiState = Find.State<SharedUIState>();
                m_SaveRoutine.Replace(SharedUIUtility.OnSaveSuccess(uiState));
            }
        }

        private void HandleProfileSaveError()
        {
            if (m_SaveRoutine.Exists())
            {
                m_SaveRoutine.OnComplete(() =>
                {
                    SharedUIState uiState = Find.State<SharedUIState>();
                    m_SaveRoutine.Replace(SharedUIUtility.OnSaveError(uiState));
                });
            }
            else
            {
                SharedUIState uiState = Find.State<SharedUIState>();
                m_SaveRoutine.Replace(SharedUIUtility.OnSaveError(uiState));
            }
        }

        #endregion // Profile Saving
    }

    public struct LoadingScreenFlavorSet {
        public string Text;
        public Sprite Image;
        public Material Material;
    }
}
