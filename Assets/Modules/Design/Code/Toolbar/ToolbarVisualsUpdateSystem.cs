using FieldDay;
using FieldDay.Systems;
using UnityEngine;

namespace SpaceFab.Design {
    /// <summary>
    /// Drives the toolbar's presentation layer from ToolbarState + ToolModeState: row focus/
    /// fade, selection arrow position, and any "selected tool" text label.
    ///
    /// Runs on PreUpdate at order 10 under ToolModeMask — after ToolbarSelectSystem has
    /// finished mutating state and before ToolbarRefreshSystem (Update order 0) clears the
    /// one-frame flags.
    ///
    /// Body is currently stubbed. Fields and permissions are declared so the consuming scene
    /// prefab can be built against a stable shape.
    /// </summary>
    public class ToolbarVisualsUpdateSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.PreUpdate, 10, UpdateMasks.ToolModeMask),
                new SysPermissions()
                    .ReadShared<ToolbarState>()
                    .ReadShared<ToolModeState>()
                    .ReadWrite<ToolbarButton>()
                    .ReadWrite<ToolbarRow>()
            );
        }

        // TODO: implement visual refresh.
        //
        // Rough shape:
        //   ToolbarState toolbarState = Find.State<ToolbarState>();
        //   ToolModeState toolModeState = Find.State<ToolModeState>();
        //
        //   1. Row fade. Walk Find.Components<ToolbarRow>() and set each row's FadeGroup.alpha:
        //      full opacity if row.Row == toolbarState.FocusedRow, faded otherwise.
        //
        //   2. Selection arrow. Move or retarget the arrow to toolbarState.CurrentArrowAnchor.
        //      If null (no tool selected yet), hide the arrow.
        //
        //   3. Selected-tool text label. Look up a human-readable name for
        //      toolModeState.ActiveTool and update the label.
        //
        // All three should be idempotent — it's fine to re-run them every frame.
        static private void ProcessWork(float deltaTime)
        {
            ToolbarState toolbarState = Find.State<ToolbarState>();
            ToolModeState toolModeState = Find.State<ToolModeState>();


            ToolbarUtility.UpdateToolbarRowHighlight(toolbarState);
            
            // set up toolbar state's CurrentArrowAnchor position for
            // toolbarState to update the ptr's position
            RectTransform anchor = null;
            ToolbarButton highlight = null;
            var buttons = Find.Components<ToolbarButton>();
            for (int i = 0; i < buttons.Count; i++)
            {
                ToolbarButton button = buttons[i];
                if (ToolbarUtility.ToolTypeForKind(button.Kind) == toolModeState.ActiveTool)
                {
                    anchor = button.ArrowAnchor;
                    highlight = button;
                    break;
                }
            }

            toolbarState.CurrentArrowAnchor = anchor;
            RectTransform ptr = toolbarState.SelectedToolPtr;
            if (ptr != null)
            {
                bool show = anchor != null;
                if (ptr.gameObject.activeSelf != show)
                {
                    ptr.gameObject.SetActive(show);
                }
                if (show)
                {
                    ptr.position = anchor.position;
                    toolbarState.SelectedToolPointerLabel.SetText(highlight.ToolName);

                    if (highlight.AnchorBelow) {
                        toolbarState.SelectedToolPointerArrow.localEulerAngles = new Vector3(0, 0, 180);
                        Positioning.SetAnchorY(toolbarState.SelectedToolPointerLabel.rectTransform, 0);
                        Positioning.SetPivotY(toolbarState.SelectedToolPointerLabel.rectTransform, 1);
                    } else {
                        toolbarState.SelectedToolPointerArrow.localEulerAngles = new Vector3(0, 0, 0);
                        Positioning.SetAnchorY(toolbarState.SelectedToolPointerLabel.rectTransform, 1);
                        Positioning.SetPivotY(toolbarState.SelectedToolPointerLabel.rectTransform, 0);
                    }
                }
            }
        }
    }
}
