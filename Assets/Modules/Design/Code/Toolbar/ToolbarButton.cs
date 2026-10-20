using BeauUtil.UI;
using FieldDay.Components;
using FieldDay;
using UnityEngine;
using SpaceFab.UI;
using System;
using FieldDay.UI.Widgets;

namespace SpaceFab.Design {
    /// <summary>
    /// Identifies which toolbar button this is. Six of the seven kinds map 1-to-1 to a
    /// ToolType; Clear is not a ToolType — it fires a one-shot request to the (deferred)
    /// confirmation-modal pipeline instead of changing the selected tool.
    /// </summary>
    public enum ToolbarButtonKind {
        DrawMetal,
        DrawNNodes,
        DrawPNodes,
        DrawVia,
        DrawGate,
        Erase,
        Clear,
    }

    /// <summary>
    /// Per-button input surface on the Chip Design toolbar. Carries one-frame pointer flags
    /// that ToolbarSelectSystem consumes on PreUpdate and ToolbarRefreshSystem clears on
    /// LateUpdate. Mirrors the MinigameZone pattern from the Overarching module.
    /// </summary>
    public class ToolbarButton : BatchedComponent, IRegistrationCallbacks {
        // Semantic identity — which tool this button selects.
        public ToolbarButtonKind Kind;

        // Which row this button visually lives in. Metal or Transistor.
        // Clear/Erase are off-row in the mockup but still get a Row assignment for consistency;
        // their hover does not swap focus because ToolbarUtility.BeginHover only swaps when the
        // hovered row differs from the selected tool's row — and Clear/Erase do not change the
        // selected tool's row.
        public ToolbarLayer Row;

        // The dynamic button component on this button's GameObject (assigned in inspector).
        // Its onClick / onPointerEnter / onPointerExit events drive the one-frame flags below.
        public GuiButton DynamicButton;

        [Header("Visuals")]
        // RectTransform the selection arrow should snap to when this button is selected.
        // Read by ToolbarVisualsUpdateSystem (stubbed this pass).
        public RectTransform ArrowAnchor;
        public string ToolName;
        public bool AnchorBelow;

        // One-frame input flags. Set by the pointer handlers below; consumed by
        // ToolbarSelectSystem; cleared by ToolbarRefreshSystem at end of frame.
        [NonSerialized] public bool ClickedThisFrame;
        [NonSerialized] public bool PointerEnterThisFrame;
        [NonSerialized] public bool PointerExitThisFrame;

        // Set by ToolbarAvailabilityUtility when the current level's allowed-tools mask is
        // applied. Unavailable buttons have Available=false AND gameObject.SetActive(false)
        // AND DynamicButton disabled — but select/refresh systems guard against stale flags
        // anyway, in case something flips a flag during a scene transition race.
        [NonSerialized] public bool Available = true;

        public void OnRegister() {
            if (DynamicButton == null) { return; }
            DynamicButton.OnClick.AddListener(HandleClick);
            DynamicButton.CursorHint.onPointerEnter.AddListener(HandlePointerEnter);
            DynamicButton.CursorHint.onPointerExit.AddListener(HandlePointerExit);
        }

        public void OnDeregister() {
            if (DynamicButton == null) { return; }
            DynamicButton.OnClick.RemoveListener(HandleClick);
            DynamicButton.CursorHint.onPointerEnter.RemoveListener(HandlePointerEnter);
            DynamicButton.CursorHint.onPointerExit.RemoveListener(HandlePointerExit);
        }

        #region Pointer Handlers

        // Pointer events route into ToolbarUtility so the mutation point is a single static
        // surface — matches MinigameZonesUtility pattern and gives Leaf a clean hook later.

        private void HandleClick() {
            ToolbarUtility.OnClick(this);
        }

        private void HandlePointerEnter() {
            ToolbarUtility.OnPointerEnter(this);
        }

        private void HandlePointerExit() {
            ToolbarUtility.OnPointerExit(this);
        }

        #endregion // Pointer Handlers
    }

    /// <summary>
    /// Pointer-event mutation surface for ToolbarButton. Intentionally trivial — the one-frame
    /// flags here are consumed by ToolbarSelectSystem, which holds the interesting logic.
    /// Declared as partial so ToolbarState.cs can extend ToolbarUtility with the command
    /// surface (SelectTool, RequestClear, BeginHover, EndHover, kind-mapping helpers) without
    /// a separate utility class name.
    /// </summary>
    public static partial class ToolbarUtility {
        public static void OnClick(ToolbarButton button) {
            button.ClickedThisFrame = true;
        }

        public static void OnPointerEnter(ToolbarButton button) {
            button.PointerEnterThisFrame = true;
        }

        public static void OnPointerExit(ToolbarButton button) {
            button.PointerExitThisFrame = true;
        }
    }
}
