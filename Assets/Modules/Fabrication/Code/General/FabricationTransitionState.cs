using FieldDay.Data;
using FieldDay.SharedState;
using FieldDay.Systems;
using SpaceFab.Fabrication.Sequence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication
{
    /// <summary>
    /// Facilitates transitioning into and out of the Fabrication minigame
    /// Loads and shuts down relevant systems.
    /// </summary>
    public class FabricationTransitionState : SharedStateComponent, IEditorOnlyData {

        [Header("-- DEBUG --")]
        public FabricationLevel DEBUG_FabricationLevel;

#if UNITY_EDITOR
        void IEditorOnlyData.ClearEditorData(bool isDevelopmentBuild) {
            DEBUG_FabricationLevel = null;
        }
#endif // UNITY_EDITOR
    }
}