using System.Collections;
using System.Collections.Generic;
using BeauRoutine;
using BeauUtil;
using BeauUtil.Debugger;
using FieldDay;
using FieldDay.Debugging;
using FieldDay.Physics;
using FieldDay.Rendering;
using FieldDay.Systems;
using UnityEngine;

namespace SpaceFab.Supply 
{
    public sealed class CameraMoveSystem : SystemComponent
    {
        public override unsafe void RegisterSystems(ref SystemRegistrationTable ecs)
        {
            ecs.Register(&ProcessWork,
            new SysUpdate(GameLoopPhase.Update, 0, UpdateMasks.SupplyMask),
            new SysPermissions()
                .ReadWriteShared<SupplyCameraControlState>()
            );
        }

        static private void ProcessWork(float deltaTime)
        {
            Find.State(out SupplyCameraControlState cameraState);

            UpdateTarget(cameraState, deltaTime);

            ProcessLerp(cameraState, deltaTime);
        }

        private static void UpdateTarget(SupplyCameraControlState cameraState, float deltaTime)
        {
            Vector2 adjust = default;
            float moveSpeed = deltaTime * cameraState.MovementSpeed;
            if (Game.Input.IsKeyDown(KeyCode.A) || Game.Input.IsKeyDown(KeyCode.LeftArrow))
            {
                adjust.x -= 1;
            }
            if (Game.Input.IsKeyDown(KeyCode.D) || Game.Input.IsKeyDown(KeyCode.RightArrow)) {
                adjust.x += 1;
            }
            if (Game.Input.IsKeyDown(KeyCode.S) || Game.Input.IsKeyDown(KeyCode.DownArrow)) {
                adjust.y -= 1;
            }
            if (Game.Input.IsKeyDown(KeyCode.W) || Game.Input.IsKeyDown(KeyCode.UpArrow)) {
                adjust.y += 1;
            }

            if (adjust.x != 0 || adjust.y != 0)
            {
                adjust.Normalize();
                adjust.x *= moveSpeed;
                adjust.y *= moveSpeed;

                cameraState.TargetPosition += adjust;
            }
        }

        private static void ProcessLerp(SupplyCameraControlState cameraState, float deltaTime)
        {
            Vector2 frameSize = CameraUtility.GetFrustumSize(cameraState.Camera, 0);
            Rect region = cameraState.Region;

            Vector2 currentPos = cameraState.CameraPosition.position;
            Vector2 targetPos = cameraState.TargetPosition;
            targetPos = Geom.Constrain(targetPos, frameSize, region);

            cameraState.TargetPosition = targetPos;

            //DebugDraw.AddPoint(cameraState.TargetPosition, 0.05f, Color.red);

            currentPos = Vector2.LerpUnclamped(currentPos, targetPos, TweenUtil.Lerp(cameraState.InterpolationStrength, 1, deltaTime));
            currentPos = Geom.Constrain(currentPos, frameSize, region);
            cameraState.CameraPosition.SetPosition(currentPos, Axis.XY, Space.World);
        }
    }
}
