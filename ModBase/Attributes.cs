using System;

namespace ModBase.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public abstract class ModEvent : Attribute
    {
        public readonly string HandlerName;

        public ModEvent(string handlerName)
        {
            HandlerName = handlerName;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ModEventOnEnable : ModEvent
    {
        public ModEventOnEnable(string handlerName) : base(handlerName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ModEventOnDisable : ModEvent
    {
        public ModEventOnDisable(string handlerName) : base(handlerName)
        {
        }
    }
}
