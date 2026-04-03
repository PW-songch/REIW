
namespace REIW.Animations.Character
{
        public partial class CharacterAnimation
        {
            protected override void InitializeAnimationEventListener()
            {
                base.InitializeAnimationEventListener();

                if (Character == null)
                    return;

                CreateAnimationEventListeners(CharacterAnimationEventListener.DrivedTypes, Character.EventBus);
                SetAnimationEventListeners();

                Character.OnInitialized += OnCharacterInitialized;
            }
            
            private void OnCharacterInitialized()
            {
                RegisterAnimationEventListeners(Character.EventBus);
            }

            #region AnimationEventListeners Settings

            /// <summary>
            /// EventListener 연결 함수명 규칙
            /// 1. 기본 이름 : "ConnectingEvents_"
            /// 2. namespace 이름 : EventListener의 namespace에서 AnimationBase의 namespace(REIW.Animations)를 제외한 각 이름들을 "_"로 연결
            /// ex) REIW.Animations.Character.CommonAnimationEventListener의 경우 REIW.Animations.Character에서 REIW.Animations를 제외한 "Character_"
            /// 3. EventListener 이름
            /// 함수명 : 1 + 2 + 3
            /// </summary>
            private void ConnectingEvents_Character_CommonAnimationEventListener()
            {
                var characterEL = GetAnimationEventListeners<CommonAnimationEventListener>();
                {
                    characterEL.WalkEvent += (InEnable) => { Movement.IsWalkInput = InEnable; };
                    characterEL.JumpEvent += () => { Movement.IsJumpInput = true; };
                    characterEL.JumpStartedEvent += () => { StateMachine.Jump.StartJump(); };
                    characterEL.LandedEvent += () => { Movement.IsLanding = true; };
                    characterEL.JumpCollisionDetectedEvent += () => { Movement.SetAirbornStateGrounderIKMaxStep(false); };
                    characterEL.MountEvent += (InEnable) => { Movement.IsMountInput = InEnable; };
                    characterEL.CancelCurrentMovementEvent += () => { StateMachine.CurrentState.ExitState = true; };
                    characterEL.DashEvent += () => { Movement.IsDashInput = true; };
                    characterEL.SprintEvent += (InEnable) => { Movement.IsSprintInput = InEnable; };
                }
            }

            private void ConnectingEvents_Character_WallClimbAnimationEventListener()
            {
                var wallClimbEL = GetAnimationEventListeners<WallClimbAnimationEventListener>();
                {
                    wallClimbEL.GravityChangedEvent +=
                        (InWorldGravity) => Movement.GravityChange(InWorldGravity);
                }
            }

            private void ConnectingEvents_Character_GrappleAnimationEventListener()
            {
                var grappleEL = GetAnimationEventListeners<GrappleAnimationEventListener>();
                {
                    grappleEL.GrappleRequestedEvent += (InTarget, InGrapplePosition, InGrappleDistance,
                        InFar, InStartGrappleCallback) =>
                    {
                        if (StateMachine.Grapple.IsEnableThrowGrapple)
                        {
                            Movement.IsGrappleInput = true;
                            StateMachine.Grapple.SetGrappleInfo(
                                GrappleAnimationState.GrappleInformation.Create(InTarget, InGrapplePosition,
                                    InGrappleDistance, InFar, InStartGrappleCallback));
                        }
                        else
                        {
                            InStartGrappleCallback?.Invoke(false);
                        }
                    };

                    grappleEL.GrappleStartedEvent += (InTarget, InGrappleMoveTime) =>
                    {
                        StateMachine.Grapple.StartGrapple(InTarget, InGrappleMoveTime);
                    };

                    grappleEL.GrappleArrivalEvent += (force) =>
                    {
                        StateMachine.Grapple.ArriveGrapple(force);
                    };

                    grappleEL.GrappleLaunchRequestedEvent += (InStartLaunchCallback) =>
                    {
                        StateMachine.Grapple.LaunchRequested(InStartLaunchCallback);
                    };

                    grappleEL.GrappleLaunchLandedEvent += () =>
                    {
                        StateMachine.Grapple.LandingLaunch();
                    };
                }
            }

            private void ConnectingEvents_Character_ParkourAnimationEventListener()
            {
                var parkourEL = GetAnimationEventListeners<ParkourAnimationEventListener>();
                {
                    parkourEL.ParkourRequestedEvent += (InActionData, InStartParkourCallback) =>
                    {
                        Movement.IsParkourInput = true;
                        if (StateMachine.CurrentState.StateBaseType != eStateType.PARKOUR)
                            StateMachine.CurrentState.ExitState = true;
                        StateMachine.Parkour.SetParkourInfo(
                            ParkourAnimationState.ParkourInformation.Create(InActionData, InStartParkourCallback));
                    };
                    parkourEL.ParkourStartedEvent += InFinishedParkourCallback =>
                    {
                        StateMachine.Parkour.StartParkourAction(InFinishedParkourCallback);
                    };
                }
            }

            private void ConnectingEvents_Character_GatheringAnimationEventListener()
            {
                var gatheringEL = GetAnimationEventListeners<GatheringAnimationEventListener>();
                {
                    gatheringEL.StartGatheringEvent += (InGatheringType, gatheringSpeed) =>
                    {
                        StateMachine.Gathering.PlayAnimationType =
                            StateMachine.Gathering.ConvertToAnimationType(InGatheringType);
                        StateMachine.Gathering.PlayAnimationSpeed = gatheringSpeed;
                    };

                    gatheringEL.StopGatheringEvent += () =>
                    {
                        StateMachine.Gathering.PlayAnimationType = eAnimationType.NONE;
                    };

                    gatheringEL.StartGatheringSuccessEvent += () =>
                    {
                        StateMachine.Gathering.PlayAnimationType = (eAnimationType)GatheringAnimationState.eAnimationType.GATHERING_SUCCESS;
                        StateMachine.Gathering.PlayAnimationSpeed = 1f;
                    };
                }
            }

            private void ConnectingEvents_Character_FishingAnimationEventListener()
            {
                var fishingEL = GetAnimationEventListeners<FishingAnimationEventListener>();
                {
                    fishingEL.PlayFishingAnimation += (type) =>
                    {
                        StateMachine.Fishing.PlayAnimationType = type;
                    };
                }
            }

            private void ConnectingEvents_Character_EmoteAnimationEventListener()
            {
                var emoteEL = GetAnimationEventListeners<EmoteAnimationEventListener>();
                {
                    emoteEL.EmoteEvent += (serial, startTime, isHost) =>
                    {
                        StateMachine.Emote.PlayAnimation(serial, startTime, isHost);
                    };

                    emoteEL.GroupReadyEmoteEvent += () =>
                    {
                        StateMachine.Emote.SetGroupReady();
                    };
                    
                    emoteEL.GroupMainEmoteEvent += (serial, startServerTime) =>
                    {
                        StateMachine.Emote.TriggerMainAnimation(serial, startServerTime);
                    };
                    
                    emoteEL.StopEvent += () =>
                    {
                        StateMachine.Emote.Dispose();
                    };
                }
            }
            
            private void ConnectingEvents_Character_CinematicAnimationEventListener()
            {
                var cinematicEL = GetAnimationEventListeners<CinematicAnimationEventListener>();
                {
                    cinematicEL.EnterCinematicEvent += () =>
                    {
                        StateMachine.Cinematic.OnEnterCinematic();
                    };
                    
                    cinematicEL.PlayClipEvent += (animationType) =>
                    {
                        StateMachine.Cinematic.PlayAnimation(animationType);
                    };
                    
                    cinematicEL.ExitCinematicEvent += () =>
                    {
                        StateMachine.Cinematic.OnExitCinematic();
                    };
                    
                    cinematicEL.PlayFacialEvent += (facialType) =>
                    {
                        StateMachine.Cinematic.OnPlayFacial(facialType);
                    };
                    
                    cinematicEL.StopFacialEvent += () =>
                    {
                        StateMachine.Cinematic.OnStopFacial();
                    };
                }
            }
            #endregion
        }
}
