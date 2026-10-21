using BeauRoutine;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using SpaceFab.Save;
using SpaceFab;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FieldDay.Debugging;
using SpaceFab.Comic;
using FieldDay.Assets;
using FieldDay.UI.Widgets;
using SpaceFab.UI;
using BeauUtil.Debugger;
using FieldDay.UI;

namespace SpaceFab.Title
{
    [PreloadOrder(10)]
    public class TitleController : SceneController {
        public enum PageId {
            None = 0,
            Main,
            NewGame,
            ContinueGame,
            Settings
        }

        public TitleCanvasLayer MainPage;
        public TitleCanvasLayer AuxMain;
        public TitleCanvasLayer PlayPage;
        public TitleCanvasLayer OptionsPage;

        [Header("Targets")]
        public SceneReference ContinueGameScene;
        public SceneReference CreditsScene;

        [Header("Main Page")]
        public GuiButton NewGameButton;
        public GuiButton ContinueGameButton;
        public GuiButton SettingsButton;
        public GuiButton CreditsButton;

        [Header("Play Page")]
        public TMP_Text PlayHeader;
        public CanvasGroup PlayCodeInputGroup;
        public TMP_InputField PlayCodeInput;
        public GuiButton PlayButton;

        [Header("Options Page")]
        public SettingsMenu SettingsGroup;

        [Header("Background")]
        public Transform LogoTransform;
        public ColorGroup LogoFader;
        public Vector2 LogoOnscreenPosition;
        public Vector2 LogoOffscreenPosition;

        [Header("Camera Positioning")]
        public Vector2 DefaultCameraPosition;
        public Vector2 NewGameCameraPosition;
        public Vector2 ContinueGameCameraPosition;
        public Vector2 SettingsCameraPosition;
        public float CameraTransitionDuration;
        public float CameraTransitionHaltFadeDuration;
        public Curve CameraTransitionCurve;

        [NonSerialized] public PageId CurrentPage;
        [NonSerialized] public bool IsLogoVisible;
        [NonSerialized] public Routine LogoAnim;
        [NonSerialized] public Routine CameraAnim;
        [NonSerialized] public bool InputLock;

        #region Events

        protected override IEnumerator<WorkSlicer.Result?> OnScenePreload() {
            LogoTransform.SetPosition(LogoOffscreenPosition, Axis.XY, Space.Self);
            LogoFader.SetAlpha(0);
            IsLogoVisible = false;
            yield return null;

            NewGameButton.OnClick.Register(OnNewGameClick);
            ContinueGameButton.OnClick.Register(OnContinueGameClick);
            SettingsButton.OnClick.Register(OnSettingsClick);
            CreditsButton.OnClick.Register(OnCreditsClick);
            PlayButton.OnClick.Register(OnStartClick);
            yield return null;

            PlayPage.CloseButton.OnClick.Register(OnPlayClickBack);
            OptionsPage.CloseButton.OnClick.Register(OnSettingsClickBack);
            PlayCodeInput.onValueChanged.AddListener(OnTextUpdated);
        }

        protected override void OnSceneReady() {
            Routine.Start(this, SceneOpen());
        }

        #endregion // Events

        #region Handlers

        private void OnNewGameClick() {
            PlayHeader.SetText("New Game");
            PlayCodeInputGroup.blocksRaycasts = false;
            PlayCodeInput.readOnly = true;
            PlayCodeInput.SetTextWithoutNotify(string.Empty);
            PlayButton.Interactable = false;
            OGD.Player.NewId(OnCodeGenerated, OnCodeError);

            Game.Input.PauseAll();
            Routine.Start(this, ToPlay(PageId.NewGame, NewGameCameraPosition));
        }

        private void OnContinueGameClick() {
            PlayHeader.SetText("Continue Game");
            PlayCodeInputGroup.blocksRaycasts = true;
            PlayCodeInput.readOnly = false;
            PlayCodeInput.SetTextWithoutNotify(Find.State<UserSettingsState>().PlayerCode);
            PlayButton.Interactable = OGD.Player.IsValidPotentialId(PlayCodeInput.text);

            Game.Input.PauseAll();
            Routine.Start(this, ToPlay(PageId.ContinueGame, ContinueGameCameraPosition));
        }

        private void OnSettingsClick() {
            Game.Input.PauseAll();
            Routine.Start(this, ToSettings());
        }

        private void OnCreditsClick() {
            Game.Input.PauseAll();
            Routine.Start(this, ToCredits());
        }

        private void OnStartClick() {
            PlayPage.Input.SetInputOverride(false);
            if (CurrentPage == PageId.NewGame) {
                SpacefabGame.SaveBuffer.Clear();
                if (Game.IsDevBuild && DebugInput.IsDown(KeyCode.LeftShift)) {
                    SaveUtility.SetDebugFlag(true);
                    Log.Msg("[TitleController] Debug save starting");
                    OnStartSuccess();
                } else {
                    SpacefabGame.Events.Dispatch(GameEvents.TitleNewGameClicked);
                    SaveUtility.SetDebugFlag(false);
                    OGD.Player.ClaimId(PlayCodeInput.text, null, OnStartSuccess, OnNewFailure);
                }
            } else {
                SpacefabGame.Events.Dispatch(GameEvents.TitleContinueGameClicked);
                SaveUtility.LoadFromServer(PlayCodeInput.text)
                    .OnComplete(OnStartSuccess)
                    .OnFail(OnContinueFailure);
            }
        }

        private void OnPlayClickBack() {
            Game.Input.PauseAll();
            Routine.Start(this, BackToMain());
        }

        private void OnSettingsClickBack() {
            Game.Input.PauseAll();
            Routine.Start(this, BackToMain());
        }

        private void OnTextUpdated(string text) {
            if (CurrentPage == PageId.ContinueGame) {
                PlayButton.Interactable = OGD.Player.IsValidPotentialId(text);
            }
        }

        #endregion // Handlers

        private void CancelOGDCallbacks() {
            OGD.Player.CancelRequests();
        }

        private void CloseCurrentPage() {
            switch (CurrentPage) {
                case PageId.Main: {
                    MainPage.Hide();
                    AuxMain.Hide();
                    break;
                }
                case PageId.NewGame:
                case PageId.ContinueGame: {
                    PlayPage.Hide();
                    PlayButton.Interactable = false;
                    break;
                }
                case PageId.Settings: {
                    OptionsPage.Hide();
                    break;
                }
            }
        }

        private void OpenCurrentPage() {
            switch (CurrentPage) {
                case PageId.Main: {
                    MainPage.Show();
                    AuxMain.Show();
                    break;
                }
                case PageId.NewGame:
                case PageId.ContinueGame: {
                    PlayPage.Show();
                    break;
                }
                case PageId.Settings: {
                    OptionsPage.Show();
                    break;
                }
            }
        }

        private void MoveCameraTo(Vector2 pose, float duration, Curve ease) {
            CameraAnim.Replace(this, Game.Rendering.PrimaryCamera.transform.MoveTo(pose, duration, Axis.XY, Space.Self).Ease(ease)).SetPhase(RoutinePhase.Update);
        }

        #region Sequences

        private IEnumerator SceneOpen() {
            ShowLogo();
            yield return 0.6f;
            CurrentPage = PageId.Main;
            OpenCurrentPage();
        }

        private IEnumerator ToPlay(PageId page, Vector2 cameraPosition) {
            yield return 0.1f;
            CloseCurrentPage();
            HideLogo();
            MoveCameraTo(cameraPosition, CameraTransitionDuration, CameraTransitionCurve);
            yield return CameraTransitionHaltFadeDuration;
            CurrentPage = page;
            OpenCurrentPage();
            Game.Input.ResumeAll();
        }

        private IEnumerator ToCredits() {
            yield return 0.1f;
            Game.Scenes.LoadMainScene(CreditsScene);
            Game.Input.ResumeAll();
        }

        private IEnumerator ToSettings() {
            yield return 0.1f;
            CloseCurrentPage();
            HideLogo();
            MoveCameraTo(SettingsCameraPosition, CameraTransitionDuration, CameraTransitionCurve);
            yield return CameraTransitionHaltFadeDuration;
            CurrentPage = PageId.Settings;
            OpenCurrentPage();
            Game.Input.ResumeAll();
        }

        private IEnumerator BackToMain() {
            CancelOGDCallbacks();
            yield return 0.1f;
            CloseCurrentPage();
            MoveCameraTo(DefaultCameraPosition, CameraTransitionDuration, CameraTransitionCurve);
            yield return CameraTransitionHaltFadeDuration;
            CurrentPage = PageId.Main;
            OpenCurrentPage();
            ShowLogo();
            Game.Input.ResumeAll();
        }

        #endregion // Sequences

        #region Logo

        private void HideLogo() {
            if (IsLogoVisible) {
                IsLogoVisible = false;
                LogoAnim.Replace(this, LogoHideAnim());
            }
        }

        private void ShowLogo() {
            if (!IsLogoVisible) {
                IsLogoVisible = true;
                LogoAnim.Replace(this, LogoShowAnim());
            }
        }

        private IEnumerator LogoHideAnim() {
            yield return Tween.OneToZero(LogoFader.SetAlpha, 0.15f);
        }

        private IEnumerator LogoShowAnim() {
            LogoTransform.SetPosition(LogoOffscreenPosition, Axis.XY, Space.Self);
            yield return Routine.Combine(
                LogoTransform.MoveTo(LogoOnscreenPosition, 0.4f, Axis.XY, Space.Self).Ease(Curve.CubeOut),
                Tween.ZeroToOne(LogoFader.SetAlpha, 0.4f)
            );
        }

        #endregion // Logo

        #region OGD

        private void OnCodeGenerated(string id) {
            PlayCodeInput.SetTextWithoutNotify(id);
            PlayButton.Interactable = OGD.Player.IsValidPotentialId(id);
        }

        private void OnCodeError(OGD.Core.Error error) {
            PlayPage.Input.ClearInputOverride();
            PopupUtility.DisplayGenericPopup("Uh oh!", "We encountered an error!", (_) => {
                OnPlayClickBack();
            });
        }

        private void OnStartSuccess() {
            Game.SharedState.Get<UserSettingsState>().PlayerCode = PlayCodeInput.text;

            // TODO: set this in OGD
            //SpacefabGame.Events.Dispatch(GameEvents.TitleNewGameClicked);
            SpacefabGame.Events.Dispatch(GameEvents.TitleProfileStarting, PlayCodeInput.text);

            Find.State(out ChapterState chapterState);
            if (chapterState.LastSelectedContractIndex < 0) {
                ComicScripting.LoadComicForChapter(chapterState.ChapterIndex);
            } else {
                Game.Scenes.LoadMainScene(ContinueGameScene);
            }

            if (CurrentPage == PageId.NewGame) {
                Game.Scenes.GetQueuedLoadContext(out SceneRequestContext context);
                context.Set("QueueSave", true);
                Game.Scenes.QueueMainLoadContext(context);
            }
        }

        private void OnNewFailure(OGD.Core.Error error) {
            PlayPage.Input.ClearInputOverride();
            PopupUtility.DisplayGenericPopup("Uh oh!", "We encountered an error!");
        }

        private void OnContinueFailure() {
            PlayPage.Input.ClearInputOverride();
            PopupUtility.DisplayGenericPopup("Uh oh!", "We encountered an error!");
        }

        #endregion // OGD
    }
}