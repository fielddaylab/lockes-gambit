using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Physics;
using FieldDay.UI;
using System;
using UnityEngine;

namespace SpaceFab.Supply {
    public struct SupplyRouteData {
        public const int MaxNodeIndices = 32;
        public const int MaxHazardIndices = 8;

        public const int MaxNodes = 8;
        public const int MaxNonTerminalNodes = MaxNodes - 1;
        public const int MaxHazards = 8;
        public const int MaxShips = 3;
        public const int MaxCapacity = 3;

        public int NodeCount;
        public SupplyRouteNode[] Nodes;
        public BitSet32 NodeMask;
        public SupplyRouteFlags Flags;

        public void Create() {
            Nodes = new SupplyRouteNode[MaxNodes];
        }

        public void Clear() {
            for(int i = NodeCount; i-- > 0;) {
                Nodes[i] = null;
            }

            NodeCount = 0;
            NodeMask = default;
            Flags = default;
        }

        static public void Copy(in SupplyRouteData src, ref SupplyRouteData dst) {
            dst.NodeCount = src.NodeCount;
            dst.NodeMask = src.NodeMask;
            dst.Flags = src.Flags;
            for(int i = 0; i < MaxNodes; i++) {
                dst.Nodes[i] = src.Nodes[i];
            }
        }
    }

    [Flags]
    public enum SupplyRouteFlags : byte {
        AutoConnectEnd = 0x01,
    }

    public unsafe struct SupplyRouteFragmentData {
        public byte NodeCount;
        public fixed byte Nodes[SupplyRouteData.MaxNodes - 1];

        public ushort Key {
            get {
                if (NodeCount < 2) {
                    return 0;
                }

                return (ushort) (Nodes[0] << 8 | Nodes[NodeCount - 1]);
            }
        }
    }

    public unsafe struct SupplyRouteStats {
        public SupplyRouteResultFlags Flags;
        public byte Time;
        public byte Cost;
        public byte Risk;
        public byte HazardMask;
        public byte MaterialCount;
        public fixed uint MaterialHashes[SupplyRouteData.MaxCapacity];
        public static byte MaxTime = 6;
    }

    public unsafe struct SupplyRouteRenderInfo {
        
    }

    /// <summary>
    /// Serialized form of one ship's route. Nodes are stored as SupplyRouteNode.Id hashes rather
    /// than runtime indices - an index is the node's position in SupplyChainMap.Nodes, which is
    /// scene hierarchy order and shifts whenever the map hierarchy is edited.
    /// Deliberately separate from SupplyRouteData so the on-disk format is decoupled from the
    /// runtime state shape (SupplyRouteData holds live SupplyRouteNode references and cannot be blitted).
    /// </summary>
    public unsafe struct SupplyRouteSaveData {
        public byte NodeCount;
        public SupplyRouteFlags Flags;
        public fixed uint NodeIds[SupplyRouteData.MaxNodes];
    }

    [Flags]
    public enum SupplyRouteResultFlags : byte {
        TooManyResources = 0x01,
        NodeInAnotherPath = 0x02,
        NodeInInvalidSegment = 0x04,
        NodeInEarlierSegment = 0x08,
        PathTooLong = 0x10,

        HasUnusedConverter = 0x20,

        ErrorMask = TooManyResources | NodeInAnotherPath | NodeInInvalidSegment | NodeInEarlierSegment | PathTooLong,
    }

    static public partial class SupplyRouteUtility {
        static public int GetIndexOfNodeInRoute(in SupplyRouteData data, SupplyRouteNode node) {
            for(int i = 0; i < data.NodeCount; i++) {
                if (data.Nodes[i] == node) {
                    return i;
                }
            }

            return -1;
        }

        static public bool IsNodeInRoute(in SupplyRouteData data, SupplyRouteNode node) {
            return data.NodeMask.IsSet(node.Index);
        }
        
        static public unsafe bool TryEvaluatePath(in SupplyRouteData route, in SupplyShipStats ship, int sourceRouteIndex, out SupplyRouteStats stats) {
            if (route.NodeCount < 2) {
                stats = default;
                return false;
            }

            SupplyChainMap map = Find.State<SupplyChainMap>();
            SupplyRouteConfig config = Find.GlobalAsset<SupplyRouteConfig>();
            SupplyRouteResultFlags resultFlags = 0;
            int hazardMask = 0;

            int cost = 0;
            int risk = 0;
            int time = 0;

            // TRAVEL DISTANCE

            float distance = 0;
            int maxNodesToRead = (route.Flags & SupplyRouteFlags.AutoConnectEnd) != 0 ? route.NodeCount + 1 : route.NodeCount;
            for(int i = 1; i < maxNodesToRead; i++) {
                SupplyRouteNode nodeA = route.Nodes[i - 1];
                SupplyRouteNode nodeB = route.Nodes[i % route.NodeCount];

                Vector2 nodeAPos = nodeA.Position;
                Vector2 nodeBPos = nodeB.Position;

                float segmentDist = Vector2.Distance(nodeAPos, nodeBPos);
                distance += segmentDist;
            }

            // HAZARDS

            if (map.HazardCount > 0) {
                ContactFilter2D hazardFilter = default;
                hazardFilter.SetLayerMask(global::LayerMasks.SupplyChainHazard_Mask);

                // TODO: Determine if we allow multiple passes through a given region to affect the route multiple times

                for (int i = 1; i < maxNodesToRead; i++) {
                    SupplyRouteNode nodeA = route.Nodes[i - 1];
                    SupplyRouteNode nodeB = route.Nodes[i % route.NodeCount];

                    Vector2 nodeAPos = nodeA.Position;
                    Vector2 nodeBPos = nodeB.Position;

                    int intersectionCount = RaycastUtility.LinecastIntersections2D(nodeAPos, nodeBPos, hazardFilter, s_HazardIntersectionBuffer, s_HazardRaycastBuffers);
                    for(int intersectionIndex = 0; intersectionIndex < intersectionCount; intersectionIndex++) {
                        RaycastIntersection2D intersection = s_HazardIntersectionBuffer[intersectionIndex];
                        Collider2D collider = Find.FromId<Collider2D>(intersection.ColliderId);
                        SupplyRouteHazard hazard = collider.GetComponentInParent<SupplyRouteHazard>();
                        if (!Bits.Contains(hazardMask, hazard.Index)) {
                            Bits.Add(ref hazardMask, hazard.Index);
                            cost += hazard.Cost;
                            time += hazard.Time;
                            risk += hazard.Risk;
                        }
                    }
                }
            }

            int travelTime = (int)(distance / config.ShipSpeeds[ship.Speed] + (1.0f - float.Epsilon));

            // MATERIALS

            StringHash32* materials = stackalloc StringHash32[SupplyRouteData.MaxCapacity];
            int materialCount = 0;
            BitSet32 converterIndices = default;
            int maxNodeTime = 0;

            Assert.True(ship.Capacity <= SupplyRouteData.MaxCapacity, "Ship capacity overflow");

            // PRODUCERS
            // also sets up converter pass

            for (int i = 1; i < maxNodesToRead; i++) {
                SupplyRouteNode node = route.Nodes[i % route.NodeCount];
                if (node.Type == SupplyRouteNodeType.Producer) {
                    if (materialCount >= ship.Capacity) {
                        resultFlags |= SupplyRouteResultFlags.TooManyResources;
                    } else {
                        materials[materialCount++] = node.MaterialType;
                        risk += node.Risk;
                        cost += node.Cost;
                        maxNodeTime = Math.Max(maxNodeTime, node.Time);
                    }
                } else if (node.Type == SupplyRouteNodeType.Converter) {
                    converterIndices.Set(i);
                }
            }

            // CONVERTERS
            // cyclic to handle potential chains of conversions (A->B, B->C)
            
            if (!converterIndices.IsEmpty && materialCount > 0) {
                bool converted;
                do {
                    converted = false;
                    foreach(var bit in converterIndices) {
                        SupplyRouteNode node = route.Nodes[bit];
                        for(int i = 0; i < materialCount; i++) {
                            if (materials[i] == node.ConversionInputType) {
                                materials[i] = node.MaterialType;
                            }
                            cost += node.Cost;
                            risk += node.Risk;
                            maxNodeTime = Math.Max(maxNodeTime, node.Time);
                            converted = true;
                            converterIndices.Unset(bit);
                        }
                    }
                } while (converted && !converterIndices.IsEmpty);
            }

            if (!converterIndices.IsEmpty) {
                resultFlags |= SupplyRouteResultFlags.HasUnusedConverter;
            }

            time = Math.Max(travelTime, maxNodeTime);

            Assert.True(risk <= byte.MaxValue, "Risk overflow");
            Assert.True(time <= byte.MaxValue, "Time overflow");
            Assert.True(cost <= byte.MaxValue, "Cost overflow");

            stats.Risk = (byte)risk;
            stats.Time = Math.Min((byte)time, SupplyRouteStats.MaxTime);
            stats.Cost = (byte)cost;
            stats.Flags = resultFlags;
            stats.MaterialCount = (byte) materialCount;
            stats.HazardMask = (byte) hazardMask;

            for(int i = 0; i < materialCount; i++) {
                stats.MaterialHashes[i] = materials[i].HashValue;
            }
            for(int i = materialCount; i < SupplyRouteData.MaxCapacity; i++) {
                stats.MaterialHashes[i] = default;
            }

            return true;
        }

        static private readonly Raycast2DBuffers s_HazardRaycastBuffers = new Raycast2DBuffers(8);
        static private readonly RaycastIntersection2D[] s_HazardIntersectionBuffer = new RaycastIntersection2D[8];
    }
}