using FieldDay.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Fabrication.LayerInstructions
{
    /// <summary>
    /// View component for the Layer Instruction Panel. Data-only — its text and CanvasGroup
    /// fields are populated by LayerInstructionVisualsUtility in response to sequence
    /// reset/advance/completion signals.
    /// </summary>
    public class LayerInstructionRow : BatchedComponent
    {
        public TMP_Text LayerText;
        public Image RowBG;
        public Image CompleteIcon;
        public Image IndicatorArrow;
        public Image LayerIcon;

        public void Initialize()
        {
            CompleteIcon.gameObject.SetActive(false);
            IndicatorArrow.gameObject.SetActive(false);
        }

        public void MarkRowActive()
        {
            IndicatorArrow.gameObject.SetActive(true);
        }

        public void MarkRowComplete()
        {
            CompleteIcon.gameObject.SetActive(true);
            IndicatorArrow.gameObject.SetActive(false);
        }
    }
}