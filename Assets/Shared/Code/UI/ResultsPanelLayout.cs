using FieldDay.UI;
using FieldDay.UI.Widgets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.UI {
    public sealed class ResultsPanelLayout : MonoBehaviour {
        public Canvas Canvas;
        public CanvasGroup Group;
        public CanvasInputLayer Input;

        [Header("Header")]
        public RectTransform HeaderPosition;
        public TMP_Text HeaderText;
        public Graphic HeaderBackground;

        [Header("Contract")]
        public ContractUI Contract;
    }
}