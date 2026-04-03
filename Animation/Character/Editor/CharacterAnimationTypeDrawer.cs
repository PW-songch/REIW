using UnityEditor;

namespace REIW.Animations.Character
{
    [CustomPropertyDrawer(typeof(eAnimationType))]
    public class CharacterAnimationTypeDrawer : AnimationTypeDrawer<eStateType>
    {
        protected override eStateType GetStateType(int animationType)
        {
            return (unchecked((uint)animationType) / AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT).ToEnum<eStateType>();
        }
    }
}
