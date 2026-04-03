using UnityEngine;

namespace REIW
{
    public class VaultActionMoveSolver : ParkourActionMoveSolver
    {
        private float _speed;
        private int _samples;

        private float[] _progressLut;
        private float[] _valueLut;

        public static VaultActionMoveSolver Create(VaultActionMoveSolver solver, Vector3 start, Vector3 end,
            Vector3 directionWeight, float minDistance, float maxDistance, float minMoveTime, float maxMoveTime,
            AnimationCurve speedCurve, int samples = 128)
        {
            solver ??= new VaultActionMoveSolver();
            solver.Initialize(start, end, directionWeight,
                minDistance, maxDistance, minMoveTime, maxMoveTime, speedCurve, samples);
            return solver;
        }

        public void Initialize(Vector3 start, Vector3 end, Vector3 directionWeight,
            float minDistance, float maxDistance, float minMoveTime, float maxMoveTime,
            AnimationCurve speedCurve, int samples = 128)
        {
            Initialize();

            _start = start;
            _end = end;
            _samples = Mathf.Max(16, samples);

            var delta = end - start;
            delta.Scale(directionWeight);
            _distance = delta.magnitude;

            if (_distance <= Epsilon)
            {
                IsValid = true;
                Error = string.Empty;
                return;
            }

            if (maxDistance <= Epsilon)
            {
                IsValid = false;
                Error = "maxDistance must be greater than 0.";
                return;
            }

            if (maxMoveTime <= Epsilon)
            {
                IsValid = false;
                Error = "maxMoveTime must be greater than 0.";
                return;
            }

            _direction = delta / _distance;

            speedCurve ??= AnimationCurve.Linear(0f, 1f, 1f, 1f);

            BuildHorizontalLuts(speedCurve, _samples, out _progressLut, out _valueLut, out var totalArea);

            if (totalArea <= Epsilon)
            {
                Error = "speedCurve area is zero.";
                return;
            }

            var ratio = Mathf.InverseLerp(minDistance, maxDistance, _distance);
            _duration = Mathf.Lerp(minMoveTime, maxMoveTime, ratio);
            _speed = _distance / (_duration * totalArea);

            IsValid = true;
        }

        public override Vector3 EvaluatePosition(float elapsedTime)
        {
            if (!IsValid || !IsPlay)
                return _start;

            if (_distance <= Epsilon)
            {
                IsPlay = false;
                return _end;
            }

            var n = GetNormalizedTime(elapsedTime);
            var progress01 = SampleLut(_progressLut, n, _samples);

            return _start + (_direction * (_distance * progress01));
        }

        public override Vector3 EvaluateVelocity(float elapsedTime)
        {
            if (!IsValid || !IsPlay)
                return Vector3.zero;

            if (_distance <= Epsilon)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            var n = GetNormalizedTime(elapsedTime);
            if (n >= 1f)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            var speedMultiplier = Mathf.Max(0f, SampleLut(_valueLut, n, _samples));
            var speed = _speed * speedMultiplier;

            return _direction * speed;
        }
    }
}
