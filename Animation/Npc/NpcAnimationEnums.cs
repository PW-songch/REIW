namespace REIW.Animations.Npc
{
    public enum eStateType : uint
    {
        NONE = 0,

        BASE_TYPE_START = NONE,
        IDLE,
        WALK,
        COLLISION,
        PLAY_TARGET_ANIMATION,
        CINEMATIC,

        // SubjectBehaviorState
        EAT,
        INTERFERE,
        FIGHT,
        ESCAPE,
        SLEEP,
        INTERACT,
        
        // ex) Next Attraction
        NEXT_TYPE_START = (BASE_TYPE_START / AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT + 1) * AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT,

        STATE_TYPE_END
    }

    /// <summary>
    /// eAnimationType 타입 값 : eStateType의 state 값 * 1000으로 애니메이션 타입의 값 적용
    /// eAnimationType 타입 추가 : eStateType의 state 이름 기준으로 각 state별 애니메이션 타입 이름 적용
    /// eAnimationType 세부 타입 추가 : 애니메이션의 각 state class에서 애니메이션 세부 타입 정의
    /// eStateType_TYPE_END : 필요에 따라 정의하여 사용 (ex: IDLE_TYPE_END = AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT + IDLE_TYPE_START - 1)
    /// </summary>
    public enum eAnimationType : uint
    {
        NONE = 0,

        /// <summary>
        /// IDLE
        /// </summary>
        IDLE_TYPE_START = eStateType.IDLE * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        IDLE,

        /// <summary>
        /// WALK
        /// </summary>
        WALK_TYPE_START = eStateType.WALK * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        WALK,

        /// <summary>
        /// COLLISION
        /// </summary>
        COLLISION_TYPE_START = eStateType.COLLISION * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// CINEMATIC
        /// </summary>
        CINEMATIC_TYPE_START = eStateType.CINEMATIC * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// EAT
        /// </summary>
        EAT_TYPE_START = eStateType.EAT * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        EAT,

        /// <summary>
        /// INTERFERE
        /// </summary>
        INTERFERE_TYPE_START = eStateType.INTERFERE * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        INTERFERE,

        /// <summary>
        /// Fight
        /// </summary>
        FIGHT_TYPE_START = eStateType.FIGHT * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        FIGHT,

        /// <summary>
        /// ESCAPE
        /// </summary>
        ESCAPE_TYPE_START = eStateType.ESCAPE * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        ESCAPE,

        /// <summary>
        /// SLEEP
        /// </summary>
        SLEEP_TYPE_START = eStateType.SLEEP * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        SLEEP,

        TYPE_END = (eStateType.STATE_TYPE_END - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
    }

    public static class NpcAnimationEnums
    {
        public static readonly int ANIMATION_TYPE_BIT_DIGITS = Utilities.BitsForValue((int)eAnimationType.TYPE_END);

        public static eAnimationType SetDontChangeAnimationNetObject(this eAnimationType InAnimationType)
        {
            return EnumUtility.PackFlag(InAnimationType, ANIMATION_TYPE_BIT_DIGITS);
        }

        public static eStateType GetStateType(uint InAnimationType)
        {
            return (InAnimationType / AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT).ToEnum<eStateType>();
        }
    }
}