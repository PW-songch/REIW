using System.Collections;
using REIW.Animations.Character;
using REIW.EventLock;
using UnityEngine;
using UnityEngine.Pool;

namespace REIW
{
    public class CharacterMoveParkour : CharacterMoveComponentBase<CharacterMoveParkourData>
    {
        public override CharacterMoveType MoveType => CharacterMoveType.Parkour;

        private bool _parkourInputPressed;
        private bool _updateAction;
        private float _actionElapsed;
        private eEventLockType _currentEventLockType;
        private eEventLockType _currentReleaseEventLockType;
        private ParkourAnimationState _playingAniState;
        private Coroutine _parkourActionCoroutine;

        private ParkourActionData _currentActionData;
        private ParkourVaultActionData _vaultActionData;
        private ParkourJumpActionData _jumpActionData;
        private VaultActionMoveSolver _vaultActionMoveSolver;
        private JumpActionMoveSolver _jumpActionMoveSolver;

        public bool InAction => _currentActionData != null;
        private bool IsCurrentVaultAction => _vaultActionData != null;
        private bool IsVaultAction(ParkourActionData actionData) => actionData is ParkourVaultActionData;
        private bool IsCurrentJumpAction => _jumpActionData != null;
        private bool IsJumpAction(ParkourActionData actionData) => actionData is ParkourJumpActionData;
        private bool IsJumpPressed
        {
            get => InputBuffer.hasBuffer && InputBuffer.value == eCharacterActionInputType.JUMP;
            set => InputBuffer.Set(value ? eCharacterActionInputType.JUMP : eCharacterActionInputType.NONE);
        }
        private CharacterActionInputBuffer InputBuffer => MovementData.InputBuffer;

        private ParkourActionMoveSolver ActiveActionMoveSolver =>
            _vaultActionMoveSolver is { IsValid: true } ? _vaultActionMoveSolver :
                (_jumpActionMoveSolver is { IsValid: true } ? _jumpActionMoveSolver : null);
        private ParkourActionMoveSolver PlayingActionMoveSolver =>
            _vaultActionMoveSolver is { IsValid: true, IsPlay: true } ? _vaultActionMoveSolver :
                (_jumpActionMoveSolver is { IsValid: true, IsPlay: true } ? _jumpActionMoveSolver : null);

        public override eEventLockType CurrentEventLockType => (InAction ? (eEventLockType.CharacterMoveAllAction |
            (_currentActionData.LockMoveInput ? eEventLockType.CharacterInputLock : eEventLockType.None)) :
            base.CurrentEventLockType) | _currentEventLockType;
        public override eEventLockType ReleaseEventLockType => InAction ? _currentReleaseEventLockType : base.ReleaseEventLockType;

        public override void Initialize(ICharacterMoveController controller)
        {
            base.Initialize(controller);
            _playingAniState = LocalCharacter.CharacterAnimation.StateMachine.Parkour;
            EnvironmentScanner.SetParkourData(MovementData);
        }

        public override void EnterComponent()
        {
            base.EnterComponent();

            if (InAction)
            {
                if (Controller is LocalCharacter localCharacter)
                    localCharacter.LockMoveInput = _currentActionData.AtStartLockMoveInput;
            }
        }

        public override void ExitComponent()
        {
            if (LocalCharacter.CharacterMoveComponentsHandler.CurrentMovePlayMode == CharacterMovePlayMode.Parkour)
                return;

            base.ExitComponent();
            CancelParkourAction();
        }

        public override void FixedUpdateComponent()
        {
            if (!InAction && _playingAniState.CanEnterFromCurrentState && IsPossibleParkourAction())
            {
                FixedUpdateParkourVaultActionProcess();
                //UpdateParkourJumpActionProcess();
            }

            _parkourInputPressed = false;
        }

        public override void UpdateOriginalInput(PlayerCharacterInputs inputs)
        {
            if (inputs.Jump)
                IsJumpPressed = true;
        }

        public override void UpdateInput(PlayerCharacterInputs inputs)
        {
            if (inputs.Parkour)
                _parkourInputPressed = true;
            if (inputs.Jump)
                IsJumpPressed = true;

            UpdateParkourJumpAction(inputs);
        }

        public override bool UpdateVelocity(ref Vector3 velocity, float deltaTime)
        {
            var fix = false;

            if (InAction)
            {
                if (IsCurrentVaultAction)
                    fix = UpdateVaultActionVelocity(ref velocity, deltaTime);
                else if (IsCurrentJumpAction)
                    fix = UpdateJumpActionVelocity(ref velocity, deltaTime);

                fix = fix && !LocalCharacter.IsApplyingRootMotion;
            }

            return fix;
        }

        public override bool UpdateRotation(ref Quaternion rotation, float deltaTime)
        {
            return false;
        }

        private void UpdateParkourJumpAction(PlayerCharacterInputs inputs)
        {
            if (!InAction && _playingAniState.CanEnterFromCurrentState && IsPossibleParkourAction())
            {
                UpdateParkourJumpActionProcess();

                if (IsJumpPressed)
                {
                    SetPlayerCharacterInputsByOperation(inputs, !InAction);
                    IsJumpPressed = false;
                }
            }
        }

        private bool UpdateVaultActionVelocity(ref Vector3 velocity, float deltaTime)
        {
            var fix = false;

            if (_updateAction && _vaultActionMoveSolver?.IsValid == true)
            {
                var v = _vaultActionMoveSolver.EvaluateFrameVelocityFromCurrent(
                    Controller.CharacterTransform.position, _actionElapsed, deltaTime);
                fix = _vaultActionMoveSolver.IsPlay;
                SetVelocityWithRootMotion(fix, v, ref velocity);

                _actionElapsed += deltaTime;
            }

            return fix;
        }

        private bool UpdateJumpActionVelocity(ref Vector3 velocity, float deltaTime)
        {
            var fix = false;

            if (_updateAction && _jumpActionMoveSolver?.IsValid == true)
            {
                var v = _jumpActionMoveSolver.EvaluateFrameVelocityFromCurrent(
                    Controller.CharacterTransform.position, _actionElapsed, deltaTime);
                fix = _jumpActionMoveSolver.IsPlay;
                SetVelocityWithRootMotion(fix, v, ref velocity);

                _actionElapsed += deltaTime;
            }

            return fix;
        }

        private void SetVelocityWithRootMotion(bool fix, Vector3 applyVelocity, ref Vector3 velocity)
        {
            if (fix)
            {
                velocity = applyVelocity;
            }
            else if (InAction)
            {
                if (_currentActionData.ApplySolverHorizontalVelocity)
                {
                    velocity.x = applyVelocity.x;
                    velocity.z = applyVelocity.z;
                }

                if (_currentActionData.ApplySolverVerticalVelocity)
                {
                    velocity.y = applyVelocity.y;
                }
            }
        }

        private void FixedUpdateParkourVaultActionProcess()
        {
            if (InAction)
                return;

            if ((_parkourInputPressed || LocalCharacter.IsMoveInput) &&
                LocalCharacter.CharacterAnimation.ForwardSpeedParameter >= MovementData.DetectionMoveSpeedMin)
            {
                var hitData = EnvironmentScanner.ObstacleCheck();

#if UNITY_EDITOR
                // if (hitData.forwardHitFound)
                // {
                //     LogConsole.Normal(eLogCategory.Parkour, $"Found Obstacle - name: " +
                //         $"{hitData.forwardHit.collider.name}, depth: {hitData.hitDepth}, width: {hitData.hitWidth}, " +
                //         $"height: {hitData.hitHeight}, ledgeHitFound: {hitData.ledgeHitFound}".Color(Color.green));
                // }
#endif

                if (_parkourInputPressed)
                    ExecuteParkourVaultAction(hitData, true);
                if (!InAction && LocalCharacter.IsMoveInput)
                    ExecuteParkourVaultAction(hitData, false);
            }
        }

        private void ExecuteParkourVaultAction(ObstacleHitData hitData, bool isManualOperation)
        {
            if (InAction)
                return;

            if (hitData.forwardHitFound /*&& hitData.hasSpace*/
                && Vector3.Angle(Controller.Forward, -hitData.forwardHit.normal) <= MovementData.DetectionHorizontalAngleMax)
            {
                using (ListPool<ParkourVaultActionData>.Get(out var actionList))
                {
                    foreach (var action in MovementData.ParkourVaultActions)
                    {
                        if (action.CheckIfPossible(hitData, Controller.CharacterTransform,
                                LocalCharacter.CharacterAnimation.ForwardSpeedParameter,
                                MovementData.DetectionHorizontalAngleMax, isManualOperation))
                        {
                            actionList.Add(action);
                        }
                    }

                    if (actionList.Count > 0)
                    {
                        if (actionList.Count > 1)
                            actionList.Sort(actionList[0].Compare);
                        RequestParkourAction(actionList[0]);
                    }
                }
            }
        }

        private void UpdateParkourJumpActionProcess()
        {
            if (InAction)
                return;

            if (LocalCharacter.CharacterAnimation.ForwardSpeedParameter >= MovementData.DetectionJumpMoveSpeedMin)
            {
                for (var i = 0; i < MovementData.ParkourJumpActions.Length; ++i)
                {
                    var action = MovementData.ParkourJumpActions[i];
                    var minJumpDistance = float.IsNaN(action.MinLandableGroundDistance) ?
                        MovementData.MinJumpDistance : action.MinLandableGroundDistance;
                    var maxJumpDistance = float.IsNaN(action.MaxLandableGroundDistance) ?
                        MovementData.MaxJumpDistance : action.MaxLandableGroundDistance;
                    var jumpHeight = (MovementData.JumpHeight > 0f ? MovementData.JumpHeight : LocalCharacter.JumpHeight) +
                                     (action.MaxLandableGroundHeight > 0f ? action.MaxLandableGroundHeight : 0f);

                    var jumpData = EnvironmentScanner.FindPointToJump(
                        (LocalCharacter.IsMoveInput ? LocalCharacter.CharacterMoveDir : LocalCharacter.Forward).normalized,
                        minJumpDistance, maxJumpDistance, jumpHeight, action.CheckClimb, action.FindBaseLandableGround);

                    if (!jumpData.IsValid)
                        continue;

                    jumpData.SetGround(Controller.GroundCollider.transform);
                    ExecuteParkourJumpAction(action, jumpData, IsJumpPressed);

                    if (InAction)
                        break;
                }
            }
        }

        private void ExecuteParkourJumpAction(ParkourJumpActionData actionData, JumpData jumpData, bool isManualOperation)
        {
            if (InAction || !jumpData.IsValid)
                return;

#if UNITY_EDITOR
            // var log = "landableTargets: ";
            // foreach (var target in jumpData.landableTargets)
            // {
            //     log += target.hit.collider.name + " / ";
            // }
            //
            // LogConsole.Normal(eLogCategory.Parkour, log);
#endif

            if (actionData.CheckIfPossible(LocalCharacter.Motor, jumpData,
                    LocalCharacter.CharacterAnimation.ForwardSpeedParameter, isManualOperation))
            {
                RequestParkourAction(actionData);
            }
        }

        private void RequestParkourAction(ParkourActionData actionData)
        {
            if (actionData == null)
                return;

#if UNITY_EDITOR
            LogConsole.Normal(eLogCategory.Parkour, $"RequestParkourAction - {actionData.AnimationType}".Color(Color.cyan));
#endif

            _currentActionData = actionData;
            _vaultActionData = actionData as ParkourVaultActionData;
            _jumpActionData = actionData as ParkourJumpActionData;

            Controller.EventBus.Post<IMoveParkourEventListener>(_ => _.OnParkourRequested(_currentActionData, StartParkourAction));
        }

        private void StartParkourAction(bool isSuccess, float animationLength)
        {
            if (_currentActionData == null)
                return;

            if (isSuccess)
            {
                _currentActionData.TransitionLength = animationLength;
                StopParkourCoroutine();
                _parkourActionCoroutine = StartCoroutine(CorParkourAction(_currentActionData));
            }
            else
            {
#if UNITY_EDITOR
                LogConsole.Error(eLogCategory.Parkour, "Failed StartParkourAction");
#endif

                if (_currentActionData is ParkourJumpActionData && _currentActionData.ManualOperation)
                    Controller.EventBus.Post<ICharacterBaseEventListener>(_ => _.OnJumpRequested());

                FinishedParkourAction();
            }
        }

        private void PrepareParkourAction(ParkourActionData actionData)
        {
            if (actionData == null)
                return;

            _currentEventLockType = eEventLockType.None;
            _currentReleaseEventLockType = eEventLockType.None;

            if (actionData is ParkourVaultActionData vaultActionData)
            {
                if (vaultActionData.MoveToTargetDirectionWeight != Vector3.zero)
                {
                    _vaultActionMoveSolver = VaultActionMoveSolver.Create(_vaultActionMoveSolver,
                        LocalCharacter.Transform.position, vaultActionData.HitData.forwardHit.point,
                        vaultActionData.MoveToTargetDirectionWeight,
                        vaultActionData.MinObstacleDistance, vaultActionData.MaxObstacleDistance,
                        vaultActionData.MinMoveToTargetTime, vaultActionData.MaxMoveToTargetTime,
                        vaultActionData.MoveHorizontalSpeedCurve);
                }
            }
            else if (actionData is ParkourJumpActionData jumpActionData)
            {
                _currentEventLockType |= eEventLockType.CharacterJump;
                _currentReleaseEventLockType |= eEventLockType.CharacterGraple;
                _currentReleaseEventLockType |= eEventLockType.CharacterGlide;

                _jumpActionMoveSolver = JumpActionMoveSolver.Create(_jumpActionMoveSolver,
                    jumpActionData.JumpData.startPosition, jumpActionData.JumpData.TargetPosition,
                    jumpActionData.JumpData.JumpHeight, jumpActionData.JumpTime,
                    jumpActionData.JumpHorizontalSpeedCurve, jumpActionData.JumpHeightCurve);

#if UNITY_EDITOR
                LogConsole.Normal(eLogCategory.Parkour,
                    ($"Jump Action [{jumpActionData.name}] - target: {jumpActionData.JumpData.LandTarget.hit.collider.name}, " +
                     $"targetPosition: {jumpActionData.JumpData.TargetPosition}, " +
                     $"distance: {jumpActionData.JumpData.LandTarget.DistanceFromStart}, " +
                     $"height: {jumpActionData.JumpData.LandTarget.HeightFromStart}, " +
                     $"moveSpeed: {LocalCharacter.CurMoveSpeed}, " +
                     $"duration: {_jumpActionMoveSolver.Duration}, " +
                     $"ani speed: {jumpActionData.TransitionLength / _jumpActionMoveSolver.Duration}").Color(Color.green));
#endif
            }
        }

        private void FinishedParkourAction()
        {
            if (!InAction)
                return;

            if (Controller is LocalCharacter localCharacter)
            {
                localCharacter.LockMoveInput = false;
                localCharacter.ColliderTransformLinker?.RestoreParent();
            }

            _currentActionData?.FinishParkourAction();
            ResetData();
        }

        private void StopParkourCoroutine()
        {
            StopCurrentCoroutine();
            StopCoroutine(ref _parkourActionCoroutine);
        }

        public void CancelParkourAction()
        {
#if UNITY_EDITOR
            if (InAction)
                LogConsole.Normal(eLogCategory.Parkour, "FinishedParkourAction".Color(Color.magenta));
#endif
            StopParkourCoroutine();
            FinishedParkourAction();
        }

        private IEnumerator CorParkourAction(ParkourActionData actionData)
        {
            if (!InAction || actionData != _currentActionData)
                yield break;

            actionData.StartParkourAction();
            PrepareParkourAction(actionData);

            Controller.EventBus.Post<IMoveParkourEventListener>(_ => _.OnParkourStarted(actionData,
                () =>
                {
                    FinishedParkourAction();
#if UNITY_EDITOR
                    LogConsole.Normal(eLogCategory.Parkour, "FinishedParkourAction".Color(Color.magenta));
#endif
                }));

            if (actionData.StartActionDelay > 0)
                yield return new WaitForSeconds(actionData.StartActionDelay);

            _updateAction = true;
            ActiveActionMoveSolver?.Play();

            yield return IsJumpAction(actionData) ? StartCoroutine(CorParkourJumpAction(actionData as ParkourJumpActionData)) : new WaitWhile(() => InAction);

            if (actionData.PostActionDelay > 0)
                yield return new WaitForSeconds(actionData.PostActionDelay);

            Controller.EventBus.Post<IMoveParkourEventListener>(_ => _.OnParkourFinished());

#if UNITY_EDITOR
            if (actionData.PostActionDelay > 0)
                LogConsole.Normal(eLogCategory.Parkour, "Finish CorParkourAction");
#endif
        }

        private IEnumerator CorParkourJumpAction(ParkourJumpActionData actionData)
        {
            if (!InAction || actionData != _jumpActionData)
                yield break;

            LocalCharacter.ForceUnground();

            yield return new WaitUntil(() => InAction && !Controller.IsStableOnCollider);
            yield return new WaitWhile(() => InAction && !Controller.IsStableOnCollider);

            _jumpActionMoveSolver.Finished();
        }

        private bool IsPossibleParkourAction()
        {
            return Controller.IsStableOnCollider && LocalCharacter.IsMoveInput;
        }

        public override bool IsColliderValidForCollisions(Collider coll)
        {
            return !InAction || !IsCurrentVaultAction || !_vaultActionData.InactivationCollisionWithHitTarget ||
                   !_vaultActionData.HitData.forwardHitFound || _vaultActionData.HitData.forwardHit.collider != coll;
        }

        private void ResetData()
        {
            ActiveActionMoveSolver?.Finished();
            _currentActionData = null;
            _vaultActionData = null;
            _jumpActionData = null;
            _updateAction = false;
            _actionElapsed = 0;
            _parkourInputPressed = false;
            _currentEventLockType = eEventLockType.None;
            _currentReleaseEventLockType = eEventLockType.None;
        }

        private void SetPlayerCharacterInputsByOperation(PlayerCharacterInputs inputs, bool operation)
        {
            if (inputs == null)
                return;

            inputs.Jump = operation;
        }
    }
}
