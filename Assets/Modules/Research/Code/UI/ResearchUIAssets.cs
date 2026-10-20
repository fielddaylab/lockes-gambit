using FieldDay.Assets;
using UnityEngine;

namespace SpaceFab.Research {
    /// <summary>
    /// Global asset holding shared Research-UI visual values that are
    /// not chip-specific (chip sprites live on
    /// ResearchObservationChipAssets). Empty placeholder today — the
    /// previous tenants (pagination-dot tint colors) were retired when
    /// the paginator moved to sprite-layered dots. Add new shared UI
    /// tunables here as they come up.
    /// </summary>
    [CreateAssetMenu(menuName = "SpaceFab/Research/UI Assets")]
    public class ResearchUIAssets : GlobalAsset {
        // TODO: Replace chamber button sprites to the shared ButtonUp/ButtonDown sprites
        // with separate icon overlays to reduce the number of sprite assets.
        [Header("Chamber Button Sprites")]
        public Sprite VoltageButton;
        public Sprite ThermalButton;
        public Sprite DopingButton;
        public Sprite LockedChamberButton;

        [Header ("Atomic View Toggle")]
        public Sprite ButtonUp;
        public Sprite ButtonDown;

        [Header("Wiki Property Pages")]
        // When true, the decomposed-observation chips on wiki property
        // pages are clickable in the Research scene (same add/remove
        // routing as observation pages, minus the grey styling). When
        // false they are display-only.
        public bool PropertyPageObservationChipsClickable;

        [Header("Verification")]
        public Color DefaultColor;
        public Color SuccessColor;
        public Color FailureColor;
        public Color ProcessingColor;
        public Sprite SuccessIcon;
        public Sprite FailureIcon;
    }
}
