namespace REIW.Animations.Character
{
    public class DefineStateType : IDefineStateType<eStateType>
    {
        public virtual eStateType GetStateType(in eStateType stateType)
        {
            return stateType;
        }

        public virtual eStateType GetBaseStateType(in eStateType stateType)
        {
            return (eStateType)((int)stateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        }
    }
}
