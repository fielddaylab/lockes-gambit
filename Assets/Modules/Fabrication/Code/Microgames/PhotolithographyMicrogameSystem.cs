using FieldDay;
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
    /// Drives the Photolithography microgame's per-frame simulation while it is active. Runs on
    /// FixedUpdate at order 0 under AttemptMask; gated by PhotolithographyMicrogameState.IsActive.
    /// </summary>
    public class PhotolithographyMicrogameSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.FixedUpdate, 0, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<PhotolithographyMicrogameState>()
            );
        }

        // Early-returns when the microgame is not active. Active body is TODO until mechanics are authored.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(out PhotolithographyMicrogameState state, out MicrogameCanvasState canvasState);
            if (!state.IsActive) { return; }

            switch (state.Phase)
            {
                case PhotolithographyMicrogamePhase.Entering:
                    MicrogameCanvasUtility.ShowStationInstructions(canvasState, FabricationConsts.PHOTOLITHOGRAPHY_STATION_ID);
                    ProcessFalling(state, deltaTime);
                    break;

                case PhotolithographyMicrogamePhase.Active:
                    ProcessActive(state, deltaTime);
                    break;
            }
        }

        static private void ProcessActive(PhotolithographyMicrogameState state, float deltaTime)
        {
            if (!state.InputAccepted) { return; }

            float rotateSpeed = 120f;
            if (Game.Input.IsKeyDown(FabricationConsts.Left0) ||
                Game.Input.IsKeyDown(FabricationConsts.Left1)) {
                state.PhotomaskAngle += rotateSpeed * deltaTime;
            }

            if (Game.Input.IsKeyDown(FabricationConsts.Right0) ||
                Game.Input.IsKeyDown(FabricationConsts.Right1)) {
                state.PhotomaskAngle -= rotateSpeed * deltaTime;
            }

            if (Game.Input.IsKeyDown(FabricationConsts.Down0) ||
                Game.Input.IsKeyDown(FabricationConsts.Down1)) {
                state.FallSpeed = 1.5f;
            } else {
                state.FallSpeed = 0.5f;
            }

            state.PhotomaskAngle %= 360f;
            state.Photomask.transform.rotation = Quaternion.Euler(0f, 0f, state.PhotomaskAngle);

            ProcessFalling(state, deltaTime);
        }

        static private void ProcessFalling(PhotolithographyMicrogameState state, float deltaTime)
        {
            state.PhotomaskY -= state.FallSpeed * deltaTime;

            state.Photomask.transform.localPosition = new Vector3(
                state.Photomask.transform.localPosition.x,
                state.PhotomaskY,
                state.Photomask.transform.localPosition.z
            );

            float landingY = 0f;
            if (state.PhotomaskY <= landingY)
            {
                state.PhotomaskY = landingY;

                Find.State(out StationControlState stationState);
                MicrogameStationInterfacerUtility.SignalCompleted(stationState.ActiveInterfacer);
            }
        }
    }
}
