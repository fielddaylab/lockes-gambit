using BeauUtil;
using FieldDay.Data;
using SpaceFab.Save;
using SpaceFab.Supply;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Save
{
    public class SupplySaveState : MinigameSaveStateBase, ISaveStateChunkObject
    {
        // outputs
        public int FinalizedRisk;
        public int FinalizedTotalCycles;
        public int FinalizedCost;

        // layout
        // Per-ship drawn routes. Routes is always MaxShips long; only the first RouteCount slots
        // are meaningful, and a slot with NodeCount < 2 means that ship has no route.
        public int RouteCount;
        public SupplyRouteSaveData[] Routes;

        #region Interfaces

        // ISaveStateChunkObject

        public override void Read(object self, ref ByteReader reader, SaveStateChunkConsts consts)
        {
            base.Read(self, ref reader, consts);

            FinalizedRisk = reader.Read<int>();
            FinalizedTotalCycles = reader.Read<int>();
            FinalizedCost = reader.Read<int>();

            SupplySaveUtility.ReadRoutes(ref reader, consts, this);
        }

        public override void Write(object self, ref ByteWriter writer, SaveStateChunkConsts consts)
        {
            base.Write(self, ref writer, consts);

            writer.Write(FinalizedRisk);
            writer.Write(FinalizedTotalCycles);
            writer.Write(FinalizedCost);

            SupplySaveUtility.WriteRoutes(ref writer, consts, this);
        }

        // IMinigameSaveState

        public override void SetDefaults()
        {
            base.SetDefaults();

            FinalizedRisk = -1;
            FinalizedTotalCycles = -1;
            FinalizedCost = -1;

            SupplySaveUtility.ClearRoutes(this);
        }

        #endregion // Interfaces
    }

    /// <summary>
    /// Serializer for the Supply chunk's route layout.
    /// Wire format is a count-prefixed list of routes; each route writes its node count, its flags,
    /// and then that many node id hashes. Node counts of 0 are written for empty ship slots so the
    /// list stays positional (slot index == ship index).
    /// </summary>
    public static class SupplySaveUtility
    {
        // Resets every route slot, allocating the array on first use. Called from SetDefaults, so
        // this is also what wipes routes when a new contract is confirmed via ClearMinigameState.
        public static void ClearRoutes(SupplySaveState saveState)
        {
            if (saveState.Routes == null)
            {
                saveState.Routes = new SupplyRouteSaveData[SupplyRouteData.MaxShips];
            }
            else
            {
                Array.Clear(saveState.Routes, 0, saveState.Routes.Length);
            }

            saveState.RouteCount = 0;
        }

        // Writes the route list. Only RouteCount slots are emitted; a slot with fewer than 2 nodes
        // is written as a bare 0 count.
        public static unsafe void WriteRoutes(ref ByteWriter writer, SaveStateChunkConsts consts, SupplySaveState saveState)
        {
            int count = saveState.RouteCount;
            writer.Write(count);

            for (int i = 0; i < count; i++)
            {
                SupplyRouteSaveData routeData = saveState.Routes[i];
                if (routeData.NodeCount < 2)
                {
                    writer.Write((byte) 0);
                    continue;
                }

                writer.Write(routeData.NodeCount);
                writer.Write(routeData.Flags);
                for (int nodeIdx = 0; nodeIdx < routeData.NodeCount; nodeIdx++)
                {
                    writer.Write(routeData.NodeIds[nodeIdx]);
                }
            }
        }

        // Reads the route list back. Tolerates a chunk written before routes existed - those end
        // right after the three finalized ints, so an empty remainder means "no routes".
        public static unsafe void ReadRoutes(ref ByteReader reader, SaveStateChunkConsts consts, SupplySaveState saveState)
        {
            ClearRoutes(saveState);

            if (reader.Remaining < sizeof(int))
            {
                return;
            }

            int count = reader.Read<int>();
            saveState.RouteCount = Math.Min(count, SupplyRouteData.MaxShips);

            for (int i = 0; i < count; i++)
            {
                byte nodeCount = reader.Read<byte>();
                if (nodeCount == 0)
                {
                    continue;
                }

                // Built on the stack - a fixed buffer cannot be written through a managed array element.
                SupplyRouteSaveData routeData = default;
                routeData.NodeCount = (byte) Math.Min((int)nodeCount, SupplyRouteData.MaxNodes);
                routeData.Flags = reader.Read<SupplyRouteFlags>();

                // Every id is read off the stream even when the slot is out of range or the route is
                // overlong, so the reader still lands exactly at the end of the chunk.
                for (int nodeIdx = 0; nodeIdx < nodeCount; nodeIdx++)
                {
                    uint nodeId = reader.Read<uint>();
                    if (nodeIdx < SupplyRouteData.MaxNodes)
                    {
                        routeData.NodeIds[nodeIdx] = nodeId;
                    }
                }

                if (i < saveState.RouteCount)
                {
                    saveState.Routes[i] = routeData;
                }
            }
        }
    }
}