using UnityEditor;

namespace REIW.Animations.Character
{
    [CustomEditor(typeof(SprintAnimationState), true)]
    public class SprintAnimationStateInspector : LocomotionAnimationStateInspector
    {
        protected override void Awake()
        {
            base.Awake();

            _hideProperties.AddRange(new string[]
            {
                LocomotionAnimationState.QuickTurnLeftRotationDataName,
                LocomotionAnimationState.QuickTurnRightRotationDataName,
            });

            _hideProperties.AddRange(_moveProperties);
            _hideProperties.AddRange(_turnProperties);
        }
    }
}