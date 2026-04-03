namespace REIW.Animations.Character
{
    public enum eStateType : uint
    {
        NONE = 0,

        // Playable
        IDLE,
        WALK,
        RUN,
        SPRINT,
        DASH,
        AIRBORNE,
        JUMP,
        PARKOUR,
        GRAPPLE,
        MOUNT,
        INTERACTION,
        GATHERING,
        MOVE_FAIL,
        FISHING,
        GLIDING,
        PLAY_TARGET_ANIMATION,

        // Non-playable
        NON_PLAYABLE_TYPE_START = AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT / 2,
        CHARACTER_CUSTOMIZING,
        CHARACTER_STAGE,
        EMOTE,
        RACE_SELECTION,
        CINEMATIC,
        CHARACTER_SELECTION,

        // Battle Royale
        BR_TYPE_START = (NONE / AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT + 1) * AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT,
        BR_IDLE,
        BR_WALK,
        BR_RUN,
        BR_SPRINT,
        BR_DASH,
        BR_AIRBORNE,
        BR_JUMP,
        BR_PARKOUR,
        BR_GRAPPLE,
        BR_MOUNT,
        BR_INTERACTION,
        BR_GATHERING,
        BR_MOVE_FAIL,
        BR_FISHING,
        BR_GLIDING,
        BR_PLAY_TARGET_ANIMATION,
        BR_ADS,

        // ex) Next Attraction
        NEXT_TYPE_START = (BR_TYPE_START / AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT + 1) * AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT,

        // Last
        NETWORK,

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
        /// <summary>
        /// NONE
        /// </summary>
        NONE = 0,

        /// <summary>
        /// IDLE
        /// </summary>
        IDLE_TYPE_START = (eStateType.IDLE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        IDLE,
        IDLE_TYPE_END = IDLE_TYPE_START + AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT - 1,

        /// <summary>
        /// WALK
        /// </summary>
        WALK_TYPE_START = (eStateType.WALK - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        WALK,

        /// <summary>
        /// RUN
        /// </summary>
        RUN_TYPE_START = (eStateType.RUN - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        RUN,

        /// <summary>
        /// SPRINT
        /// </summary>
        SPRINT_TYPE_START = (eStateType.SPRINT - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// DASH
        /// </summary>
        DASH_TYPE_START = (eStateType.DASH - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// AIRBORNE
        /// </summary>
        AIRBORNE_TYPE_START = (eStateType.AIRBORNE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// JUMP
        /// </summary>
        JUMP_TYPE_START = (eStateType.JUMP - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// PARKOUR
        /// </summary>
        PARKOUR_TYPE_START = (eStateType.PARKOUR - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// GRAPPLE
        /// </summary>
        GRAPPLE_TYPE_START = (eStateType.GRAPPLE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// MOUNT
        /// </summary>
        MOUNT_TYPE_START = (eStateType.MOUNT - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// INTERACTION
        /// </summary>
        INTERACTION_TYPE_START = (eStateType.INTERACTION - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// GATHERING
        /// </summary>
        GATHERING_TYPE_START = (eStateType.GATHERING - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// MOVE_FAIL
        /// </summary>
        MOVE_FAIL_START_TYPE = (eStateType.MOVE_FAIL - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// FISHING
        /// </summary>
        FISHING_TYPE_START = (eStateType.FISHING - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// GLIDING
        /// </summary>
        GLIDING_TYPE_START = (eStateType.GLIDING - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// CHARACTER_CUSTOMIZING
        /// </summary>
        CHARACTER_CUSTOMIZING_TYPE_START = (eStateType.CHARACTER_CUSTOMIZING - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// CHARACTER_STAGE
        /// </summary>
        CHARACTER_STAGE_TYPE_START = (eStateType.CHARACTER_STAGE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// EMOTE
        /// </summary>
        EMOTE_TYPE_START = (eStateType.EMOTE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// RACE_SELECTION
        /// </summary>
        RACE_SELECTION_TYPE_START = (eStateType.RACE_SELECTION - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// CINEMATIC
        /// </summary>
        CINEMATIC_TYPE_START = (eStateType.CINEMATIC - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// CHARACTER_SELECTION
        /// </summary>
        CHARACTER_SELECTION_TYPE_START = (eStateType.CHARACTER_SELECTION - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        #region Battle Royale
        /// <summary>
        /// BR_IDLE
        /// </summary>
        BR_IDLE_TYPE_START = (eStateType.BR_IDLE - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_WALK
        /// </summary>
        BR_WALK_TYPE_START = (eStateType.BR_WALK - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_RUN
        /// </summary>
        BR_RUN_TYPE_START = (eStateType.BR_RUN - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_SPRINT
        /// </summary>
        BR_SPRINT_TYPE_START = (eStateType.BR_SPRINT - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_DASH
        /// </summary>
        BR_DASH_TYPE_START = (eStateType.BR_DASH - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_JUMP
        /// </summary>
        BR_JUMP_TYPE_START = (eStateType.BR_JUMP - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,

        /// <summary>
        /// BR_ADS
        /// </summary>
        BR_ADS_TYPE_START = (eStateType.BR_ADS - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
        #endregion

        /// <summary>
        /// TYPE_END
        /// </summary>
        TYPE_END = (eStateType.STATE_TYPE_END - 1) * AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT,
    }

    public enum eMoveType
    {
        STAND = 0,
        WALK,
        RUN,
        SPRINT,
        DASH,
        AIRBORNE,
        ADS,
    }

    public enum eTurnDirection
    {
        NONE,
        LEFT,
        RIGHT
    }

    public static class CharacterAnimationEnums
    {
        public static readonly int ANIMATION_TYPE_BIT_DIGITS = Utilities.BitsForValue((uint)eAnimationType.TYPE_END);

        public static eAnimationType SetDontChangeAnimationNetObject(this eAnimationType animationType)
        {
            return EnumUtility.PackFlag(animationType, ANIMATION_TYPE_BIT_DIGITS);
        }

        public static eStateType GetStateType(uint animationType)
        {
            return (animationType / AnimationConsts.ANIMATIONTYPE_WITH_STATETYPE_CONVERSION_UNIT).ToEnum<eStateType>();
        }
    }
}
