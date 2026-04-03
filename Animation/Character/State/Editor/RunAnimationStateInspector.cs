using UnityEditor;
using UnityEngine;

namespace REIW.Animations.Character
{
    [CustomEditor(typeof(RunAnimationState), true)]
    public class RunAnimationStateInspector : LocomotionAnimationStateInspector
    {
        protected override void Awake()
        {
            base.Awake();

            _hideProperties.AddRange(new string[]
            {
                LocomotionAnimationState.QuickTurnLeftRotationDataName,
                LocomotionAnimationState.QuickTurnRightRotationDataName,
            });
        }
    }
}
