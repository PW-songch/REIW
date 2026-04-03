using System;
using Animancer;
using UnityEngine;

namespace REIW.Animations.Character
{
    [Serializable]
    public class CharacterAnimationParameters
    {
        [SerializeField] private StringAsset _forwardSpeedParameterName;
        private Parameter<float> _forwardSpeedParameter;
        [SerializeField] private float _validForwardSpeed = 0.0001f;

        [SerializeField] private StringAsset _verticalSpeedParameterName;
        private Parameter<float> _verticalSpeedParameter;

        public float ForwardSpeed
        {
            get => _forwardSpeedParameter?.Value ?? 0;
            set
            {
                if (_forwardSpeedParameter != null)
                    _forwardSpeedParameter.Value = value;
            }
        }

        public float VerticalSpeed
        {
            get => _verticalSpeedParameter?.Value ?? 0;
            set
            {
                if (_verticalSpeedParameter != null)
                    _verticalSpeedParameter.Value = value;
            }
        }

        public bool IsValidForwardSpeed => ForwardSpeed > _validForwardSpeed;

        public LocomotionAnimationState.eMovementType ReservedMoveType { get; set; }

        public void CreateParameters(AnimancerComponent InAnimancer)
        {
            _forwardSpeedParameter = InAnimancer.Parameters.GetOrCreate<float>(_forwardSpeedParameterName);
            _verticalSpeedParameter = InAnimancer.Parameters.GetOrCreate<float>(_verticalSpeedParameterName);
        }
    }
}