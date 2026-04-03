using UnityEngine;

namespace REIW
{
    public abstract class ParkourActionMoveSolver
    {
        protected const float Epsilon = 0.0001f;

        protected Vector3 _start;
        protected Vector3 _end;
        protected Vector3 _direction;
        protected float _distance;
        protected float _duration;

        public bool IsValid { get; protected set; }
        public bool IsPlay { get; protected set; }
        public string Error { get; protected set; }

        public float Duration => _duration;
        public Vector3 InitialVelocity => EvaluateVelocity(0f);

        protected virtual void Initialize()
        {
            IsValid = false;
            IsPlay = false;
            Error = string.Empty;
        }

        protected float GetNormalizedTime(float elapsedTime)
        {
            if (!IsValid || _duration <= Epsilon)
                return 1f;

            return Mathf.Clamp01(elapsedTime / _duration);
        }

        public virtual Vector3 EvaluatePosition(float elapsedTime) => Vector3.zero;

        public virtual Vector3 EvaluateVelocity(float elapsedTime) => Vector3.zero;

        public virtual Vector3 EvaluateFrameVelocity(float elapsedTime, float deltaTime)
        {
            if (!IsValid || !IsPlay || deltaTime <= 0f)
                return Vector3.zero;

            if (elapsedTime >= _duration)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            var t0 = Mathf.Clamp(elapsedTime, 0f, _duration);
            var t1 = Mathf.Clamp(t0 + deltaTime, 0f, _duration);

            if (t1 <= t0)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            return (EvaluatePosition(t1) - EvaluatePosition(t0)) / (t1 - t0);
        }

        public virtual Vector3 EvaluateFrameVelocityFromCurrent(Vector3 currentPosition, float elapsedTime, float deltaTime)
        {
            if (!IsValid || !IsPlay || deltaTime <= 0f)
                return Vector3.zero;

            if (elapsedTime >= _duration)
            {
                IsPlay = false;
                return Vector3.zero;
            }

            var nextTime = Mathf.Clamp(elapsedTime + deltaTime, 0f, _duration);
            var desiredNext = EvaluatePosition(nextTime);

            return (desiredNext - currentPosition) / deltaTime;
        }

        public void Play()
        {
            IsPlay = true;
        }

        public void Finished()
        {
            IsValid = false;
            IsPlay = false;
        }

        protected void BuildHorizontalLuts(AnimationCurve curve, int samples,
            out float[] progressLut, out float[] valueLut, out float totalArea)
        {
            progressLut = new float[samples + 1];
            valueLut = new float[samples + 1];

            var step = 1f / samples;
            var prev = Mathf.Max(0f, curve.Evaluate(0f));

            progressLut[0] = 0f;
            valueLut[0] = prev;
            totalArea = 0f;

            for (int i = 1; i <= samples; ++i)
            {
                var t = i * step;
                var current = Mathf.Max(0f, curve.Evaluate(t));
                valueLut[i] = current;

                totalArea += (prev + current) * 0.5f * step;
                progressLut[i] = totalArea;

                prev = current;
            }

            if (totalArea > Epsilon)
            {
                for (int i = 0; i <= samples; ++i)
                {
                    progressLut[i] /= totalArea;
                }
            }
            else
            {
                for (int i = 0; i <= samples; ++i)
                {
                    progressLut[i] = 1f;
                }
            }
        }

        protected void BuildVerticalShapeLut(AnimationCurve curve, int samples,
            out float[] shapeLut, out float[] derivativeLut, out float peakTime01)
        {
            shapeLut = new float[samples + 1];
            derivativeLut = new float[samples + 1];

            var startValue = curve.Evaluate(0f);
            var endValue = curve.Evaluate(1f);

            var maxValue = float.MinValue;
            var maxIndex = 0;

            for (int i = 0; i <= samples; ++i)
            {
                var t = i / (float)samples;
                var corrected = curve.Evaluate(t) - Mathf.Lerp(startValue, endValue, t);

                shapeLut[i] = corrected;

                if (corrected > maxValue)
                {
                    maxValue = corrected;
                    maxIndex = i;
                }
            }

            if (maxValue <= Epsilon)
            {
                maxIndex = samples / 2;
                for (int i = 0; i <= samples; ++i)
                {
                    var t = i / (float)samples;
                    shapeLut[i] = 4f * t * (1f - t);
                }
            }
            else
            {
                for (int i = 0; i <= samples; ++i)
                {
                    shapeLut[i] /= maxValue;
                }
            }

            shapeLut[0] = 0f;
            shapeLut[samples] = 0f;

            var step = 1f / samples;
            for (int i = 0; i <= samples; ++i)
            {
                var prev = Mathf.Max(0, i - 1);
                var next = Mathf.Min(samples, i + 1);

                var dt = (next - prev) * step;
                derivativeLut[i] = dt > Epsilon ? (shapeLut[next] - shapeLut[prev]) / dt : 0f;
            }

            peakTime01 = maxIndex / (float)samples;
        }

        protected float SampleLut(float[] lut, float normalizedTime, int samples)
        {
            if (lut == null || lut.Length == 0) return 0f;
            if (normalizedTime <= 0f) return lut[0];
            if (normalizedTime >= 1f) return lut[^1];

            var scaled = normalizedTime * samples;
            var index = Mathf.FloorToInt(scaled);
            index = Mathf.Clamp(index, 0, samples - 1);

            var frac = scaled - index;
            return Mathf.Lerp(lut[index], lut[index + 1], frac);
        }
    }
}
