using UnityEngine.Pool;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimation
    {

        #region AnimationEventListeners Settings

        /// <summary>
        /// EventListener 연결 함수명 규칙
        /// 1. 기본 이름 : "ConnectingEvents_"
        /// 2. namespace 이름 : EventListener의 namespace에서 AnimationBase의 namespace(REIW.Animations)를 제외한 각 이름들을 "_"로 연결
        /// ex) REIW.Animations.Character.BR.CommonAnimationEventListener의 경우 REIW.Animations.Character.BR에서 REIW.Animations를 제외한 "BR_Character_"
        /// 3. EventListener 이름
        /// 함수명 : 1 + 2 + 3
        /// </summary>
        private void ConnectingEvents_Character_BR_CommonAnimationEventListener()
        {
            var brCharacterEL = GetAnimationEventListeners<BR.CommonAnimationEventListener>();
            {
                brCharacterEL.ChangeMovementStateEvent += (state) =>
                {
                    CurrentState.GetStateChangeModule<BR.StateChangeModule>()?.SetMovementState(state);

                    var modules = ListPool<BR.StateChangeModule>.Get();
                    if (StateMachine.GetNextStateChangeModules(modules))
                    {
                        foreach (var module in modules)
                            module.SetMovementState(state);
                    }
                    ListPool<BR.StateChangeModule>.Release(modules);
                };
            }
        }

        #endregion
    }
}
