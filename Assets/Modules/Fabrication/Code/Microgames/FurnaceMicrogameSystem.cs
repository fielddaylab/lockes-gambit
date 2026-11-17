using FieldDay;
using FieldDay.Audio;
using FieldDay.Systems;
using SpaceFab.Fabrication.Layout;
using SpaceFab.Fabrication.StationControl;
using SpaceFab.Fabrication.Stations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    /// <summary>
    /// Drives the Furnace microgame's per-frame simulation while it is active. Runs on FixedUpdate
    /// at order 0 under AttemptMask; gated by FurnaceMicrogameState.IsActive.
    /// </summary>
    public class FurnaceMicrogameSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.FixedUpdate, 0, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<FurnaceMicrogameState>()
            );
        }

        // Early-returns when the microgame is not active. Active body is TODO until mechanics are authored.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(
                out FurnaceMicrogameState microgameState, 
                out MicrogameCanvasState canvasState // use for enabling/disabling fader and popups
            );

            if (!microgameState.IsActive) { return; }

            switch (microgameState.Phase)
            {
                case FurnaceMicrogamePhase.Idle:
                    break;
                case FurnaceMicrogamePhase.Entering:
                    MicrogameCanvasUtility.ShowStationInstructions(canvasState, FabricationConsts.FURNACE_STATION_ID);
                    break;
                case FurnaceMicrogamePhase.Active:
                    ProcessActive(microgameState, deltaTime);
                    break;
                default:
                    break;
            }
        }

        private static void ProcessActive(FurnaceMicrogameState state, float deltaTime)
        {
            if (state.InputAccepted && Game.Input.IsKeyDown(FabricationConsts.Activate))
            {
                if (!state.isSpacebarHeld) {
                    state.HeatSfx = Sfx.Play("Fabrication.Furnace.Heat");
                    state.isSpacebarHeld = true;
                }

                if (state.IncreasingHeat)
                {
                    state.CurrentValue += state.Sensitivity * deltaTime;
                    if (state.CurrentValue >= state.MaxRange)
                    {
                        state.IncreasingHeat = false;
                    }
                }
                else
                {
                    state.CurrentValue -= state.Sensitivity * deltaTime;
                    if (state.CurrentValue <= 0)
                    {
                        state.IncreasingHeat = true;
                    }
                }

                float percentage = state.CurrentValue / state.MaxRange;
                state.MeterArrowAnchor.rotation = Quaternion.Euler(new Vector3(0, 0, -percentage * 180));

                Sfx.SetVolume(state.HeatSfx, percentage + 0.2f);
            }
            else if (state.InputAccepted && state.isSpacebarHeld)
            {
                Sfx.Stop(state.HeatSfx);
                Sfx.Play("Fabrication.Furnace.Exit");

                state.FinalHeat = state.CurrentValue;
                state.isSpacebarHeld = false;
                state.InputAccepted = false;
                Find.State(out StationControlState stationState);
                MicrogameStationInterfacerUtility.SignalCompleted(stationState.ActiveInterfacer);
                return;
            }
        }
    }
}
