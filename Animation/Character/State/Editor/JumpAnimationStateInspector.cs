using UnityEditor;

namespace REIW.Animations.Character
{
    [CustomEditor(typeof(JumpAnimationState), true)]
    public class JumpAnimationStateInspector : AnimationStateInspector
    {
        protected override void Awake()
        {
            base.Awake();

            _hideProperties.Add(AirborneAnimationState.FallName);
        }
    }
}