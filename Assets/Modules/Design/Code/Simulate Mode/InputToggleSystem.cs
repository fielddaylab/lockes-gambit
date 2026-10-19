using FieldDay;
using FieldDay.Components;
using FieldDay.Systems;
using SpaceFab.Design.Visuals;
using UnityEngine;

namespace SpaceFab.Design
{
    /// <summary>
    /// Per-frame visibility + state visuals refresh for every InputToggleVisual in the scene.
    /// Owns the effective show/hide for each overlay so a runtime UseToggleInputMode flip takes
    /// effect without re-running grid setup:
    ///   - Hidden when the visual hasn't been coord-stamped yet (SpawnInputOverlays runs once in
    ///     GridStackLoadSystem.SetupBaseLevel).
    ///   - Hidden when DesignMinigameState.UseToggleInputMode is false.
    ///   - Hidden when the visual's coord doesn't match any InputToggleEntry (cell isn't an Input).
    ///   - Otherwise visible; background renderer tint + "LO"/"HI" label reflect the current state.
    /// Common sprites (background frame, arrow) are set once on spawn from GridSpriteDB; the
    /// per-input subtype label is set once on spawn too. This system only changes what depends on
    /// Lo/Hi state.
    /// Click handling is wired per-visual via Cursor.onClick — no polling here.
    /// </summary>
    public class InputToggleSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 5, UpdateMasks.DesignMask),
                new SysPermissions()
                    .ReadShared<InputToggleState>()
                    .ReadShared<DesignMinigameState>()
                    .Read<InputToggleVisual>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            InputToggleState toggleState = Find.State<InputToggleState>();
            DesignMinigameState designState = Find.State<DesignMinigameState>();
            GridSpriteDB spriteDB = Find.GlobalAsset<GridSpriteDB>();

            var visuals = Find.Components<InputToggleVisual>();
            for (int i = 0; i < visuals.Count; i++)
            {
                InputToggleVisual visual = visuals[i];
                if (visual == null) { continue; }

                // Pre-stamp visuals stay hidden until SpawnInputOverlays assigns the cell index.
                if (!visual.CellIndexStamped)
                {
                    SetActiveIfChanged(visual, false);
                    continue;
                }

                int entryIdx = InputToggleUtility.IndexOfCellIndex(toggleState, visual.CellIndex);
                if (entryIdx < 0)
                {
                    // Cell no longer maps to an Input (player erased it). Hide and skip sprite work.
                    SetActiveIfChanged(visual, false);
                    continue;
                }

                SetActiveIfChanged(visual, true);
                ApplyStateVisuals(visual, toggleState.Inputs[entryIdx].State, spriteDB);
            }
        }

        // SetActive is a virtual call; avoid it when the state already matches to keep this loop
        // cheap on the typical "no change" frame.
        static private void SetActiveIfChanged(InputToggleVisual visual, bool active)
        {
            if (visual.gameObject.activeSelf != active)
            {
                visual.gameObject.SetActive(active);
            }
        }

        // Applies the per-state visuals to an active overlay: color tint on the background
        // renderer + "LO"/"HI" text on the state label. Skip-when-already-correct guards keep
        // the per-frame work to a no-op when nothing changed.
        static private void ApplyStateVisuals(InputToggleVisual visual, FlowState state, GridSpriteDB spriteDB)
        {
            if (visual.BackgroundRenderer != null)
            {
                //Color tint = GridSpriteDBUtility.LookupInputToggleColor(spriteDB, state);
                visual.BackgroundRenderer.sprite = GridSpriteDBUtility.LookupInputBackground(spriteDB, state);
                // if (visual.BackgroundRenderer.color != tint)
                // {
                //     visual.BackgroundRenderer.color = tint;
                // }
            }

            if (visual.StateText != null)
            {
                string next = InputToggleUtility.GetStateShortLabel(state);
                if (visual.StateText.text != next)
                {
                    visual.StateText.SetText(next);

                    visual.StateText.color = GridSpriteDBUtility.LookupInputToggleTextColor(spriteDB, state);
                }
            }

            if (visual.ArrowRenderer != null)
            {
                //use white for arrow & subtype, use color for signal switch text
                visual.ArrowRenderer.color = Color.white;
                //visual.ArrowRenderer.color = GridSpriteDBUtility.LookupInputToggleTextColor(spriteDB, state);
            }

            if (visual.SubtypeText != null)
            {
                //use white for arrow & subtype, use color for signal switch text
                visual.SubtypeText.color = Color.white;
                //visual.SubtypeText.color = GridSpriteDBUtility.LookupInputToggleTextColor(spriteDB, state);
            }

            if (visual.ToggleHandleRenderer != null)
            {
                visual.ToggleHandleRenderer.color = GridSpriteDBUtility.LookupInputToggleColor(spriteDB, state);

                // Slide the knob: left-anchor for Lo, right-anchor for Hi. Skip the write when
                // already correct so a still frame doesn't churn the transform.
                Transform handle = visual.ToggleHandleRenderer.transform;
                Vector3 nextLocal = state == FlowState.Hi ? visual.ToggleHandleHiLocalPosition : visual.ToggleHandleLoLocalPosition;
                if (handle.localPosition != nextLocal)
                {
                    handle.localPosition = nextLocal;
                }
            }
        }
    }
}
