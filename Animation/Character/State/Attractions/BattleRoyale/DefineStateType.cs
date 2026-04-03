namespace REIW.Animations.Character.BR
{
    public class DefineStateType : Character.DefineStateType
    {
        public override eStateType GetStateType(in eStateType stateType)
        {
            if (stateType < eStateType.BR_TYPE_START)
                return (eStateType)((int)eStateType.BR_TYPE_START + ((int)stateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT));

            return stateType;
        }
    }
}
