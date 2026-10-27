using System;
using System.Collections;
using System.Collections.Generic;
using BeauUtil;
using FieldDay;
using FieldDay.SharedState;
using FieldDay.UI;
using UnityEngine;

namespace SpaceFab.Supply 
{
    public class SupplyCameraControlState : SharedStateComponent, IRegistrationCallbacks
    {
        [Header("Components")]
        public Camera Camera;
        public Transform CameraPosition;
        public PhysicsInputLayer WorldInput;

        [Header("Configuration")]
        public Rect Region;
        public float InterpolationStrength;
        public float MovementSpeed;
        
        [NonSerialized] public Vector2 TargetPosition;
        
        public void OnDeregister()
        {
        }

        public void OnRegister()
        {
            TargetPosition = CameraPosition.position;
        }
    }
}