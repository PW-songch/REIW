using REIW.BR;
using UnityEngine;

namespace REIW.Animations.Character.BR
{
    public class AdsStateChangeModule : StateChangeModule
    {
        public AdsStateChangeModule(MonoBehaviour animation) : base(animation)
        {
        }

        public override bool CanExitState()
        {
            return _movementState != eMovementState.None && !_movementState.HasFlagUnsafe(eMovementState.Ads);
        }
    }
}
