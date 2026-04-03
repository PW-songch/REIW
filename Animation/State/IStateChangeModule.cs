using System;

namespace REIW.Animations
{
    public interface IStateChangeModule<TStateType> where TStateType : Enum
    {
        (bool Valid, TStateType Next) NextStateType { get; }

        bool CanExitState();
        bool CanChangeNextState(TStateType nextState);
        void ChangedState();
    }
}
