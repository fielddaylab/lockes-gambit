
using BeauUtil;
using FieldDay;
using FieldDay.Components;
using FieldDay.Rendering;
using TMPro;
using UnityEngine.UI;
using static SpaceFab.Logging.Logging;

namespace SpaceFab
{
    public class SettingsMenu : BatchedComponent, IRegistrationCallbacks
    {
        public Toggle FullscreenToggle;
        public Slider VolumeSlider;
        public Slider MusicSlider;
        public Slider SFXSlider;

        public void OnDeregister()
        {
            FullscreenToggle.onValueChanged.RemoveAllListeners();

            VolumeSlider.onValueChanged.RemoveAllListeners();
            MusicSlider.onValueChanged.RemoveAllListeners();
            SFXSlider.onValueChanged.RemoveAllListeners();

            Game.Rendering.OnFullscreenChanged.Deregister(OnFullscreenUpdated);
        }

        public void OnRegister()
        {
            UserSettingsState state = Find.State<UserSettingsState>();

            Game.Rendering.OnFullscreenChanged.Register(OnFullscreenUpdated);

            FullscreenToggle.SetIsOnWithoutNotify(ScreenUtility.GetFullscreen());
            UpdateFullscreen(FullscreenToggle.isOn);
            FullscreenToggle.onValueChanged.AddListener(UpdateFullscreen);
            FullscreenToggle.onValueChanged.AddListener((bool toggle) => SpacefabGame.Events.Dispatch(GameEvents.SettingsToggleFullScreen, toggle));

            VolumeSlider.onValueChanged.AddListener(UpdateVolume);
            MusicSlider.onValueChanged.AddListener((float vol) => UpdateBusVolume(SettingsUtility.MUSIC_BUS_ID, VolumeType.MUSIC, vol));
            SFXSlider.onValueChanged.AddListener((float vol) => UpdateBusVolume(SettingsUtility.SFX_BUS_ID, VolumeType.SFX, vol));
            InitializeVolumeSettings();
        }

        private void InitializeVolumeSettings()
        {
            UserSettingsState settings = Find.State<UserSettingsState>();
            VolumeSlider.SetValueWithoutNotify(settings.MasterVolume * 10);
            MusicSlider.SetValueWithoutNotify(settings.MusicVolume * 10);
            SFXSlider.SetValueWithoutNotify(settings.SFXVolume * 10);
        }

        static private void UpdateFullscreen(bool toggle)
        {
            SettingsUtility.SetFullscreen(Find.State<UserSettingsState>(), toggle);
        }

        static private void UpdateVolume(float volume)
        {
            SettingsUtility.SetMasterVolume(Find.State<UserSettingsState>(), volume / 10);
            SpacefabGame.Events.Dispatch(GameEvents.SettingsSetVolume, EvtArgs.Create((VolumeType.MASTER, volume / 10)));
        }

        static private void UpdateBusVolume(StringHash32 bus, VolumeType type, float volume)
        {
            SettingsUtility.SetAudioBusVolume(Find.State<UserSettingsState>(), bus, volume / 10);
            SpacefabGame.Events.Dispatch(GameEvents.SettingsSetVolume, EvtArgs.Create((type, volume / 10)));
        }

        private void OnFullscreenUpdated(bool fullscreen)
        {
            FullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        }
    }
}