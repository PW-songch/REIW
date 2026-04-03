using System;

namespace REIW.Animations.Character
{
    [Serializable]
    public partial class CharacterAnimationStateMachine : AnimationStateMachine<eAnimationType, eStateType, CharacterAnimationState, CharacterAnimationStateMachine, CharacterAnimation>
    {
        private eStateType _immediate_nextstateType = eStateType.NONE;

        public NetworkAnimationState Network => GetAnimationState<NetworkAnimationState>(eStateType.NETWORK);
        public IdleAnimationState Idle => GetAnimationState<IdleAnimationState>(eStateType.IDLE);
        public RunAnimationState Run => GetAnimationState<RunAnimationState>(eStateType.RUN);
        public DashAnimationState Dash => GetAnimationState<DashAnimationState>(eStateType.DASH);
        public SprintAnimationState Sprint => GetAnimationState<SprintAnimationState>(eStateType.SPRINT);
        public WalkAnimationState Walk => GetAnimationState<WalkAnimationState>(eStateType.WALK);
        public AirborneAnimationState Airborne => GetAnimationState<AirborneAnimationState>(eStateType.AIRBORNE);
        public JumpAnimationState Jump => GetAnimationState<JumpAnimationState>(eStateType.JUMP);
        public ParkourAnimationState Parkour => GetAnimationState<ParkourAnimationState>(eStateType.PARKOUR);
        public GrappleAnimationState Grapple => GetAnimationState<GrappleAnimationState>(eStateType.GRAPPLE);
        public MountAnimationState Mount => GetAnimationState<MountAnimationState>(eStateType.MOUNT);
        public InteractionAnimationState Interaction => GetAnimationState<InteractionAnimationState>(eStateType.INTERACTION);
        public GatheringAnimationState Gathering => GetAnimationState<GatheringAnimationState>(eStateType.GATHERING);
        public FishingAnimationState Fishing => GetAnimationState<FishingAnimationState>(eStateType.FISHING);
        public GlidingAnimationState Gliding => GetAnimationState<GlidingAnimationState>(eStateType.GLIDING);
        public EmoteAnimationState Emote => GetAnimationState<EmoteAnimationState>(eStateType.EMOTE);
        public CinematicAnimationState Cinematic => GetAnimationState<CinematicAnimationState>(eStateType.CINEMATIC);

        public bool IsCurrentNormalMoveState
        {
            get
            {
                var stateType = CurrentState.StateBaseType;
                switch (stateType)
                {
                    case eStateType.WALK:
                    case eStateType.RUN:
                    case eStateType.SPRINT:
                        return true;
                }
                return false;
            }
        }

        protected override void SetSecondaryCheckStates()
        {
            if (!_stateLoader)
                return;

            _secondaryCheckNextStateList = _stateLoader.GetStates<PlayTargetAnimationState>();
        }

        protected override void CreateNextStateChangeModules(CharacterAnimation animation, in EnumAttraction attractionType)
        {
            switch (attractionType)
            {
                case EnumAttraction.BattleRoyale:
                    CreateBattleRoyaleNextStateChangeModules(animation);
                    break;
            }
        }

        public bool IsImmediateNextStateType(CharacterAnimationState characterState)
        {
            if (_immediate_nextstateType == eStateType.NONE)
                return false;
            if (_immediate_nextstateType != characterState.StateType)
                return false;

            _immediate_nextstateType = eStateType.NONE;
            return true;
        }

        public void SetImmediateNextStateType(eStateType nextStateType)
        {
            if (CurrentState.StateType == _immediate_nextstateType)
                return;
            
            _immediate_nextstateType = nextStateType;   
        }
        
        public void ResetStateType(eStateType type = eStateType.NONE) => _immediate_nextstateType = type;

        public (bool HasNext, eStateType Next) CheckImmediateNextStateType()
        {
            bool hasNext = _immediate_nextstateType != eStateType.NONE;
            return (hasNext, _immediate_nextstateType);
        }
    }
}
