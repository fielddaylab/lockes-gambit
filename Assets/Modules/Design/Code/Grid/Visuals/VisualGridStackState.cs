using FieldDay;
using FieldDay.SharedState;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceFab.Design.Visuals
{
    public class VisualGridStackState : SharedStateComponent, IRegistrationCallbacks
    {
        public SpriteRenderer GridRenderer;
        public VisualGridStack VisualGridStack;
        public GameObject CellVisualsPrefab;
        public Transform CellVisualsContainer;

        [Header("PuzzleDescription")]
        public TMP_Text PuzzleTitleText;
        public TMP_Text PuzzleDescriptionText;

        // TODO: set to true when:
        // LayoutChanged
        // Visual Feedback Routine
        // ResetFlow States
        // Result closed clicked
        [NonSerialized] public bool VisualsNeedRefreshing;

        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            VisualGridStack = new VisualGridStack();
        }
    }
}