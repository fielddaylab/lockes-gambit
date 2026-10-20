using System;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Animation;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.UI;
using FieldDay.UI.Animation;
using FieldDay.UI.Widgets;
using FieldDay.World;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab.Supply {
    public sealed class ShipListPanel : SharedPanel, IRegistrationCallbacks {
        [Serializable]
        public struct SpeedIconConfig {
            public Sprite Image;
            public Vector2 Size;
        }

        public LayoutSizeGroup Layout;
        public LayoutOptions VerticalLayoutOptions;

        [Header("Row Config")]
        public ShipListRow[] Rows;
        public SpeedIconConfig[] SpeedIcons;

        [NonSerialized] public ShipListRow SelectedRow;

        void IRegistrationCallbacks.OnDeregister() {
            Game.Events.DeregisterAllForContext(this);
        }

        void IRegistrationCallbacks.OnRegister() {
            SpacefabGame.Events.Register<SupplyRouteEventArgs>(GameEvents.SupplyRouteDrawingOpen, OnRouteStarted)
                .Register<SupplyRouteEventArgs>(GameEvents.SupplyRouteDrawingClose, OnRouteEnded);
        }

        private void OnRouteStarted(SupplyRouteEventArgs evtArgs) {
            SelectedRow = Rows[evtArgs.RouteIndex];
            Anims.Replace(ref SelectedRow.Anim, SupplyChainUtility.ShipRowToOnAnimInstance, SelectedRow, 0);
            SelectedRow.CursorHint.TooltipFooter = "<sprite name=\"MouseLeft\"> Cancel";
            SelectedRow.CursorHint.MarkDirty();
            FlashAnim.Play(SelectedRow.Flash, Color.white.WithAlpha(0.5f), FlashAnim.Default);
            SupplyChainUtility.SetShipRowStatsActive(SelectedRow, true);
            SupplyChainUtility.ReflowShipList(this, true);
            PopAnim.Play(SelectedRow.LayoutOffset, PopAnim.Default);
        }

        private void OnRouteEnded(SupplyRouteEventArgs evtArgs) {
            SelectedRow.CursorHint.TooltipFooter = "<sprite name=\"MouseLeft\"> Draw Route";
            SelectedRow.CursorHint.MarkDirty();
            Anims.Replace(ref SelectedRow.Anim, SupplyChainUtility.ShipRowToOffAnimInstance, SelectedRow, 0);
            if (evtArgs.Stats.Time <= 0) {
                FlashAnim.Play(SelectedRow.Flash, Color.black, FlashAnim.Default);
            } else {
                FlashAnim.Play(SelectedRow.Flash, Color.white.WithAlpha(0.5f), FlashAnim.Default);
            }
            SupplyChainUtility.SetShipRowStatsActive(SelectedRow, evtArgs.Stats.Time > 0);
            SelectedRow = null;
            SupplyChainUtility.ReflowShipList(this, true);
        }
    }

    static public partial class SupplyChainUtility {
        static public void PopulateShipList(ShipListPanel panel, SupplyShipIndex ships) {
            for(int i = 0; i < ships.ShipCount; i++) {
                ShipListRow row = panel.Rows[i];
                PopulateShipInformation(row, ships.ShipAssets[i], panel);
                row.CursorHint.Owner = row.CursorHint.UserData = row;
                row.ShipIndex = i;
                row.CursorHint.onClick.Register(HandleShipClicked);
                row.gameObject.SetActive(true);
                SetShipRowStatsActive(row, false);
            }

            for(int i = ships.ShipCount; i < panel.Rows.Length; i++) {
                panel.Rows[i].gameObject.SetActive(false);
            }

            ReflowShipList(panel, true);
        }

        static public void ReflowShipList(ShipListPanel panel, bool snap) {
            using (var children = panel.Layout.Root.QueryLayoutChildren()) {
                var yBuffer = Frame.AllocSpan<float>(children.Count);
                Positioning.DeferredVerticalLayout(children, panel.VerticalLayoutOptions, 0, yBuffer);
                for(int i = 0; i < children.Count; i++) {
                    var row = panel.Rows[i];
                    row.TargetPos.y = yBuffer[i];
                    if (snap) {
                        Positioning.SetOffsetY(row.Rect, row.TargetPos.y);
                        SyncShipRowPositions(row);
                    }
                }
            }
        }

        static public void HandleShipClicked(PointerListener.EventData evtData) {
            ShipListRow row = (ShipListRow) evtData.Source.UserData;
            Find.State(out SupplyRouteDrawingState draw);
            ShipListPanel panel = (ShipListPanel) row.Panel;

            if (draw.RouteIndex == row.ShipIndex) {
                SupplyRouteUtility.QueueRouteDrawingClose();
            } else {
                SupplyRouteUtility.QueueRouteDrawing(row.ShipIndex);

                using (TempVarTable table = TempVarTable.Alloc()) {
                    table.Set("ship", row.ShipIndex);
                    ScriptUtility.Trigger(SupplyScriptTriggers.OnShipSelected, table);
                }
            }
        }

        static public readonly ShipRowToOnAnim ShipRowToOnAnimInstance = new ShipRowToOnAnim();
        static public readonly ShipRowToOffAnim ShipRowToOffAnimInstance = new ShipRowToOffAnim();

        public sealed class ShipRowToOnAnim : LiteAnimator<ShipListRow> {
            public override void InitAnimation(ShipListRow target, ref LiteAnimatorState state) {
                state.ResetTime(0.25f);
                state.Easing = BeauRoutine.Curve.CubeOut;
                state.Registers.X.Float() = target.LayoutOffset.Offset0.x;
                state.Registers.Y.Float() = 140f;
            }

            public override void ResetAnimation(ShipListRow target, ref LiteAnimatorState state) {
                
            }

            public override void UpdateAnimation(ShipListRow target, ref LiteAnimatorState state, float deltaTime) {
                float eased = state.Easing.Evaluate(state.PercentProgress);
                target.LayoutOffset.Offset0 = new Vector2(Mathf.LerpUnclamped(state.Registers.X.Float(), state.Registers.Y.Float(), eased), 0);
                SupplyChainUtility.SyncShipRowPositions(target);
            }
        }

        public sealed class ShipRowToOffAnim : LiteAnimator<ShipListRow> {
            public override void InitAnimation(ShipListRow target, ref LiteAnimatorState state) {
                state.ResetTime(0.15f);
                state.Easing = BeauRoutine.Curve.CubeOut;
                state.Registers.X.Float() = target.LayoutOffset.Offset0.x;
            }

            public override void ResetAnimation(ShipListRow target, ref LiteAnimatorState state) {

            }

            public override void UpdateAnimation(ShipListRow target, ref LiteAnimatorState state, float deltaTime) {
                float eased = state.Easing.Evaluate(state.PercentProgress);
                target.LayoutOffset.Offset0 = new Vector2(state.Registers.X.Float() * (1 - eased), 0);
                SupplyChainUtility.SyncShipRowPositions(target);
            }
        }
    }
}