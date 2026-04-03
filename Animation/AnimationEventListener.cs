namespace REIW.Animations
{
    public abstract class AnimationEventListener : IEventListener
    {
        private bool _isRegistered;

        public AnimationEventListener(EventBus eventBus)
        {
            Register(eventBus);
        }

        public void Register(EventBus eventBus)
        {
            if (_isRegistered)
                return;

            if (eventBus != null)
            {
                eventBus.Register(this);
                _isRegistered = true;
            }
        }

        public void Unregister(EventBus eventBus)
        {
            if (_isRegistered && eventBus != null)
            {
                eventBus.Unregister(this);
                _isRegistered = false;
            }
        }
    }
}
