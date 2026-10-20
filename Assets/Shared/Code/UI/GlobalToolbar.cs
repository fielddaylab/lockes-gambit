
using TMPro;
using System;
using BeauRoutine;
using FieldDay;
using UnityEngine;
using FieldDay.UI;
using FieldDay.Audio;
using FieldDay.UI.Widgets;

namespace SpaceFab
{
    public class GlobalToolbar : BaseGuiModule, IRegistrationCallbacks
    {
        #region Inspector

        public Canvas Canvas;
        public BaseRaycasterInputLayer InputLayer;
        
        [Header("Buttons")]
        public GuiButton ReturnButton;
        public GuiButton PauseButton;

        [Header("Icons")]
        public Sprite DefaultPauseIcon;
        public Sprite SettingsPauseIcon;

        #endregion // Inspector

        [NonSerialized] public Vector2 ReturnPosition;
        [NonSerialized] public Vector2 PausePosition;

        public void OnDeregister() {
            Game.Scenes.OnMainSceneLateEnable.Deregister(OnSceneLateEnable);
        }

        public void OnRegister() {
            PauseButton.OnClick.Register(() => {
                PauseUtility.SetPaused(Find.GuiModule<PauseModule>(), true);
            });
            ReturnButton.OnClick.Register(OnReturnClicked);

            ReturnPosition = ReturnButton.Rect.anchoredPosition;
            PausePosition = PauseButton.Rect.anchoredPosition;

            Game.Scenes.OnMainSceneLateEnable.Register(OnSceneLateEnable);
        }

        private void OnReturnClicked() {
            if (Game.SharedState.TryGet(out MinigameRequestExitState minigameExiter)) {
                minigameExiter.ExitRequestState = RequestState.Requested;
            } else {
                // TODO: exit game prompt
            }
        }

        private void OnSceneLateEnable() {
            if (Game.SharedState.TryGet(out GlobalUISceneConfig config)) {
                Canvas.enabled = true;
                InputLayer.ClearInputOverride();
                ReturnButton.gameObject.SetActive(config.ReturnScene.IsValid);
                if (!config.ReturnScene.IsValid) {
                    PauseButton.Rect.anchoredPosition = ReturnPosition;
                    PauseButton.ImageGraphic.sprite = SettingsPauseIcon;
                } else {
                    PauseButton.Rect.anchoredPosition = PausePosition;
                    PauseButton.ImageGraphic.sprite = DefaultPauseIcon;
                }
            } else {
                Canvas.enabled = false;
                InputLayer.SetInputOverride(false);
            }
        }
    }
}
