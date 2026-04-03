using System;
using REIW.BR;

namespace REIW.Animations.Character.BR
{
    public class CommonAnimationEventListener : CharacterAnimationEventListener, REIW.BR.ICharacterBaseEventListener
    {
        public event Action<eMovementState> ChangeMovementStateEvent;

        public CommonAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnChangeMovementState(eMovementState prevState, eMovementState changeState)
        {
            ChangeMovementStateEvent?.Invoke(changeState);
        }
    }
}
