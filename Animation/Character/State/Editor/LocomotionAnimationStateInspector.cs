using System.Collections.Generic;
using UnityEditor;

namespace REIW.Animations.Character
{
    [CustomEditor(typeof(LocomotionAnimationState), true)]
    public class LocomotionAnimationStateInspector : AnimationStateInspector
    {
        protected readonly HashSet<string> _moveProperties = new()
        {
            LocomotionAnimationState.MoveStartName,
            LocomotionAnimationState.MoveMixerName,
        };

        protected readonly HashSet<string> _footStartMoveNormalizedTimeProperties = new()
        {
            LocomotionAnimationState.LeftFootStartMoveNormalizedTimeName,
            LocomotionAnimationState.RightFootStartMoveNormalizedTimeName,
        };

        protected readonly HashSet<string> _turnProperties = new()
        {
            LocomotionAnimationState.TurnLeftName,
            LocomotionAnimationState.TurnRightName,
            LocomotionAnimationState.TurnLeftRotationDataName,
            LocomotionAnimationState.TurnRightRotationDataName,
            LocomotionAnimationState.TurnAngleName,
            LocomotionAnimationState.TurnRootMotionRotationSpeedName,
            LocomotionAnimationState.TurnRootMotionRoationSpeedNormalizedTimeName,
        };

        protected readonly HashSet<string> _quickTurnProperties = new()
        {
            LocomotionAnimationState.QuickTurnLeftName,
            LocomotionAnimationState.QuickTurnRightName,
            LocomotionAnimationState.QuickTurnLeftRotationDataName,
            LocomotionAnimationState.QuickTurnRightRotationDataName,
            LocomotionAnimationState.QuickTurnMoveSpeedName,
            LocomotionAnimationState.QuickTurnAngleName,
            LocomotionAnimationState.QuickTurnRootMotionRotationSpeedName,
            LocomotionAnimationState.ContinuousQuickTurnNormalizedTimeName,
        };

        protected readonly HashSet<string> _stopProperties = new()
        {
            LocomotionAnimationState.StandStopName,
            LocomotionAnimationState.MoveStopName,
            LocomotionAnimationState.TurnEnableStopNormalizedTimeName,
        };
    }
}
