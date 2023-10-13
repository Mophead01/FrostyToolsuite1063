using System;

namespace Frosty.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public class RegisterPointerRefIdOverrideAttribute : Attribute
    {
        public string LookupName { get; set; }
        public Type PrIdType { get; set; }
        public RegisterPointerRefIdOverrideAttribute(string lookupName, Type type)
        {
            LookupName = lookupName;
            PrIdType = type;
        }
    }
}
