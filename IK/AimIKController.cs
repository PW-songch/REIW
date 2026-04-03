using RootMotion.FinalIK;
using UnityEngine;

namespace REIW.Animations.Character
{
    public class AimIKController : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _headLookWeight = 1f;
        [SerializeField] private Vector3 _gunHoldOffset;
        [SerializeField] private Vector3 _leftHandOffset;
        [SerializeField] private Recoil _recoil;
        [SerializeField] private float _recoilMagnitude = 1f;

        private FullBodyBipedIK _fbbIK;
        private AimIK _aimIK;

		private Vector3 _headLookAxis;
		private Vector3 _leftHandPosRelToRightHand;
		private Quaternion _leftHandRotRelToRightHand;
		private Vector3 _aimTarget;
		private Quaternion _rightHandRotation;

		public AimIK AimIK => _aimIK;

		private void OnDestroy()
		{
			if (_fbbIK)
				_fbbIK.solver.OnPreRead -= OnPreRead;
		}

		public void Initialize(FullBodyBipedIK fbbIK, AimIK aimIK)
		{
			_fbbIK = fbbIK;
			_aimIK = aimIK;

			if (!_aimIK)
				_aimIK = GetComponent<AimIK>();
			if (!_fbbIK)
				_fbbIK = GetComponent<FullBodyBipedIK>();

			if (!_aimIK || !_fbbIK)
			{
				enabled = false;
				return;
			}
			
			_aimIK.enabled = false;
			
			_headLookAxis = _fbbIK.references.head.InverseTransformVector(_fbbIK.references.root.forward);

			if (_recoil)
			{
				_recoil.ik = _fbbIK;
				_recoil.aimIK = _aimIK;
			}
		}

		public void Enable(bool enable)
		{
			if (!_fbbIK || !_aimIK)
				return;

			enabled = enable;

			if (enable)
				_aimIK.enabled = false;

			if (enable)
				_fbbIK.solver.OnPreRead += OnPreRead;
			else
				_fbbIK.solver.OnPreRead -= OnPreRead;
		}

		public void UpdateAim(Vector3 aimTarget)
		{
			if (!_aimIK || !_fbbIK)
				return;
			
			_aimTarget = aimTarget;
			
			Read();
            UpdateAimIK();
            UpdateFBBIK();
            UpdateAimIK();
            HeadLookAt(aimTarget);
		}

		public void Fire()
		{
			_recoil?.Fire(_recoilMagnitude);
		}

		private void Read()
		{
			_leftHandPosRelToRightHand = _fbbIK.references.rightHand.InverseTransformPoint(_fbbIK.references.leftHand.position);
			_leftHandRotRelToRightHand = Quaternion.Inverse(_fbbIK.references.rightHand.rotation) * _fbbIK.references.leftHand.rotation;
		}

		private void UpdateAimIK()
		{
			_aimIK.solver.IKPosition = _aimTarget;
			_aimIK.solver.Update();
		}
		
		private void UpdateFBBIK()
		{
			_rightHandRotation = _fbbIK.references.rightHand.rotation;
			
			Vector3 rightHandOffset = _fbbIK.references.rightHand.rotation * _gunHoldOffset;
			_fbbIK.solver.rightHandEffector.positionOffset += rightHandOffset;

			if (_recoil != null) _recoil.SetHandRotations(_rightHandRotation * _leftHandRotRelToRightHand, _rightHandRotation);
			
			_fbbIK.solver.Update();
			
			if (_recoil != null) {
				_fbbIK.references.rightHand.rotation = _recoil.rotationOffset * _rightHandRotation;
				_fbbIK.references.leftHand.rotation = _recoil.rotationOffset * _rightHandRotation * _leftHandRotRelToRightHand;
			} else {
				_fbbIK.references.rightHand.rotation = _rightHandRotation;
				_fbbIK.references.leftHand.rotation = _rightHandRotation * _leftHandRotRelToRightHand;
			}
		}

		private void OnPreRead()
		{
			Quaternion r = _recoil != null? _recoil.rotationOffset * _rightHandRotation: _rightHandRotation;
			Vector3 leftHandTarget = _fbbIK.references.rightHand.position + _fbbIK.solver.rightHandEffector.positionOffset + r * _leftHandPosRelToRightHand;
			_fbbIK.solver.leftHandEffector.positionOffset += leftHandTarget - _fbbIK.references.leftHand.position - _fbbIK.solver.leftHandEffector.positionOffset + r * _leftHandOffset;
		}
		
		private void HeadLookAt(Vector3 lookAtTarget)
		{
			Quaternion headRotationTarget = Quaternion.FromToRotation(_fbbIK.references.head.rotation * _headLookAxis, lookAtTarget - _fbbIK.references.head.position);
			_fbbIK.references.head.rotation = Quaternion.Lerp(Quaternion.identity, headRotationTarget, _headLookWeight) * _fbbIK.references.head.rotation;
		}
    }
}
