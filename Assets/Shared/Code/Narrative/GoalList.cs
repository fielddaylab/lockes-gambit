using BeauUtil;
using FieldDay.Assets;
using System;
using UnityEngine;

namespace SpaceFab {
    [CreateAssetMenu(menuName = "SpaceFab/Overarching/Goal List")]

    public class GoalList : GlobalAsset {
        [SerializeField] public ProgressGoal[] Goals;
    }

    [Serializable]
    public struct ProgressGoal {
        public string Name;
        public SerializedHash32 Id;
        public int TargetFunds;
        public Sprite Icon;
    }
}