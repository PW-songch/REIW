namespace REIW.Animations.Character
{
    public class NetworkAnimationState : CharacterAnimationState
    {
        public override eStateType StateType => eStateType.NETWORK;

        private bool _isMoving;

        protected override void OnEnable()
        {
            base.OnEnable();

            SetAnimatorLODActiveChangedCallback();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_animation.ClientCharacter.CharacterLOD)
                _animation.ClientCharacter.CharacterLOD.OnAnimatorLODActiveChanged -= OnAnimatorLODActiveChanged;
        }

        public override void Initialize()
        {
            base.Initialize();

            SetAnimatorLODActiveChangedCallback();
        }

        public override void DisableStateNetwork()
        {
            base.OnDisable();
        }

        public override bool LateUpdateState()
        {
            var isMoving = Animation.IsMoving;
            if (!isMoving)
                isMoving = (eAnimationType)Animation.CurrentAnimation > eAnimationType.IDLE_TYPE_END;

            if (isMoving != _isMoving)
            {
                Movement.EnableIK(_animation.ClientCharacter.CharacterLOD.CurrentAnimatorLOD == 0 &&
                                  (isMoving || Movement.IsApplyingGrounderIK()));
                _isMoving = isMoving;
            }

            return base.LateUpdateState();
        }

        protected override void UpdateAnimationParameters()
        {
            Movement.UpdateVerticalSpeedParameter(true);
        }

        private void SetAnimatorLODActiveChangedCallback()
        {
            if (_animation.ClientCharacter.CharacterLOD)
            {
                _animation.ClientCharacter.CharacterLOD.OnAnimatorLODActiveChanged -= OnAnimatorLODActiveChanged;

                if (!_animation.IsLocal)
                    _animation.ClientCharacter.CharacterLOD.OnAnimatorLODActiveChanged += OnAnimatorLODActiveChanged;
            }
        }

        private void OnAnimatorLODActiveChanged(bool active)
        {
            Movement.EnableIK(!active && (Animation.IsMoving || Movement.IsApplyingGrounderIK()));
        }
    }
}
