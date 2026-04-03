using System;

namespace REIW.Animations.Npc
{
    [Serializable]
    public class NpcAnimationStateMachine : AnimationStateMachine<eAnimationType, eStateType, NpcAnimationState, NpcAnimationStateMachine, NpcAnimation>
    {
        public CollisionAnimationState Collision => GetAnimationState<CollisionAnimationState>(eStateType.COLLISION);
        public CinematicAnimationState Cinematic => GetAnimationState<CinematicAnimationState>(eStateType.CINEMATIC);
        public FightAnimationState Fight => GetAnimationState<FightAnimationState>(eStateType.FIGHT);

        protected override void SetSecondaryCheckStates()
        {
            if (!_stateLoader)
                return;

            _secondaryCheckNextStateList = _stateLoader.GetStates<PlayTargetAnimationState>();
        }
    }
}