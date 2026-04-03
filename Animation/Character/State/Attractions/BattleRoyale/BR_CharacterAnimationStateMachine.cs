using System.Collections.Generic;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimationStateMachine
    {
        public BR.IdleAnimationState BR_Idle => GetAnimationState<BR.IdleAnimationState>(eStateType.BR_IDLE);
        public BR.RunAnimationState BR_Run => GetAnimationState<BR.RunAnimationState>(eStateType.BR_RUN);
        public BR.AdsAnimationState BR_Ads => GetAnimationState<BR.AdsAnimationState>(eStateType.BR_ADS);

        private void CreateBattleRoyaleNextStateChangeModules(CharacterAnimation animation)
        {
            if (_stateChangeModules == null)
                _stateChangeModules = new ();

            var modules = new List<IStateChangeModule<eStateType>>();
            // Locomotion
            {
                modules.Add(new BR.LocomotionStateChangeModule(animation));
                _stateChangeModules.Add(eStateType.IDLE, modules);
                _stateChangeModules.Add(eStateType.WALK, modules);
                _stateChangeModules.Add(eStateType.RUN, modules);
                _stateChangeModules.Add(eStateType.SPRINT, modules);
                _stateChangeModules.Add(eStateType.BR_IDLE, modules);
                _stateChangeModules.Add(eStateType.BR_WALK, modules);
                _stateChangeModules.Add(eStateType.BR_RUN, modules);
                _stateChangeModules.Add(eStateType.BR_SPRINT, modules);
            }
        }
    }
}
