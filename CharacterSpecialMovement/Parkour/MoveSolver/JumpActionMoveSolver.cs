using UnityEngine;

namespace REIW
{
    public class JumpActionMoveSolver : ParkourActionMoveSolver
    {
        private float _verticalAmplitude;
        private float _requiredHorizontalSpeed;
        private int _samples;

        private float[] _horizontalProgressLut;
        private float[] _horizontalValueLut;
        private float[] _verticalShapeLut;
        private float[] _verticalDerivativeLut;

        public static JumpActionMoveSolver Create(JumpActionMoveSolver solver, Vector3 start, Vector3 end,
            float jumpHeight, float jumpTime, AnimationCurve horizontalCurve, AnimationCurve verticalCurve,
            int samples = 128, float jumpDownCompensation = 0.5f, float minJumpHeight = 0.2f)
        {
            solver ??= new JumpActionMoveSolver();
            solver.Initialize(start, end, jumpHeight, jumpTime,
                horizontalCurve, verticalCurve, samples, jumpDownCompensation, minJumpHeight);
            return solver;
        }

        public void Initialize(Vector3 start, Vector3 end, float jumpHeight, float jumpTime,
            AnimationCurve horizontalCurve, AnimationCurve verticalCurve, int samples = 128,
            float jumpDownCompensation = 0.5f, float minJumpHeight = 0.2f)
        {
            Initialize();

            _start = start;
            _end = end;
            _duration = Mathf.Max(0f, jumpTime);
            _samples = Mathf.Max(16, samples);

            horizontalCurve ??= AnimationCurve.Linear(0f, 1f, 1f, 1f);
            verticalCurve ??= new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 1f),
                new Keyframe(1f, 0f)
            );

            if (_duration <= Epsilon)
            {
                Error = "jumpTime must be greater than 0.";
                return;
            }

            var flatDelta = end - start;
            flatDelta.y = 0f;

            _distance = flatDelta.magnitude;
            _direction = _distance > Epsilon ? flatDelta / _distance : Vector3.zero;

            BuildHorizontalLuts(horizontalCurve, _samples, out _horizontalProgressLut, out _horizontalValueLut, out var horizontalTotalArea);

            if (_distance > Epsilon && horizontalTotalArea <= Epsilon)
            {
                Error = "horizontalCurve area is zero.";
                return;
            }

            BuildVerticalShapeLut(verticalCurve, _samples, out _verticalShapeLut, out _verticalDerivativeLut, out var peakTime);

            var deltaY = _end.y - _start.y;
            var baseJumpHeight = Mathf.Max(0f, jumpHeight);
            var actualJumpHeight = baseJumpHeight;

            if (deltaY < 0f)
                actualJumpHeight = Mathf.Max(minJumpHeight, baseJumpHeight + (deltaY * jumpDownCompensation));
            else if (deltaY > 0f)
                actualJumpHeight = deltaY + Mathf.Max(minJumpHeight, (baseJumpHeight - deltaY) * jumpDownCompensation);

            var apexY = _start.y + actualJumpHeight;
            var baseYAtPeak = Mathf.Lerp(_start.y, _end.y, peakTime);
            _verticalAmplitude = Mathf.Max(0f, apexY - baseYAtPeak);

            _requiredHorizontalSpeed = _distance <= Epsilon ?
                0f : _distance / (_duration * horizontalTotalArea);

            IsValid = true;
        }

        public override Vector3 EvaluatePosition(float elapsedTime)
        {
            if (!IsValid || !IsPlay)
                return _start;

            if (elapsedTime >= _duration)
            {
                IsPlay = false;
                return _end;
            }

            var n = GetNormalizedTime(elapsedTime);
            var horizontalProgress01 = SampleLut(_horizontalProgressLut, n, _samples);

            var pos = _start + _direction * (_distance * horizontalProgress01);
            pos.y = Mathf.Lerp(_start.y, _end.y, n) + _verticalAmplitude * SampleLut(_verticalShapeLut, n, _samples);

            return pos;
        }

        public override Vector3 EvaluateVelocity(float elapsedTime)
        {
            if (!IsValid || !IsPlay)
                return Vector3.zero;

            if (elapsedTime >= _duration)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            var n = GetNormalizedTime(elapsedTime);
            var horizontalMultiplier = Mathf.Max(0f, SampleLut(_horizontalValueLut, n, _samples));
            var horizontalSpeed = _requiredHorizontalSpeed * horizontalMultiplier;
            var verticalSpeed =
                ((_end.y - _start.y) + (_verticalAmplitude * SampleLut(_verticalDerivativeLut, n, _samples))) / _duration;

            return (_direction * horizontalSpeed) + (Vector3.up * verticalSpeed);
        }
    }
}
