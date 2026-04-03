using System;

namespace REIW.Animations
{
    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false, Inherited = false)]
    public sealed class AnimationTypeAttribute : Attribute
    {
        public Type StateEnumType { get; }
        public object StateValueBoxed { get; }

        public AnimationTypeAttribute(object stateTypeValue)
        {
            if (stateTypeValue is null)
                throw new ArgumentNullException(nameof(stateTypeValue));

            var stateEnumType = stateTypeValue.GetType();
            if (!stateEnumType.IsEnum)
                throw new ArgumentException("stateTypeValue must be an enum type.");

            StateEnumType = stateEnumType;
            StateValueBoxed = stateTypeValue;
        }
    }
}
