using FieldDay.Components;
using UnityEngine;

namespace SpaceFab.Design {
    /// <summary>
    /// Pure-data component on each toolbar row's parent GameObject. Owns the row's fade
    /// CanvasGroup and the StackLayer identity (Metal or Transistor).
    ///
    /// ToolbarVisualsUpdateSystem walks Find.Components&lt;ToolbarRow&gt;() and sets
    /// FadeGroup.alpha based on which row is focused. Two instances total — one per row.
    /// </summary>
    public class ToolbarRow : BatchedComponent {
        public ToolbarLayer Row;
        public CanvasGroup DeemphasizeGroup;
        public CanvasGroup DiagramGroup;
    }

    public enum ToolbarLayer : byte {
        Metal = 0,
        Transistor = 1,
        Other = 2
    }
}
