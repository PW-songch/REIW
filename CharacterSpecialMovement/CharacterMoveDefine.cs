using REIW.EventLock;
using System;
using System.Collections;
using UnityEngine;

namespace REIW
{
    [Flags]
    public enum CharacterMoveType
    {
        Grapple = 1 << 0,
        WallClimb = 1 << 1,
        Gliding = 1 << 2,
        Parkour = 1 << 3,
        /// <summary>
        /// TODO: 뭔가 스킲 별로 이름을 쓰는 것 보다는 적당히 그럴싸한 일반적인 이름이 필요함
        /// </summary>
        NerualHacking = 1 << 4,
        GravityGrenade = 1 << 5,
        Max = 6,
    }

    public enum CharacterMovePlayMode
    {
        Normal = CharacterMoveType.Grapple | CharacterMoveType.WallClimb | CharacterMoveType.Parkour | CharacterMoveType.NerualHacking | CharacterMoveType.GravityGrenade,
        Gliding = CharacterMoveType.Gliding,
        Parkour = CharacterMoveType.Parkour | CharacterMoveType.Grapple,
    }

    public interface ICharacterMoveComponent
    {
        CharacterMoveType MoveType { get; }

        void Initialize(ICharacterMoveController controller);
        void EnterComponent();
        void ExitComponent();
        void FixedUpdateComponent();
        void LateUpdateComponent();
        void UpdateOriginalInput(PlayerCharacterInputs inputs);
        void UpdateInput(PlayerCharacterInputs inputs);
        bool UpdateVelocity(ref Vector3 velocity, float deltaTime);
        bool UpdateRotation(ref Quaternion rotation, float deltaTime);
        void DestroyComponent();
        void EnterFromPreviousComponentType(CharacterMovePlayMode prevmode);
        bool IsColliderValidForCollisions(Collider coll);
    }

    public interface ICharacterMoveComponentGizmo
    {
        void OnDrawGizmos();
    }

    public interface ICharacterMoveController
    {
        EventBus EventBus { get; }

        Transform CharacterTransform { get; }
        Vector3 Up => CharacterTransform.up;
        Vector3 Forward => CharacterTransform.forward;
        Vector3 Right => CharacterTransform.right;

        float Height { get; }
        float Radius { get; }

        // 현재 딛고 있는 오브젝트
        Collider GroundCollider { get; }
        // (Ground 상관 없이)오브젝트 위에 서있는지 여부
        bool IsStableOnCollider { get; }

        Vector3 Gravity { get; }
        float GravityMagnitude { get; }

        CharacterRootMotionMode ModeRootMotionHorizontalPos { get; }
        CharacterRootMotionMode ModeRootMotionVerticalPos { get; }
        CharacterRootMotionMode ModeRootMotionRotation { get; }

        void SetGravity(Vector3 gravity);
        bool StartJump(bool directly);

        PlayerCharacterInputs CurrentInputs { get; }
        EnvironmentScannerComponent EnvironmentScanner { get; }
    }

    public abstract class CharacterMoveComponentBase<T> : ICharacterMoveComponent, ICheckEventLockState
        where T : ScriptableObject
    {
        private T _data = null;
        protected T MovementData
        {
            get
            {
                if (_data == null)
                    _data = AssetManager.Singleton.GetCharacterMovementDataSO<T>(true);
                return _data;
            }
        }

        /// <summary>
        /// 액션 실행 전, 이동 컴포넌트 레벨에서의 조건을 검증합니다.
        /// (예: 지상 여부, 현재 중력 상태, 이벤트 락 여부 등)
        /// </summary>
        protected virtual bool CanExecuteAction(EnumCategory actionType) => true;

        /// <summary>
        /// 입력 기반 액션 요청을 처리합니다.
        /// \- inputCondition: 키 입력 등  
        /// \- 내부에서 CanExecuteAction + LocalCharacter.TryRequestAction 수행  
        /// 성공 시, 같은 프레임에 동일 액션은 다시 실행되지 않습니다.
        /// </summary>
        protected bool TryRequestStaminaActionFromInput(EnumCategory actionType, bool inputCondition)
        {
            if (!CanExecuteAction(actionType))
                return false;

            return LocalCharacter.CanExecuteStaminaAction(actionType, inputCondition);
        }

        private Coroutine currentCoroutine;

        protected ICharacterMoveController Controller { get; private set; }
        protected LocalCharacter LocalCharacter => Controller as LocalCharacter;
        protected Transform CharacterTransform => Controller.CharacterTransform;
        protected EnvironmentScannerComponent EnvironmentScanner => Controller.EnvironmentScanner;

        public abstract CharacterMoveType MoveType { get; }

        public virtual void Initialize(ICharacterMoveController controller)
        {
            Controller = controller;
            LocalCharacter?.CharacterEventLockController.AddEventLockState(this);
        }

        public virtual void DestroyComponent()
        {
            if (_data != null)
            {
                if (AssetManager.IsCreated)
                    AssetManager.Singleton.ReleaseAsset(_data);
                _data = null;
            }

            LocalCharacter?.CharacterEventLockController.RemoveEventLockState(this);
        }

        public virtual void EnterComponent()
        {

        }

        public virtual void ExitComponent()
        {

        }

        public virtual void FixedUpdateComponent()
        {

        }

        public virtual void LateUpdateComponent()
        {

        }

        public virtual void UpdateOriginalInput(PlayerCharacterInputs inputs)
        {

        }

        public virtual void UpdateInput(PlayerCharacterInputs inputs)
        {

        }

        public virtual bool UpdateVelocity(ref Vector3 velocity, float deltaTime)
        {
            return false;
        }

        public virtual bool UpdateRotation(ref Quaternion rotation, float deltaTime)
        {
            return false;
        }

        public virtual void EnterFromPreviousComponentType(CharacterMovePlayMode prevmode)
        {

        }

        protected Coroutine StartCoroutine(IEnumerator coroutine)
        {
            currentCoroutine = LocalCharacter?.StartCoroutine(coroutine);
            return currentCoroutine;
        }

        protected void StopCoroutine(ref Coroutine coroutine)
        {
            if (coroutine != null)
                LocalCharacter?.StopCoroutine(coroutine);
            coroutine = null;
        }

        protected void StopCurrentCoroutine()
        {
            StopCoroutine(ref currentCoroutine);
        }

        public virtual bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }

        public virtual eEventLockType CurrentEventLockType => eEventLockType.None;
        public virtual eEventLockType ReleaseEventLockType => eEventLockType.None;
    }

    public interface IPlayModeState
    {
        CharacterMovePlayMode MovePlayMode { get; }
    }

    public interface IMoveComponentStateApplier
    {
        bool MoveComponentStateApply(PlayerCharacterInputs inputs);
    }
}