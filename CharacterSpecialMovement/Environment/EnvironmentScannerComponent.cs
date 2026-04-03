using UnityEngine;

namespace REIW
{
    public partial class EnvironmentScannerComponent : CacheMonoBehaviour
    {
        private CharacterBase _character;

        private LocalCharacter localCharacter => _character as LocalCharacter;
        private Vector3 characterDirection => (localCharacter != null ?
            (localCharacter.IsMoveInput ? localCharacter.CharacterMoveDir : localCharacter.Forward) : _character.Forward).normalized;

        public void Initialize(CharacterBase target)
        {
            _character = target;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_debugFindJumpPoints.IsNullOrEmpty())
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLineList(_debugFindJumpPoints.ToArray());
            }

            if (!_debugLastDetectedJumpPoints.IsNullOrEmpty())
            {
                for (int i = 0; i < _debugLastDetectedJumpPoints.Count; ++i)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(_debugLastDetectedJumpPoints[i], 0.1f);
                }
            }

            if (_debugUndetectedGrapplePoint)
            {
                Gizmos.color = Color.magenta;
                const float drawCount = 20f;
                var characterTransform = _character.CharacterTransform;
                var origin = characterTransform.position + characterTransform.up * _character.Radius;
                var direction = (_debugUndetectedGrapplePoint.position - origin).normalized;
                var startPos = origin + direction * 0.5f;
                Gizmos.DrawLine(startPos, _debugUndetectedGrapplePoint.position);

                origin = characterTransform.position + characterTransform.up * (_character.Height - _character.Radius);
                direction = (_debugUndetectedGrapplePoint.position - origin).normalized;
                startPos = origin + direction * 0.5f;
                for (int i = 0; i < drawCount; ++i)
                    Gizmos.DrawWireSphere(Vector3.Lerp(startPos, _debugUndetectedGrapplePoint.position, i / drawCount), _character.Radius);
            }

            if (localCharacter)
            {
                var grapple = localCharacter.CharacterMoveComponentsHandler.GetMoveComponent<CharacterMoveGrapple>();
                Gizmos.color = Color.green;
                Gizmos.DrawLineList(grapple.ObstacleAvoidPos);
            }
        }
#endif
    }
}
