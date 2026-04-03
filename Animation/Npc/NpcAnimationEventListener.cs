using System;
using System.Collections.Generic;
using UnityEngine;

namespace REIW.Animations.Npc
{
    public abstract class NpcAnimationEventListener : AnimationEventListener
    {
        public static readonly IEnumerable<Type> DerivedTypes = TypeUtility.GetDerivedTypes<NpcAnimationEventListener>();
        
        protected NpcAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }
    }
    
    public class NpcCinematicAnimationEventListener : NpcAnimationEventListener, INpcCinematicEventListener
    {
        public event Action<string /*race*/, string /*gender*/> EnterCinematicEvent;
        public event Action<eAnimationType> PlayClipEvent;
        public event Action ExitCinematicEvent;
        public event Action<Animations.FacialAnimationType> PlayFacialEvent;
        public event Action StopFacialEvent;
        
        public NpcCinematicAnimationEventListener(EventBus eventBus) : base(eventBus)
        {
        }
        
        public void OnEnterCinematic(string race, string gender)
        {
            EnterCinematicEvent?.Invoke(race, gender);
        }
        
        public void OnPlayClip(eAnimationType cinematicType)
        {
            PlayClipEvent?.Invoke(cinematicType);
        }
        
        public void OnExitCinematic()
        {
            ExitCinematicEvent?.Invoke();
        }
        
        public void OnPlayFacial(Animations.FacialAnimationType type)
        {
            PlayFacialEvent?.Invoke(type);
        }
        
        public void OnStopFacial()
        {
            StopFacialEvent?.Invoke();
        }
    }
}
