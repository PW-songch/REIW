using System;
using Animancer.FSM;
using UnityEngine;

namespace REIW.Animations.Npc
{
    public class NpcAnimation : AnimationBase<eAnimationType, eStateType, NpcAnimationState, NpcAnimationStateMachine, NpcAnimation>
    {
        public override bool IsLocal => true;

        public override eStateType CurrentBaseStateType => (eStateType)((int)_currentStateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType PrevBaseStateType => (eStateType)((int)_prevStateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType CurrentBaseSubstateType => (eStateType)((int)_currentSubstateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        public override eStateType PrevBaseSubstateType => (eStateType)((int)_prevSubstateType % AnimationConsts.ANIMATION_STATETYPE_INTERVAL_UNIT);
        
        protected override int AnimationTypeBitDigits => NpcAnimationEnums.ANIMATION_TYPE_BIT_DIGITS;

        protected override void Awake()
        {
            base.Awake();
            base.Init();
        }

        protected override bool InitializeRootMotionSettings()
        {
            return true;
            
            // if (!base.InitializeRootMotionSettings())
            //     return false;
            //
            // string soName = string.Format(AnimationClipRootMotionSettingsSO.GetRootMotionSettingsSOFileNameFormat(eObjectType.Npc), name.ToLower());
            // _rootMotionSettings = AssetManager.Singleton.GetAnimationClipRootMotionSettingsSO($"{nameof(eObjectType.Npc).ToLower()}/{soName}");
            // return true;
        }

        public void SetPlayTargetAnimation(eAnimationType InAnimationType)
        {
            InAnimationType = EnumUtility.GetUnpackValue(InAnimationType, AnimationTypeBitDigits);
            StateBehaviour state = GetAnimationState(InAnimationType);
            if (state != null && state is PlayTargetAnimationState playTargetAnimationState)
                playTargetAnimationState.PlayAnimationType = InAnimationType;
        }
    }
}