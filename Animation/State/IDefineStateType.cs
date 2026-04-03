using System;

namespace REIW.Animations
{
    public interface IDefineStateType<TStateType> where TStateType : Enum
    {
        TStateType GetStateType(in TStateType stateType);

        TStateType GetBaseStateType(in TStateType stateType);
    }
}
