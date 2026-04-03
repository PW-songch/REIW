namespace REIW.Animations.Npc
{
    public class NpcAnimationState : AnimationState<eAnimationType, eStateType, NpcAnimationState, NpcAnimationStateMachine, NpcAnimation>
    {
        public override eStateType StateType => eStateType.NONE;
    }
}