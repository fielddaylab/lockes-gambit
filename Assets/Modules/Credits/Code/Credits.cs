using BeauUtil;
using FieldDay;
using FieldDay.Audio;
using FieldDay.Components;
using FieldDay.Music;
using FieldDay.Scenes;
using FieldDay.UI.Widgets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Credits
{
    public class Credits : SceneController
    {
        [Header("Scene Management")]
        [SerializeField] private GuiButton m_SkipButton;
        [SerializeField] private SceneReference m_ReturnScene;

        [Header("Music")]
        [SerializeField, AudioEvent] private StringHash32 m_Music;


        protected override IEnumerator<WorkSlicer.Result?> OnScenePreload() {
            m_SkipButton.OnClick.AddListener(HandleSkipClicked);

            Game.Scenes.GetLoadContext(out SceneRequestContext context);
            if (!context.Get("PreserveMusic").AsBool()) {
                MusicPlayer.SetLoopingTrack(m_Music);
            }

            return null;
        }  
        
        private void HandleSkipClicked()
        {
            Game.Scenes.LoadMainScene(m_ReturnScene);
        }
    }
}