using System;
using FieldDay;
using FieldDay.Scripting;
using FieldDay.SharedState;
using FieldDay.Systems;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Design {
    /// <summary>
    /// Display-state for the Chip Design toolbar. The authoritative selected tool remains
    /// on ToolModeState (ActiveTool / ActiveLayer); ToolbarState holds purely presentational
    /// fields — current focused row, hover-override status, one-frame Clear request.
    ///
    /// Populated by ToolbarSelectSystem (PreUpdate) and consumed by ToolbarVisualsUpdateSystem
    /// (LateUpdate). One-frame flags cleared by ToolbarRefreshSystem (LateUpdate, order 100).
    /// </summary>
    public class ToolbarState : SharedStateComponent, IRegistrationCallbacks {
        public RectTransform SelectedToolPtr;
        public RectTransform SelectedToolPointerArrow;
        public TMP_Text SelectedToolPointerLabel;
        public RectTransform SelectedLayerHighlight;

        // Which row the visuals layer should focus this frame. Normally aligned with the row of
        // the selected tool; temporarily flips when the player hovers over the opposite row.
        [NonSerialized] public ToolbarLayer FocusedRow;

        // True iff FocusedRow is currently driven by a hover rather than the selected tool's
        // row. ToolbarSelectSystem sets this on BeginHover and clears it on EndHover.
        [NonSerialized] public bool HoverOverrideActive;

        // One-frame request set when the Clear button is clicked. Cleared by
        // ToolbarRefreshSystem at end-of-frame. Consumer is the confirmation-modal pipeline
        // (deferred — not yet implemented).
        [NonSerialized] public bool ClearRequestedThisFrame;

        // RectTransform the selection arrow should snap to. Populated by ToolbarSelectSystem
        // from the selected button's ArrowAnchor whenever the selected tool changes. Consumed
        // by ToolbarVisualsUpdateSystem (stubbed this pass).
        [NonSerialized] public RectTransform CurrentArrowAnchor;

        public void OnRegister() {
            // Default focus to the Metal row. First frame of ToolbarSelectSystem runs no logic
            // if there are no pointer events, so this default is what the player sees until
            // they click a button. Alignment with ToolModeState.ActiveTool = None is fine —
            // no tool is selected, so no row is "correct" yet.
            FocusedRow = ToolbarLayer.Metal;
            HoverOverrideActive = false;
            ClearRequestedThisFrame = false;
            CurrentArrowAnchor = null;
        }

        public void OnDeregister() {
        }
    }

    /// <summary>
    /// Command surface for ToolbarState + ToolModeState. Declared as partial so the per-button
    /// pointer-event mutation surface (OnClick/OnPointerEnter/OnPointerExit) can live with
    /// ToolbarButton.cs while the main command logic lives here.
    /// </summary>
    public static partial class ToolbarUtility {
        #region Commands

        // Tool-selection handler. Called by ToolbarSelectSystem when a non-Clear button is
        // clicked. Writes both ToolModeState (authoritative selection) and ToolbarState
        // (display focus). HoverOverrideActive flips off because the player has committed to
        // a row — any lingering hover focus is now outdated.
        public static void SelectTool(ToolModeState toolModeState, ToolbarState toolbarState, ToolbarButtonKind kind, ToolbarLayer row) {
            toolModeState.ActiveTool = ToolTypeForKind(kind);
            if (row != ToolbarLayer.Other) {
                toolModeState.ActiveLayer = (StackLayer)row;
            }
            toolbarState.FocusedRow = row;
            toolbarState.HoverOverrideActive = false;
            using (var table = TempVarTable.Alloc()) {
                table.Set("tool", toolModeState.ActiveTool.ToString());
                ScriptUtility.Trigger(DesignScriptTriggers.OnToolSelected, table);
            }
        }

        public static void DeselectTool(ToolModeState toolModeState, ToolbarState toolbarState) {
            toolModeState.ActiveTool = ToolType.None;
            toolbarState.FocusedRow = ToolbarLayer.Other;
            toolbarState.CurrentArrowAnchor = null;
            if (toolbarState.SelectedToolPtr.gameObject.activeSelf) {
                toolbarState.SelectedToolPtr.gameObject.SetActive(false);
            }
            UpdateToolbarRowHighlight(toolbarState);
            using (var table = TempVarTable.Alloc()) {
                table.Set("tool", "None");
                ScriptUtility.Trigger(DesignScriptTriggers.OnToolSelected, table);
            }
        }


        // Clear-button handler. Sets the one-frame request flag. Downstream (deferred): the
        // confirmation-modal pipeline reads this, shows "Are you sure?", and on confirm routes
        // to ClearAllSystem via its own request flag.
        public static void RequestClear(ToolbarState toolbarState) {
            toolbarState.ClearRequestedThisFrame = true;
        }

        // Hover-entry handler. Only swaps focus if the hovered row differs from the selected
        // tool's row. Buttons in the same row as the selected tool don't trigger a swap —
        // hovering stays within the already-focused row.
        public static void BeginHover(ToolbarState toolbarState, ToolModeState toolModeState, ToolbarLayer hoveredRow) {
            ToolbarLayer selectedRow = RowForTool(toolModeState.ActiveTool, toolbarState.FocusedRow);
            if (hoveredRow == selectedRow) { return; }

            toolbarState.FocusedRow = hoveredRow;
            toolbarState.HoverOverrideActive = true;
        }

        // Hover-exit handler. If a hover override was in effect, restore focus to the row of
        // the currently selected tool. If no override was in effect (e.g. hovering within the
        // already-focused row), this is a no-op.
        public static void EndHover(ToolbarState toolbarState, ToolModeState toolModeState) {
            if (!toolbarState.HoverOverrideActive) { return; }

            toolbarState.HoverOverrideActive = false;
            toolbarState.FocusedRow = RowForTool(toolModeState.ActiveTool, toolbarState.FocusedRow);
        }

        #endregion // Commands

        #region Visuals
        public static void UpdateToolbarRowHighlight(ToolbarState state) {
            // 1. Row fade — full opacity on focused row, faded otherwise.
            var rows = Find.Components<ToolbarRow>();
            for (int i = 0; i < rows.Count; i++) {
                ToolbarRow row = rows[i];
                bool focused = row.Row == state.FocusedRow;
                if (row.DeemphasizeGroup != null) {
                    row.DeemphasizeGroup.alpha = focused ? 0f : 1;
                }
                if (row.DiagramGroup != null) {
                    row.DiagramGroup.alpha = focused ? 1f : 0.4f;
                }

                if (focused && row.DiagramGroup) {
                    state.SelectedLayerHighlight.position = row.DiagramGroup.transform.position;
                }
            }

            state.SelectedLayerHighlight.gameObject.SetActive(state.FocusedRow != ToolbarLayer.Other);

        }

        #endregion // Visuals

        #region Kind Mapping

        // Kind → ToolType mapping. Clear is not a tool — it returns None.
        public static ToolType ToolTypeForKind(ToolbarButtonKind kind) {
            switch (kind) {
                case ToolbarButtonKind.DrawMetal:  return ToolType.DrawMetal;
                case ToolbarButtonKind.DrawNNodes: return ToolType.DrawNNodes;
                case ToolbarButtonKind.DrawPNodes: return ToolType.DrawPNodes;
                case ToolbarButtonKind.DrawVia:    return ToolType.DrawVia;
                case ToolbarButtonKind.DrawGate:   return ToolType.DrawGate;
                case ToolbarButtonKind.Erase:      return ToolType.Erase;
                default:                           return ToolType.None;
            }
        }

        // ToolType → StackLayer. Used by EndHover to figure out which row to snap focus back
        // to when a hover-override ends.
        //
        // Via and Gate live on the Metal row in the UI even though they cross layers; their
        // row identity is defined by where the button sits in the toolbar, not what they
        // affect. None/Erase don't have an intrinsic row — we fall back to whatever the
        // caller passed as the default.
        public static ToolbarLayer RowForTool(ToolType tool, ToolbarLayer fallback) {
            switch (tool) {
                case ToolType.DrawMetal:
                case ToolType.DrawVia:
                case ToolType.DrawGate:
                    return ToolbarLayer.Metal;
                case ToolType.DrawNNodes:
                case ToolType.DrawPNodes:
                    return ToolbarLayer.Transistor;
                case ToolType.Erase:
                    return ToolbarLayer.Other;
                default:
                    return fallback;
            }
        }

        // Kind → ToolTypeFlags bit. Used by ToolbarAvailabilityUtility to check the per-level
        // allowed-tools mask. Note the NNODE/PNODE naming aligns with ToolTypeFlags, which
        // drops the "Draw" prefix and the trailing "s".
        public static ToolTypeFlags FlagForKind(ToolbarButtonKind kind) {
            switch (kind) {
                case ToolbarButtonKind.DrawMetal:  return ToolTypeFlags.METAL;
                case ToolbarButtonKind.DrawNNodes: return ToolTypeFlags.NNODE;
                case ToolbarButtonKind.DrawPNodes: return ToolTypeFlags.PNODE;
                case ToolbarButtonKind.DrawVia:    return ToolTypeFlags.VIA;
                case ToolbarButtonKind.DrawGate:   return ToolTypeFlags.GATE;
                case ToolbarButtonKind.Erase:      return ToolTypeFlags.ERASER;
                case ToolbarButtonKind.Clear:      return ToolTypeFlags.CLEAR;
                default:                           return default;
            }
        }

        #endregion // Kind Mapping
    }

    /// <summary>
    /// Per-level toolbar availability helpers. Called once per level load to show/hide
    /// buttons based on the level's allowed-tools bitmask.
    /// </summary>
    public static class ToolbarAvailabilityUtility {
        // Applies the level's allowed-tools mask to every ToolbarButton in the scene. Hidden
        // buttons have Available=false, gameObject.SetActive(false), and their PointerListener
        // disabled (so no pointer events can fire during transition races).
        //
        // Caller: TODO — the site that invokes this on level-load lives in
        // DesignTransitionSystem or ModeTransitionSystem once LevelData is plumbed through to
        // Design's runtime state. The level-data pipeline itself is still the broader missing
        // dependency flagged during the flow-propagation port.
        public static void ApplyAllowedTools(ToolTypeFlags allowed) {
            var buttons = Find.Components<ToolbarButton>();
            for (int i = 0; i < buttons.Count; i++) {
                ToolbarButton button = buttons[i];
                ToolTypeFlags bit = ToolbarUtility.FlagForKind(button.Kind);
                bool available = (allowed & bit) != 0;

                button.Available = available;
                button.gameObject.SetActive(available);
                if (button.DynamicButton != null) { button.DynamicButton.enabled = available; }
            }
        }
    }
}
