using FieldDay;
using FieldDay.Audio;
using FieldDay.SharedState;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Robot
{
    public enum RobotStatus
    {
        Idle,
        Holding,
        Stunned
    }
    
    /// <summary>
    /// Holds robot (player avatar) data.
    /// </summary>
    public class RobotState : SharedStateComponent, IRegistrationCallbacks
    {
        [NonSerialized] public bool IsStunned = false;
        public RobotStatus Status = RobotStatus.Idle;

        public void OnRegister()
        {
            Find.State(out RobotVisualsState visualsState);
            RobotVisualsUtility.UpdateVisuals(visualsState, Status);
        }

        public void OnDeregister()
        {

        }
    }

    public static class RobotUtility
    {
        public static void UpdateStatus(RobotState robotState, RobotStatus status)
        {
            Find.State(out RobotVisualsState visualsState);
            RobotVisualsUtility.UpdateVisuals(visualsState, status);
        }

        public static void ApplyStun(RobotState robotState, RobotVisualsState visualsState)
        {
            if (robotState.IsStunned)
            {
                // handle stacked stun if desired
                return;
            }
            robotState.Status = RobotStatus.Stunned;
            robotState.IsStunned = true;
            RobotVisualsUtility.ApplyStunVisuals(visualsState);

            Sfx.Play("Fabrication.Robot.Stunned");
        }

        // Clears the stun flag and removes its visuals. Called by StationControlSystem when the Stunned
        // phase elapses, or by any other system that needs to end a stun early.
        public static void RemoveStun(RobotState robotState, RobotVisualsState visualsState)
        {
            if (!robotState.IsStunned)
            {
                return;
            }

            robotState.Status = RobotStatus.Holding;
            robotState.IsStunned = false;
            RobotVisualsUtility.RemoveStunVisuals(visualsState);
        }
    }
}