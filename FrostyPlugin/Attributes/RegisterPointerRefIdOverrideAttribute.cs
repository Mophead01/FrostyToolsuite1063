using System;

namespace Frosty.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class RegisterPointerRefIdOverrideAttribute : Attribute
    {
        public string LookupName { get; set; }
        public Type PrIdType { get; set; }
        public bool ApplyToChildClasses { get; set; }
        public int Priority { get; set; }
        public RegisterPointerRefIdOverrideAttribute(string lookupName, Type type, bool applyToChildClasses = false, int priority = -1)
        {
            LookupName = lookupName;
            PrIdType = type;
            ApplyToChildClasses = applyToChildClasses;
            Priority = priority;
        }
    }
}
