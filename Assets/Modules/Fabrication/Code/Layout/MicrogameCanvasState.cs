using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI.Animation;
using SpaceFab.Fabrication.Stations;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Unity.Mathematics.math;

namespace SpaceFab.Fabrication.Layout
{
    /// <summary>
    /// Holds the shared Fabrication microgame canvas UI: the fader, the restart popup group, and
    /// the instructions group (instruction/subtitle text plus a single key-image display). Owned by
    /// the Fabrication minigame; written by the per-microgame systems and the tutorial-interrupt
    /// system through MicrogameCanvasUtility.
    /// </summary>
    public class MicrogameCanvasState : SharedStateComponent, IRegistrationCallbacks
    {
        public CanvasGroup FaderGroup;

        [Space(5)]
        [Header("Popup Group")]
        public CanvasGroup PopupGroup;
        public TMP_Text PopupMainText;
        public TMP_Text PopupSecondaryText;
        public DynamicButton StationRestartButton;

        [Space(5)]
        public CanvasGroup InstructionsGroup;
        public InstructionLookup InstructionsLookup;
        public Image KeyImageDisplay;
        public TextMeshProUGUI m_InstructionTMP, m_SubtitleTMP;
        public Image SputterInstruction;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            FaderGroup.alpha = 1;
            FaderGroup.blocksRaycasts = false;

            PopupGroup.alpha = 0;
            PopupGroup.blocksRaycasts = false;

            InstructionsGroup.alpha = 0;
            InstructionsGroup.blocksRaycasts = false;

            KeyImageDisplay.enabled = false;
        }
    }

    public static class MicrogameCanvasUtility
    {
        public static void ShowStationInstructions(MicrogameCanvasState state, SerializedHash32 stationID)
        {
            state.FaderGroup.alpha = 1f;
            state.FaderGroup.blocksRaycasts = true;

            state.InstructionsGroup.alpha = 1f;
            state.InstructionsGroup.blocksRaycasts = true;

            if (stationID == FabricationConsts.SPUTTER_STATION_ID)
            {   
                state.SputterInstruction.gameObject.SetActive(true);

                state.KeyImageDisplay.enabled = false;
                state.m_InstructionTMP.text = string.Empty;
                state.m_SubtitleTMP.text = string.Empty;
            }
            else
            {
                state.SputterInstruction.gameObject.SetActive(false);
                InstructionSet uiInstructions = InstructionLookupUtility.LookupInstructions(stationID, state.InstructionsLookup);

                // Resolve the station's key image to a single shared Image, instead of toggling
                // a dedicated GameObject per key type.
                Sprite keyImage = InstructionLookupUtility.LookupKeyImage(uiInstructions.UIKey, state.InstructionsLookup);
                state.KeyImageDisplay.sprite = keyImage;
                state.KeyImageDisplay.enabled = keyImage != null;

                // Resize the shared Image to the swapped sprite's native dimensions so key images of
                // differing sizes aren't stretched to a single fixed rect.
                if (keyImage != null)
                {
                    state.KeyImageDisplay.SetNativeSize();
                }

                state.m_InstructionTMP.text = uiInstructions.Instruction;
                state.m_SubtitleTMP.text = uiInstructions.Subtitle;
            }
        }

        public static void HideStationInstructions(MicrogameCanvasState state)
        {
            state.SputterInstruction.gameObject.SetActive(false);
            state.FaderGroup.alpha = 0f;
            state.FaderGroup.blocksRaycasts = false;

            state.InstructionsGroup.alpha = 0f;
            state.InstructionsGroup.blocksRaycasts = false;

            state.KeyImageDisplay.enabled = false;
        }
    }
}