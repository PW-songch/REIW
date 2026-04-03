using UnityEditor;

namespace REIW.Animations.Character
{
    [CustomEditor(typeof(WalkAnimationState), true)]
    public class WalkAnimationStateInspector : LocomotionAnimationStateInspector
    {
        protected override void Awake()
        {
            base.Awake();

            _hideProperties.AddRange(_moveProperties);
            _hideProperties.AddRange(_quickTurnProperties);
        }
    }
}