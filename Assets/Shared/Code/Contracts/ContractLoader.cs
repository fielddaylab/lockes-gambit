using System.Collections.Generic;
using BeauUtil;
using FieldDay;
using FieldDay.Scenes;
using UnityEngine;

namespace SpaceFab {
    [PreloadOrder(-99)]
    public sealed class ContractLoader : MonoBehaviour, IScenePreload {
        public bool CurrentOnly;

        public IEnumerator<WorkSlicer.Result?> Preload() {
            Find.State(out ChapterState chapterState, out ContractState contractState, out PlayerProgressState playerProgress);

            if (CurrentOnly) {
                ContractUtility.LoadCurrentContract(contractState, chapterState, ContractLoadAction.Default);
            } else {
                ContractUtility.LoadQueuedContract(contractState, chapterState, playerProgress);
            }
            while (contractState.LoadRoutine) {
                yield return WorkSlicer.Result.HaltForFrame;
            }
        }
    }
}