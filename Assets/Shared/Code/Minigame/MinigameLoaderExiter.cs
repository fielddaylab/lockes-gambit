using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Save;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab
{
    // Imports save state ahead of the per-minigame loaders, which run at higher preload orders and
    // may depend on what was imported.
    [PreloadOrder(0)]
    public class MinigameLoaderExiter : MonoBehaviour, ISceneLoadHandler, IScenePreload {
        public void OnSceneLoad(SceneBinding inScene, object inContext) {
            Find.State(
                out MinigameStateInterfacer interfacer,
                out MinigameSaveStates saveStates
                );
            Find.State(out SaveLoadState saveOpState);

            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(interfacer.MinigameState.DefaultUpdateMask);
            Log.Msg("[SupplyBug] resuming default mask");

            using (var table = TempVarTable.Alloc()) {
                table.Set("minigame", interfacer.Id.ToString().ToLower());
                ScriptUtility.Trigger(ScriptTriggers.OnMinigameLoad, table);
            }
        }

        public IEnumerator<WorkSlicer.Result?> Preload() {
            Find.State(
                out MinigameStateInterfacer interfacer,
                out MinigameSaveStates saveStates
                );
            Find.State(out SaveLoadState saveOpState);

            Debug.Log("[MinigameLoadExitSystem] Importing state...");
            interfacer.MinigameState.ImportState(saveStates);

            yield return null;

            Debug.Log("[MinigameLoadExitSystem] Imported!");
            // Entering a minigame counts as starting it — record it so the overarching
            // alert auto-rule shows Incomplete (not NotStarted) on the next visit. Persisted
            // by the save on exit below.
            MinigameSaveUtility.MarkStarted(saveStates, interfacer.Id);
            // Suspend everything, then resume only the incoming minigame's own update mask
        }
    }

    static public partial class MinigameUtility {
        static public void Exit(SceneReference sceneRef = default) {
            GameLoop.SuspendUpdates(UpdateMasks.EntireGame);
            GameLoop.ResumeUpdates(ScriptUtility.RuntimeUpdateMask);
            Routine.Start(GameLoop.Host, ExecuteExit(sceneRef));
        }

        static private IEnumerator ExecuteExit(SceneReference reloadTarget) {
            Find.State(
                out MinigameStateInterfacer interfacer,
                out MinigameSaveStates saveStates,
                out GlobalUISceneConfig globalSceneConfig
                );
            Find.State(out SaveLoadState saveOpState);

            Debug.Log("[MinigameLoadExitSystem] Exporting state...");

            interfacer.MinigameState.MergeState();
            interfacer.MinigameState.ExportState(ref saveStates);
            SaveUtility.Save(SaveSlot.Main);

            while (saveOpState.Operation) {
                yield return null;
            }

            Debug.Log("[MinigameLoadExitSystem] Exported!");
            // A reload target (set by an in-minigame "next level" flow) reloads the current
            // minigame instead of returning to overarching. Consume the flag so the next
            // real exit falls through to the return scene. The OnMinigameExit event is
            // overarching-facing, so suppress it on a same-minigame reload.
            if (reloadTarget.IsValid) {
                // forceReload: the reload target is the minigame's own (already-active) scene,
                // so the default no-reload-if-loaded path would be a no-op. Force a fresh load
                // so the re-import lands on the next first-unsolved level.
                Game.Scenes.LoadMainScene(reloadTarget, true);
            } else {
                Game.Events.Dispatch(GameEvents.OnMinigameExit);
                Game.Scenes.LoadMainScene(globalSceneConfig.ReturnScene);
            }

            SceneRequestContext loadContext = default;
            loadContext.Set("PreserveMusic", true);
            Game.Scenes.QueueMainLoadContext(loadContext);
        }
    }
}