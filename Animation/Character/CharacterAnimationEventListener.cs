using System;
using System.Collections.Generic;
using UnityEngine;

namespace REIW.Animations.Character
{
    public abstract class CharacterAnimationEventListener : AnimationEventListener
    {
        public static readonly IEnumerable<Type> DrivedTypes = TypeUtility.GetDerivedTypes<CharacterAnimationEventListener>();

        protected CharacterAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }
    }

    public class CommonAnimationEventListener : CharacterAnimationEventListener, ICharacterBaseEventListener
    {
        public event Action<bool> WalkEvent;
        public event Action<bool> SprintEvent;
        public event Action<bool> MountEvent;
        public event Action DashEvent;
        public event Action JumpEvent;
        public event Action JumpStartedEvent;
        public event Action LandedEvent;
        public event Action JumpCollisionDetectedEvent;
        public event Action CancelCurrentMovementEvent;

        public CommonAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnMoveStarted()
        {
        }

        public void OnWalkRequested()
        {
            WalkEvent?.Invoke(true);
        }

        public void OnWalkReleased()
        {
            WalkEvent?.Invoke(false);
        }

        public void OnDashRequested()
        {
            DashEvent?.Invoke();
        }

        public void OnDashStarted()
        {
            
        }
        
        public void OnDashReleased()
        {
            
        }

        public void OnSprintRequested()
        {
            SprintEvent?.Invoke(true);
        }
        
        public void OnSprintStarted()
        {
            
        }

        public void OnSprintReleased()
        {
            SprintEvent?.Invoke(false);
        }

        public void OnJumpRequested()
        {
            JumpEvent?.Invoke();
        }

        public void OnJumpStarted()
        {
            JumpStartedEvent?.Invoke();
        }

        public void OnJumpLanded()
        {
            LandedEvent?.Invoke();
        }

        public void OnJumpCollisionDetected()
        {
            JumpCollisionDetectedEvent?.Invoke();
        }

        public void OnMountRequested()
        {
            MountEvent?.Invoke(true);
        }

        public void OnMountStarted()
        {
            
        }

        public void OnMountReleased()
        {
            MountEvent?.Invoke(false);
        }

        public void OnCancelCurrentMovement()
        {
        }
    }

    public class WallClimbAnimationEventListener : CharacterAnimationEventListener, IMoveWallClimbEventListener
    {
        public event Action<bool> GravityChangedEvent;

        public WallClimbAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnGravityChangeStarted(bool isDownSnapping)
        {
        }

        public void OnGravityChangeFinished(bool worldGravity)
        {
            GravityChangedEvent?.Invoke(worldGravity);
        }

        public void OnWallClimbStarted()
        {
        }

        public void OnWallClimbFinished()
        {
        }

        public void OnFailedMovementToEdge(Vector3 failPoint, Vector3 failNormal)
        {
        }
    }

    public class GrappleAnimationEventListener : CharacterAnimationEventListener, IMoveGrappleEventListener
    {
        public event Action<GrapplePoint, Vector3, float, bool, Action<bool>> GrappleRequestedEvent;
        public event Action<GrapplePoint, float> GrappleStartedEvent;
        public event Action<bool> GrappleArrivalEvent;
        public event Action<Action<bool>> GrappleLaunchRequestedEvent;
        public event Action GrappleLaunchLandedEvent;
        public event Action<GrapplePoint, GrapplePoint, Vector3> GrapplePointTargetedEvent;

        public GrappleAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnGrappleRequested(GrapplePoint target, Vector3 grapplePosition, float grappleDistance,
            bool isFar, Action<bool> funcStartGrapple)
        {
            GrappleRequestedEvent?.Invoke(target, grapplePosition, grappleDistance, isFar, funcStartGrapple);
        }

        public void OnGrappleStarted(GrapplePoint target, float grappleMoveTime)
        {
            GrappleStartedEvent?.Invoke(target, grappleMoveTime);
        }

        public void OnGrappleArrival(bool force)
        {
            GrappleArrivalEvent?.Invoke(force);
        }

        public void OnGrappleLaunchRequested(Action<bool> funcStartLaunch)
        {
            GrappleLaunchRequestedEvent?.Invoke(funcStartLaunch);
        }

        public void OnGrappleLaunchStarted()
        {
        }

        public void OnGrappleLaunchLanding()
        {
            GrappleLaunchLandedEvent?.Invoke();
        }

        public void OnGrapplePointTargeted(GrapplePoint prev, GrapplePoint target, Vector3 grapplePosition)
        {
            GrapplePointTargetedEvent?.Invoke(prev, target, grapplePosition);
        }
    }

    public class ParkourAnimationEventListener : CharacterAnimationEventListener, IMoveParkourEventListener
    {
        public ParkourAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public event Action<ParkourActionData, Action<bool, float>> ParkourRequestedEvent;
        public event Action<Action> ParkourStartedEvent;

        public void OnParkourRequested(ParkourActionData actionData, Action<bool, float> funcStartParkour)
        {
            ParkourRequestedEvent?.Invoke(actionData, funcStartParkour);
        }

        public void OnParkourStarted(ParkourActionData actionData, Action funcFinishedParkour)
        {
            ParkourStartedEvent?.Invoke(funcFinishedParkour);
        }

        public void OnParkourFinished()
        {
        }
    }

    public class GatheringAnimationEventListener : CharacterAnimationEventListener, IGatheringEventListener
    {
        public GatheringAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public event Action<EnumGathering, float> StartGatheringEvent;
        public event Action StopGatheringEvent;
        public event Action StartGatheringSuccessEvent;

        public void OnStartGathering(EnumGathering gatheringType, float gatheringSpeed = 1f)
        {
            StartGatheringEvent?.Invoke(gatheringType, gatheringSpeed);
        }

        public void OnStopGathering()
        {
            StopGatheringEvent?.Invoke();
        }

        public void OnStartGatheringSuccess()
        {
            StartGatheringSuccessEvent?.Invoke();
        }
    }

    public class FishingAnimationEventListener : CharacterAnimationEventListener, IFishingEventListener
    {
        public event Action<eAnimationType> PlayFishingAnimation;

        public FishingAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnFishing(eAnimationType animationType)
        {
            PlayFishingAnimation?.Invoke(animationType);
        }
    }

    public class EmoteAnimationEventListener : CharacterAnimationEventListener, IEmoteEventListener
    {
        public event Action<uint, long, bool> EmoteEvent;
        public event Action GroupReadyEmoteEvent;
        public event Action<uint, long> GroupMainEmoteEvent;
        public event Action StopEvent;

        public EmoteAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnEmote(uint serial, long startTime, bool isHost = false)
        {
            EmoteEvent?.Invoke(serial, startTime, isHost);
        }

        public void OnEmoteGroupReady()
        {
            GroupReadyEmoteEvent?.Invoke();
        }

        public void OnEmoteGroupMain(uint serial, long startTime)
        {
            GroupMainEmoteEvent?.Invoke(serial, startTime);
        }

        public void OnEmoteStop()
        {
            StopEvent?.Invoke();
        }
    }

    public class CinematicAnimationEventListener : CharacterAnimationEventListener, ICinematicEventListener
    {
        public event Action EnterCinematicEvent;
        public event Action<eAnimationType> PlayClipEvent;
        public event Action ExitCinematicEvent;
        public event Action<FacialAnimationType> PlayFacialEvent;
        public event Action StopFacialEvent;

        public CinematicAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }

        public void OnEnterCinematic()
        {
            EnterCinematicEvent?.Invoke();
        }
        
        public void OnPlayClip(eAnimationType cinematicType)
        {
            PlayClipEvent?.Invoke(cinematicType);
        }
        
        public void OnExitCinematic()
        {
            ExitCinematicEvent?.Invoke();
        }

        public void OnPlayFacial(FacialAnimationType type)
        {
            PlayFacialEvent?.Invoke(type);
        }
        
        public void OnStopFacial()
        {
            StopFacialEvent?.Invoke();
        }
    }
}
