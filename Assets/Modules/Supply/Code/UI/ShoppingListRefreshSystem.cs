using FieldDay;
using FieldDay.Systems;
using BeauRoutine;
using UnityEngine;

namespace SpaceFab.Supply
{
    /// <summary>
    /// Clears the shopping list's one-frame Dirty flag after
    /// ShoppingListVisualsSystem has consumed it earlier in the frame.
    /// </summary>
    public class ShoppingListRefreshSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.LateUpdate, 100, UpdateMasks.SupplyMask),
                new SysPermissions()
                    .ReadWriteShared<ShoppingListState>()
            );
        }

        // Clears the Dirty flag once the rebuild has run for this frame.
        static private void ProcessWork(float deltaTime)
        {
            ShoppingListState shoppingState = Find.State<ShoppingListState>();
            shoppingState.Dirty = false;

            ShoppingListLayoutState layoutState = Find.State<ShoppingListLayoutState>();

            if (layoutState.ListExpanded && layoutState.IsCollapsedVisually)
            {
                layoutState.IsCollapsedVisually = false;
                MoveToPosition(layoutState, 0f);
            }
            else if (!layoutState.ListExpanded && !layoutState.IsCollapsedVisually)
            {
                layoutState.IsCollapsedVisually = true;
                MoveToPosition(layoutState, layoutState.CollapseYValue);
            }
        }

        private static void MoveToPosition(ShoppingListLayoutState layoutState, float targetY)
        {
            float currentY = layoutState.CollapseTransform.localPosition.y;
            if (Mathf.Abs(currentY - targetY) < 0.001f) return;

            layoutState.ToggleRoutine.Replace(GameLoop.Host,
                layoutState.CollapseTransform.MoveTo(targetY, 0.25f, Axis.Y, Space.Self).Ease(Curve.CubeOut)
            );
        }
    }
}
