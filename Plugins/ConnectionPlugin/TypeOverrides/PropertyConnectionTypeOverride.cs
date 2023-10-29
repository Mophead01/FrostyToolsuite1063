using FrostySdk;
using FrostySdk.Attributes;
using FrostySdk.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionPlugin.TypeOverrides
{
    public enum PropertyConnectionTargetType
    {
        PropertyConnectionTargetType_Invalid = 0,
        PropertyConnectionTargetType_ClientAndServer = 1,
        PropertyConnectionTargetType_Client = 2,
        PropertyConnectionTargetType_Server = 3,
        PropertyConnectionTargetType_NetworkedClient = 4,
        PropertyConnectionTargetType_NetworkedClientAndServer = 5
    }

    public enum InputPropertyType
    {
        InputPropertyType_Default = 0,
        InputPropertyType_Interface = 1,
        InputPropertyType_Exposed = 2,
        InputPropertyType_Invalid = 3
    }

    public class PropertyConnectionTypeOverride : BaseTypeOverride
    {
        [FieldIndex(5)]
        [Description("Target Realm for this connection.")]
        [EbxFieldMeta(EbxFieldType.Enum)]
        public PropertyConnectionTargetType TargetType { get; set; }
        [FieldIndex(6)]
        [Description("Type of object being targeted by this connection.")]
        [EbxFieldMeta(EbxFieldType.Enum)]
        public InputPropertyType InputType { get; set; }
        [FieldIndex(7)]
        [Description("If true, the source value must be a dynamic value.")]
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool SourceCantBeStatic { get; set; }

        [IsReadOnly]
        public BaseFieldOverride Flags { get; set; }

        public override void Load()
        {
            base.Load();
            dynamic propertyConnection = Original;
            if (ProfilesLibrary.DataVersion != (int)ProfileVersion.PlantsVsZombiesGardenWarfare)
            {
                uint flags = propertyConnection.Flags;
                TargetType = (PropertyConnectionTargetType)(flags & 7);
                InputType = (InputPropertyType)((flags & 48) >> 4);
                SourceCantBeStatic = Convert.ToBoolean(((flags & 8) != 0 ? 1 : 0));
            }
        }

        public override void Save(object e)
        {
            base.Save(e);
            dynamic propertyConnection = Original;
            if (ProfilesLibrary.DataVersion != (int)ProfileVersion.PlantsVsZombiesGardenWarfare)
            {
                uint newFlags = 0;
                newFlags |= (uint)TargetType;
                newFlags |= ((uint)InputType) << 4;
                if (SourceCantBeStatic)
                {
                    newFlags |= 8;
                }
                propertyConnection.Flags = newFlags;
            }
        }
    }
}
