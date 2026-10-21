using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using FieldDay.Scripting;
using FieldDay.SharedState;
using SpaceFab.Save;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Overarching
{
    public class OverarchingLoader : SceneController, IScenePreload
    {
        protected override IEnumerator<WorkSlicer.Result?> OnScenePreload() {
            Find.State(out PlayerProgressState progress, out ProgressMeterState meter);
            ProgressMeterUtility.InitializeGoalsFromProgress(progress, meter);
            return base.OnScenePreload();
        }

        protected override void OnSceneReady() {
            Find.State(out ChapterState chapterState, out ContractViewState view);
            if (chapterState.LastSelectedContractIndex < 0) {
                view.QueuedMode = OverarchingContractMode.SelectNew;
            } else {
                OverarchingTransitions.RefreshShipInteractions();
            }
        }
    }

    static public partial class OverarchingTransitions {
        static public void RefreshShipInteractions() {
            Find.State<ProgressMeterState>().NeedsRefresh = true;

            RefreshButtons(Find.State<OverarchingContractButtons>(), Find.State<MinigameSaveStates>());

            SpacefabGame.Events.Dispatch(GameEvents.ShipMenuDisplayed);
            ScriptUtility.Trigger(ScriptTriggers.OnOverarchingLoaded);
        }
    }
}