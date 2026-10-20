using BeauUtil;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.Systems;
using SpaceFab.Overarching;
using SpaceFab.Research;
using SpaceFab.Supply;
using SpaceFab.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab {
    /// <summary>
    /// Listens for a confirmed exit-minigame request and hands control to the exit pipeline
    /// by flipping MinigameLoadExitState.Phase to Exiting and switching the active update mask
    /// to MinigameTransitionMask. Runs on any Update phase at order 10.
    /// </summary>
    public class MinigameRequestExitSystem : SystemComponent {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs) {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhaseMask.Update, 10),
                new SysPermissions()
                    .ReadWriteShared<MinigameRequestExitState>()
            );
        }

        // Once the request is Confirmed, begin the exit flow and swap update masks.
        static private void ProcessWork(float deltaTime) {
            Find.State(
                out MinigameRequestExitState requestExitState,
                out MinigameStateInterfacer minigameInterfacer
                );

            switch (requestExitState.ExitRequestState) {
                case RequestState.Requested:
                    if (minigameInterfacer.Id == MinigameId.Research) {
                        Find.State<ResearchResultPanel>().Show();
                        requestExitState.ExitRequestState = RequestState.Pending;
                    } else if (DisplayLeavePopup(minigameInterfacer.Id)) {
                        requestExitState.ExitRequestState = RequestState.Pending;
                    } else {
                        goto case RequestState.Confirmed;
                    }
                    break;
                case RequestState.Pending:
                    break;
                case RequestState.Confirmed:
                    // begin exit system
                    MinigameUtility.Exit();
                    requestExitState.ExitRequestState = RequestState.None;
                    break;
                case RequestState.None:
                default:
                    break;
            }
        }

        static private bool DisplayLeavePopup(MinigameId minigame) {
            PopupRequestContent request = default;
            request.Header = "Return to Ship?";
            request.Callback = OnLeavePopupSelected;

            request.ButtonA = new PopupRequestButton() {
                Label = "Return to Ship",
                ResponseId = "Yes",
                Tint = new ColorPalette2(Color.black, new Color32(255, 137, 137, 255))
            };
            request.ButtonB = new PopupRequestButton() {
                Label = "Cancel",
                ResponseId = "No",
                Tint = new ColorPalette2(Color.black, new Color32(255, 255, 255, 255))
            };

            switch (minigame) {
                case MinigameId.Research: {
                    return false;
                }

                case MinigameId.Design: {
                    request.Text = "Progress on this level will be saved.";
                    break;
                }

                case MinigameId.Fabrication: {
                    request.Text = "Progress will be lost.";
                    break;
                }

                case MinigameId.Supply: {
                    request.Text = "Progress on this level will be saved.";
                    break;
                }
            }

            PopupPrompt popup = Find.Panel<PopupPrompt>();
            popup.Populate(request);
            popup.Show();
            return true;
        }

        static private void OnLeavePopupSelected(StringHash32 option) {
            Find.State(out MinigameRequestExitState requestExitState);

            if (option == "Yes") {
                requestExitState.ExitRequestState = RequestState.Confirmed;
            } else {
                requestExitState.ExitRequestState = RequestState.None;
            }
        }
    }
}
