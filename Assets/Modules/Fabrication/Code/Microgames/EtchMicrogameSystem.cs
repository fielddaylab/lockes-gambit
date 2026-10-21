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
    /// Drives the Plasma Etcher microgame's per-frame simulation while it is active. Runs on
    /// FixedUpdate at order 0 under AttemptMask; gated by EtchMicrogameState.IsActive.
    /// </summary>
    public class EtchMicrogameSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
                new SysUpdate(GameLoopPhase.FixedUpdate, 0, UpdateMasks.AttemptMask),
                new SysPermissions()
                    .ReadWriteShared<EtchMicrogameState>()
            );
        }

        // Early-returns when the microgame is not active. Active body is TODO until mechanics are authored.
        static private void ProcessWork(float deltaTime)
        {
            Find.State(out EtchMicrogameState state, out MicrogameCanvasState canvasState);
            if (!state.IsActive) { return; }

            switch (state.Phase)
            {
                case EtchMicrogamePhase.Entering:
                    MicrogameCanvasUtility.ShowStationInstructions(canvasState, FabricationConsts.ETCH_STATION_ID);
                    ProcessEntering(state, deltaTime);
                    break;

                case EtchMicrogamePhase.Active:
                    ProcessActive(state, deltaTime);
                    break;
            }
        }

        static private void ProcessEntering(EtchMicrogameState state, float deltaTime)
        {
            if (state.CachedPreviewPoints.Count == 0)
                return;

            float progressSpeed = 25f; // 20f;
            state.PreviewProgress += deltaTime * progressSpeed;

            int visibleCount = Mathf.Clamp(Mathf.FloorToInt(state.PreviewProgress),
                0, state.CachedPreviewPoints.Count);
            
            if (visibleCount != state.PreviewVisibleCount)
            {
                state.PreviewVisibleCount = visibleCount;
                state.Pattern.PreviewBeam.positionCount = visibleCount;

                for (int i = 0; i < visibleCount; i++)
                {
                    state.Pattern.PreviewBeam.SetPosition(i, state.CachedPreviewPoints[i]);
                }
            }

            if (state.PreviewVisibleCount >= state.CachedPreviewPoints.Count)
            {
                state.Phase = EtchMicrogamePhase.Active;
            }
        }

        static private void ProcessActive(EtchMicrogameState state, float deltaTime)
        {
            if (!state.InputAccepted)
                return;

            Vector2 newDirection = state.Direction;
            if (Game.Input.IsKeyDown(FabricationConsts.Up0) || Game.Input.IsKeyDown(FabricationConsts.Up1))
                newDirection = Vector2.up;
            else if (Game.Input.IsKeyDown(FabricationConsts.Down0) || Game.Input.IsKeyDown(FabricationConsts.Down1))
                newDirection = Vector2.down;
            else if (Game.Input.IsKeyDown(FabricationConsts.Left0) || Game.Input.IsKeyDown(FabricationConsts.Left1))
                newDirection= Vector2.left;
            else if (Game.Input.IsKeyDown(FabricationConsts.Right0) || Game.Input.IsKeyDown(FabricationConsts.Right1))
                newDirection = Vector2.right;

            if (newDirection != state.Direction) {
                state.TurnCount++;
                state.Direction = newDirection;
            }

            bool hitTurnCap = state.TurnCount > state.CachedPreviewPoints.Count * 2;

            float beamSpeed = 1.875f; //1.5f;
            if (Game.Input.IsKeyDown(KeyCode.Space)) {
                beamSpeed = 2.375f;
            }
            Vector2 current = state.PlayerPoints[state.PlayerPoints.Count - 1];
            Vector2 next = current + (Vector2)(state.Direction * beamSpeed * deltaTime);

            float waferRadius = 2.8f;
            bool hitEdge = next.sqrMagnitude > waferRadius * waferRadius;

            if (hitTurnCap || hitEdge)
            {
                Find.State(out StationControlState stationState);
                MicrogameStationInterfacerUtility.SignalCompleted(stationState.ActiveInterfacer);
                return;
            }

            state.PlayerPoints.Add(next);
            state.PlayerBeam.positionCount = state.PlayerPoints.Count;
            state.PlayerBeam.SetPosition(state.PlayerPoints.Count - 1, next);
        }
    }
}
