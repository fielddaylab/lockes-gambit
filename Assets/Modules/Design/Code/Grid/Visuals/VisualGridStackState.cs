using FieldDay;
using FieldDay.SharedState;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SpaceFab.Design.Visuals
{
    public class VisualGridStackState : SharedStateComponent, IRegistrationCallbacks {
        public SpriteRenderer GridRenderer;
        public SpriteRenderer GridTestOutline;
        public VisualGridStack VisualGridStack;
        public GameObject CellVisualsPrefab;
        public Transform CellVisualsContainer;

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