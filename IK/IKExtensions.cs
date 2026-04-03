using RootMotion.FinalIK;
using UnityEngine;

namespace REIW
{
    public static class IKExtensions
    {
        public static float FootHeightFromGround(this Grounding.Leg leg, Vector3 groundPosition)
        {
            return leg.IKPosition.y - groundPosition.y;
        }

        public static float FootHeightFromGround(this Grounding.Leg leg)
        {
            return leg.FootHeightFromGround(leg.GetHitPoint.point);
        }

        public static float FootDistanceFromGround(this Grounding.Leg leg, Vector3 groundPosition)
        {
            return Vector3.Distance(leg.IKPosition, groundPosition);
        }

        public static float FootDistanceFromGround(this Grounding.Leg leg)
        {
            return leg.FootDistanceFromGround(leg.GetHitPoint.point);
        }
    }
}
