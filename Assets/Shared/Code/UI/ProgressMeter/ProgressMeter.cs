using FieldDay;
using FieldDay.Components;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceFab {
    /// <summary>
    /// View component for the ProgressMeter UI. Holds inspector references to the row
    /// title texts, cell containers, current-day marker, and cell prefab. Per-state
    /// sprites are pulled from the ProgressMeterSpriteSet GlobalAsset by the utility, so
    /// they are not configured here. Registers itself with ProgressMeterState on
    /// OnRegister, exposes a context-menu rebuild for editor authoring, and holds no
    /// logic itself — every mutation routes through ProgressMeterUtility.
    /// </summary>
    public class ProgressMeter : BatchedComponent, IRegistrationCallbacks {
        public TMP_Text CycleRowTitle;
        public TMP_Text FundsRowTitle;
        public RectTransform CycleCellContainer;
        public HorizontalLayoutGroup CycleCellLayout;
        public RectTransform FundsCellContainer;
        public HorizontalLayoutGroup FundsCellLayout;
        public RectTransform CurrentDayMarker;
        public ProgressMeterCell CycleCellPrefab;
        public ProgressMeterCell FundsCellPrefab;

        [NonSerialized] public ProgressMeterCell[] CycleCells;
        [NonSerialized] public ProgressMeterCell[] FundsCells;

        public void OnRegister()
        {
            ProgressMeterUtility.RebindCells(this);
            ProgressMeterUtility.TryRegisterMeter(this);
        }

        public void OnDeregister()
        {
            ProgressMeterUtility.TryUnregisterMeter(this);
        }

        // Editor-only manual rebuild. Invoked via the component's gear-icon context menu in
        // the inspector. Auto-rebuild on OnValidate was removed because scene-load-time
        // OnValidate firing under [ExecuteAlways] was leaking cell instantiation into the
        // hierarchy root before container references resolved.
        [ContextMenu("Rebuild Progress Meter Cells")]
        private void RebuildCellsContextMenu() {
            // ProgressMeterUtility.RebuildCells(this);
        }
    }
}
