using FieldDay.Data;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Save
{
    public class FabricationSaveState : MinigameSaveStateBase, ISaveStateChunkObject
    {
        public int FinalizedTotalCycles;
        public int FinalizedCost;

        #region Interfaces

        // ISaveStateChunkObject

        public override void Read(object self, ref ByteReader reader, SaveStateChunkConsts consts)
        {
            base.Read(self, ref reader, consts);

            FinalizedTotalCycles = reader.Read<int>();
            if (consts.Version < 4) {
                reader.Read<float>();
                FinalizedCost = 0;
            } else {
                FinalizedCost = reader.Read<int>();
            }
        }

        public override void Write(object self, ref ByteWriter writer, SaveStateChunkConsts consts)
        {
            base.Write(self, ref writer, consts);

            writer.Write(FinalizedTotalCycles);
            writer.Write(FinalizedCost);
        }

        // IMinigameSaveState

        public override void SetDefaults()
        {
            base.SetDefaults();

            FinalizedTotalCycles = 0;
            FinalizedCost = 0;
        }

        #endregion // Interfaces
    }
}