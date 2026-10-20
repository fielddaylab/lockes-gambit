using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.Systems;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Overarching {
    static public partial class OverarchingTransitions {
        static public void AdvanceContract() {
            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(ScriptUtility.RuntimeUpdateMask);
            Find.State(out ChapterState chapterState, out PlayerProgressState progressState, out ContractState contractState, out MinigameSaveStates saveStates);
            ChapterUtility.LoadNextChapter(chapterState, progressState, contractState, saveStates);
        }
        
        static public void ToMinigame(MinigameZone zone) {
            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(ScriptUtility.RuntimeUpdateMask);
            Game.Scenes.LoadMainScene(zone.Scene);
            Game.Events.Dispatch(GameEvents.OnMinigameLoad);
            
            SceneRequestContext loadContext = default;
            loadContext.Set("PreserveMusic", true);
            Game.Scenes.QueueMainLoadContext(loadContext);
        }
    }
}
