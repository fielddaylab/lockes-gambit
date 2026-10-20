using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Data;
using FieldDay.Scenes;
using FieldDay.SharedState;
using SpaceFab;
using SpaceFab.Overarching;
using SpaceFab.Save;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Save
{
    public class MinigameSaveStates : SharedStateComponent, IRegistrationCallbacks
    {
        public DesignSaveState Design;
        public FabricationSaveState Fabrication;
        public ResearchSaveState Research;
        public SupplySaveState Supply;

        #region Interfaces

        // IRegistrationCallbacks

        public void OnDeregister()
        {
            SpacefabGame.SaveBuffer.DeregisterHandler("DesignSaveState");
            SpacefabGame.SaveBuffer.DeregisterHandler("FabricationSaveState");
            SpacefabGame.SaveBuffer.DeregisterHandler("ResearchSaveState");
            SpacefabGame.SaveBuffer.DeregisterHandler("SupplySaveState");
        }

        public void OnRegister()
        {
            Design = new DesignSaveState();
            Fabrication = new FabricationSaveState();
            Research = new ResearchSaveState();
            Supply = new SupplySaveState();
            MinigameSaveUtility.ClearMinigameState(this);

            SpacefabGame.SaveBuffer.RegisterHandler("DesignSaveState", Design);
            SpacefabGame.SaveBuffer.RegisterHandler("FabricationSaveState", Fabrication);
            SpacefabGame.SaveBuffer.RegisterHandler("ResearchSaveState", Research);
            SpacefabGame.SaveBuffer.RegisterHandler("SupplySaveState", Supply);
        }

        #endregion // Interfaces
    }

    public static class MinigameSaveUtility
    {
        public static void ClearMinigameState(MinigameSaveStates saveStates)
        {
            saveStates.Design.SetDefaults();
            saveStates.Fabrication.SetDefaults();
            saveStates.Research.SetDefaults();
            saveStates.Supply.SetDefaults();
        }

        // Resolves a MinigameId to its save-state slot, or null for an id without one
        // (e.g. MinigameId.COUNT). Canonical mapping — used by the overarching alert auto-rule too.
        public static MinigameSaveStateBase GetState(MinigameSaveStates saveStates, MinigameId mg)
        {
            Assert.NotNullOrDestroyed(saveStates);
            switch (mg)
            {
                case MinigameId.Design:      return saveStates.Design;
                case MinigameId.Research:    return saveStates.Research;
                case MinigameId.Fabrication: return saveStates.Fabrication;
                case MinigameId.Supply:      return saveStates.Supply;
                default:                     Assert.Fail("Invalid minigame id {0}", mg); return null;
            }
        }

        // Marks the given minigame as started so the overarching alert auto-rule can tell a
        // started-but-unsolved minigame (Incomplete) from one never begun (NotStarted). The flag
        // is persisted on the next save.
        public static void MarkStarted(MinigameSaveStates saveStates, MinigameId mg)
        {
            MinigameSaveStateBase save = GetState(saveStates, mg);
            save.Started = true;
        }

        // True only when every minigame has FoundValidSolution. Used to gate the submit-chapter
        // button. Returns false defensively if the save states aren't available yet.
        public static bool AllSolved(MinigameSaveStates saveStates)
        {
            for (int i = 0; i < (int)MinigameId.COUNT; i++)
            {
                MinigameSaveStateBase save = GetState(saveStates, (MinigameId)i);
                if (save == null || !save.FoundValidSolution) { return false; }
            }
            return true;
        }

        public static int CurrentCosts(MinigameSaveStates saveStates) {
            return Math.Max(0, saveStates.Supply.FinalizedCost) + Math.Max(0, saveStates.Fabrication.FinalizedCost);
        }

        public static int CurrentTime(MinigameSaveStates saveStates) {
            return Math.Max(0, saveStates.Supply.FinalizedTotalCycles) + Math.Max(0, saveStates.Fabrication.FinalizedTotalCycles);
        }

        public static int CurrentRisk(MinigameSaveStates saveStates) {
            return Math.Max(0, saveStates.Supply.FinalizedRisk);
        }
    }
}