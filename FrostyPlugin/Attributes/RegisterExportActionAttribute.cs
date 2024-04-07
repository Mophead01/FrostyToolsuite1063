using FrostySdk.Managers;
using System;

namespace Frosty.Core.Attributes
{
    public enum ExportType
    {
        All,
        LaunchOnly,
        KyberLaunchOnly,
        FrostyLaunchOnly,
        ExportOnly,
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class RegisterExportActionAttribute : Attribute
    {
        public Type LaunchClass { get; set; }
        public ExportType ExportType { get; set; }
        public int Priority { get; set; }

        public RegisterExportActionAttribute(Type launchClass, ExportType exportType, int priority)
        {
            LaunchClass = launchClass;
            ExportType = exportType;
            Priority = priority;
        }
    }
}
