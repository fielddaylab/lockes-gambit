using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.Assets;
using FieldDay.Components;
using FieldDay.Scripting;
using FieldDay.UI;
using SpaceFab.Materials;
using System;
using UnityEngine;
using BeauPools;
using SpaceFab.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace SpaceFab.Supply {
    public sealed class SupplyRouteNode : BatchedComponent, IRegistrationCallbacks {
        public SupplyRouteNodeType Type;

        [Header("Stats")]
        [Range(0, 5)] public int Cost;
        [Range(0, 3)] public int Risk;
        [Range(0, 5)] public int Time;

        [Header("Materials")]
        [AssetName(typeof(MaterialAsset), true)] public StringHash32 MaterialType;
        [AssetName(typeof(MaterialAsset), true)] public StringHash32 ConversionInputType;

        [Header("Components")]
        public Collider2D Collider;
        public CursorHint Cursor;
        public SupplyRouteNodeRenderer Renderer;
        public SupplyRouteNodeInfoDisplay InfoPopup;

        [Header("Links")]
        [AssetName(typeof(WikiPageData), true)] public StringHash32 WikiPage;

        [NonSerialized] public StringHash32 Id;
        [NonSerialized] public int Index;
        [NonSerialized] public Vector2 Position;
        [NonSerialized] public SupplyHoverFlags Hover;

        static public CursorTooltipContentDelegate PlanetTooltip = (CursorHint c, ref CursorTooltipBuildState b) => {

            return true;
        };

        static public CursorTooltipContentDelegate InfoPopupTooltip = (CursorHint c, ref CursorTooltipBuildState b) => {

            return true;
        };

        public void OnRegister() {
            if (Cursor != null) {
                Cursor.onClick.Register(SupplyRouteUtility.HandleNodeClicked);
            }
        }

        public void OnDeregister() {
            if (Cursor != null) {
                Cursor.onClick.Deregister(SupplyRouteUtility.HandleNodeClicked);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            using(PooledStringBuilder psb = PooledStringBuilder.Create()) {
                psb.Builder.Append(name);
                switch (Type) {
                    case SupplyRouteNodeType.Producer: {
                        psb.Builder.Append("\n").Append(MaterialType.ToDebugString());
                        break;
                    }
                    case SupplyRouteNodeType.Converter: {
                        psb.Builder.Append("\n").Append(ConversionInputType.ToDebugString()).Append(" -> ").Append(MaterialType.ToDebugString());
                        break;
                    }
                }
                if (Type != SupplyRouteNodeType.Home) {
                    psb.Builder.Append("\n - Cost ").AppendNoAlloc(Cost).Append("\n - Risk ").AppendNoAlloc(Risk).Append("\n - Time ").AppendNoAlloc(Time);
                }
                Handles.Label(transform.position, psb.Builder.ToString());
            }
        }
#endif // UNITY_EDITOR
    }

    public enum SupplyRouteNodeType : byte {
        Home,
        Producer,
        Converter
    }

    [Flags]
    public enum SupplyHoverFlags : byte {
        None = 0,
        Node = 0x01,
        Route = 0x02,
    }

    static public partial class SupplyRouteUtility {
        static public void HandleNodeClicked(PointerListener.EventData evtData) {
            SupplyRouteNode node = evtData.Source.UserData as SupplyRouteNode;
            if (node == null) { return; }
            if (node.Type != SupplyRouteNodeType.Producer && node.Type != SupplyRouteNodeType.Converter) { return; }

            using (TempVarTable table = TempVarTable.Alloc()) {
                table.Set("node", node.Id);
                ScriptUtility.Trigger(SupplyScriptTriggers.OnNodeClicked, table);
            }
        }

        static public void InitializeTooltipReferences(SupplyRouteNode node) {
            node.Cursor.UserData = node;
            node.Cursor.DynamicBuilder = SupplyRouteNode.PlanetTooltip;
            if (node.InfoPopup) {
                node.InfoPopup.Cursor.UserData = node;
                node.Cursor.DynamicBuilder = SupplyRouteNode.InfoPopupTooltip;
            }
        }

        static public void AddNodeHoverFlag(SupplyRouteNode node, SupplyHoverFlags flags) {
            bool wasHovering = node.Hover != 0;
            if ((node.Hover & flags) != flags) {
                node.Hover |= flags;
                if (!wasHovering) {
                    SetHovering(node.Renderer, true);
                }
            }
        }

        static public void RemoveNodeHoverFlag(SupplyRouteNode node, SupplyHoverFlags flags) {
            bool wasHovering = node.Hover != 0;
            if ((node.Hover & flags) != 0) {
                node.Hover &= ~flags;
                if (wasHovering && node.Hover == 0) {
                    SetHovering(node.Renderer, false);
                }
            }
        }
    }
}