using System;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class StateChangeModule : IStateChangeModule<eStateType>
    {
        private WeakReference<CharacterAnimation> _characterAnimation;
        protected CharacterAnimation Animation =>
            _characterAnimation?.TryGetTarget(out var animation) == true ? animation : null;

        public virtual (bool Valid, eStateType Next) NextStateType
        {
            get
            {
                return (false, eStateType.NONE);
            }
        }

        public StateChangeModule(MonoBehaviour animation)
        {
            _characterAnimation = new WeakReference<CharacterAnimation>(animation as CharacterAnimation);
        }

        public virtual bool CanExitState()
        {
            return false;
        }

        public virtual bool CanChangeNextState(eStateType nextState)
        {
            return true;
        }

        public virtual void ChangedState()
        {
        }
    }
}