using System;
using BeauUtil;
using BeauUtil.UI;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using FieldDay.UI.Widgets;
using SpaceFab.Save;
using SpaceFab.UI;
using UnityEngine;

namespace SpaceFab.Supply {
    public sealed class SupplyResultPanel : SharedPanel {
        public GuiButton NextButton;
        public SceneReference NextScene;

        public Transform ShoppingListParent; // to copy the visuals from the shopping list to this
        public Transform ListParent;

        [Header("Ships")]
        public SupplyProgressMeterView MeterView;
        public SupplyShipBreakdownRow[] ShipRows;

        protected override void Awake() {
            base.Awake();

            NextButton.OnClick.AddListener(Commit);
        }

        private void Start() {
            Hide();
        }

        private void Commit() {
            Find.State<MinigameRequestExitState>().ExitRequestState = RequestState.Confirmed;
        }

        // Safety net for the pushed priority: OnDisable fires both on Hide() (SetActive(false)) and
        // on scene-teardown deactivation, and runs before OnDestroy invalidates the input layer, so
        // PopPriority's not-destroyed assert still holds. This is what catches the Commit-then-exit
        // path where Hide() never runs.
        private void OnDisable() {
            ReleasePriority();
        }

        // Pushes GUI input priority for this panel, at most once.
        private void AcquirePriority() {
            Input.TryPushPriority();
        }

        // Pops the priority pushed by AcquirePriority, if held. Skipped during shutdown (the GuiMgr
        // stack is being torn down anyway and the layer may already be invalid).
        private void ReleasePriority() {
            Input.TryPopPriority();
        }

        public override void Show() {
            base.Show();

            AcquirePriority();
            Input.SetInputOverride(null);

            Find.State(out SupplyRouteCollection routes, out SupplyMinigameState minigameState, out SupplyShipIndex ships, out SupplyRouteDrawingState draw);

            int cost = 0,
                time = 0,
                risk = 0;

            for (int i = 0; i < ships.ShipCount; i++) {
                cost += routes.RouteStats[i].Cost;
                time = Math.Max(time, routes.RouteStats[i].Time);
                risk += routes.RouteStats[i].Risk;
            }

            minigameState.Cost = cost;
            minigameState.TotalCycles = time;
            minigameState.Reliability = risk;

            minigameState.FoundValidSolution = true;

            // Refresh the route mirror before exporting - ExportState writes it out, and it still
            // holds whatever was imported at scene entry until something captures the live routes.
            SupplyRouteSaveUtility.Capture(routes, draw, ships, minigameState);

            ref SupplySaveState saveState = ref Find.State<MinigameSaveStates>().Supply;
            SupplyStateUtility.ExportState(ref saveState, minigameState);

            Populate(minigameState);

            CopyShoppingList();
        }

        private void Populate(SupplyMinigameState minigameState) {

        }

        private void CopyShoppingList()
        {
            for (int i = 0; i < ShoppingListParent.childCount; i++)
            {
                GameObject requirement = ShoppingListParent.GetChild(i).gameObject;
                Instantiate(requirement, ListParent);
            }
        }

        public override void Hide() {
            base.Hide();

            // base.Hide() deactivates the GameObject, which fires OnDisable -> ReleasePriority; this
            // call is a guarded no-op in that case but keeps the Show/Hide pairing explicit.
            ReleasePriority();
            Input.SetInputOverride(false);
        }
    }
}