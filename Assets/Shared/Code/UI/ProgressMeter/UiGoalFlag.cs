using BeauUtil;
using FieldDay;
using FieldDay.Components;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab {
    public class UiGoalFlag : BatchedComponent, IRegistrationCallbacks {

        public SerializedHash32 Id;

        // limit the funds cells to prevent stem from detaching from flag
        public TMP_Text NameText;
        public RectTransform Stem;
        public CanvasGroup Fader;
        public Image Icon;

        public int minFundsCells;
        public int maxFundsCells;

        [NonSerialized] public int targetFunds;
        [NonSerialized] public int uiTargetFunds;
        [NonSerialized] public StringHash32 CurrentGoalId = null;

        public void OnDeregister() {
        }

        public void OnRegister() {
            ProgressMeterUtility.SetFlagVisible(this, false);
        }
    }
}
