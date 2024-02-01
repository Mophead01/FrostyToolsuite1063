using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Attributes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using System.Windows.Media;
using FrostyNM;
using System.Xml.Linq;

namespace ConnectionPlugin.Editors
{

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

        [IsHidden]
        public BaseFieldOverride Flags { get; set; }

        public override void Load()
        {
            base.Load();
            dynamic propertyConnection = Original;
            uint flags = propertyConnection.Flags;
            TargetType = (PropertyConnectionTargetType)(flags & 7);
            InputType = (InputPropertyType)((flags & 48) >> 4);
            SourceCantBeStatic = Convert.ToBoolean(((flags & 8) != 0 ? 1 : 0));
        }

        public override void Save(object e)
        {
            base.Save(e);
            dynamic propertyConnection = Original;
            uint newFlags = 0;
            newFlags |= (uint)TargetType;
            newFlags |= ((uint)InputType) << 4;
            if (SourceCantBeStatic)
            {
                newFlags |= 8;
            }
            propertyConnection.Flags = newFlags;
        }
        public uint[] GetPConnectionDataFromFlags(uint flags)
        {
            uint[] data = new uint[3];
            data[0] = flags & 7; //target type
            data[1] = (flags & 48) >> 4; //input type
            data[2] = (uint)((flags & 8) != 0 ? 1 : 0); //source cant be static
            return data;
        }
    }
    public enum ConnectionType
    {
        PropertyConnection,
        LinkConnection,
        EventConnection
    }
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
    public class PropertyConnectionEditor : FrostyTypeEditor<PropertyConnectionControl>
    {
        public PropertyConnectionEditor()
        {
            ValueProperty = PropertyConnectionControl.ValueProperty;
            NotifyOnTargetUpdated = true;
        }
    }
    [TemplatePart(Name = "PART_StackPanel", Type = typeof(StackPanel))]
    public class PropertyConnectionControl : Control
    {
        #region -- Properties --

        #region -- Value --
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(PropertyConnectionControl), new FrameworkPropertyMetadata(null));
        public object Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        #endregion

        #endregion

        static PropertyConnectionControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyConnectionControl), new FrameworkPropertyMetadata(typeof(PropertyConnectionControl)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Loaded += PropertyConnectionControl_Loaded;
        }

        private bool firstTime = true;
        private void PropertyConnectionControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (firstTime)
            {
                TargetUpdated += PropertyConnectionControl_TargetUpdated;
                RefreshUI();
                firstTime = false;
            }
        }

        private void PropertyConnectionControl_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            RefreshUI();
        }

        protected virtual void RefreshUI()
        {
            dynamic propConnection = Value;
            StackPanel sp = GetTemplateChild("PART_StackPanel") as StackPanel;

            EbxAsset parentAsset = GetParentEditor().Asset;
            Dictionary<string, FrameworkElement> panels = Enumerable.Range(0, sp.Children.Count).ToDictionary(idx => ((FrameworkElement)sp.Children[idx]).Name, idx => (FrameworkElement)sp.Children[idx]);

            void CompleteConnection(string type, PointerRef pr, string field)
            {
                (string, string, string, string) entityTexts = GetEntity(pr, parentAsset);
                (string, string, string) sanitizedTexts = Sanitize(pr, field);
                void SetOrRemove(string name, bool dontRemove, string newValue)
                {
                    //if (dontRemove)
                    //    (panels["PART_" + type + name] as TextBlock).Text = newValue;
                    //else
                    //    sp.Children.Remove(panels["PART_" + type + name]);
                    if (dontRemove)
                    {
                        (panels["PART_" + type + name] as TextBlock).Text = newValue;
                        panels["PART_" + type + name].Visibility = Visibility.Visible;
                    }
                    else
                        panels["PART_" + type + name].Visibility = Visibility.Hidden;
                }
                SetOrRemove("ExternalName", entityTexts.Item1 != "", entityTexts.Item1 + "/");
                SetOrRemove("Idx", entityTexts.Item4 != "", entityTexts.Item4);
                SetOrRemove("Name", true, entityTexts.Item2);
                SetOrRemove("Override", entityTexts.Item3 != "", "(" + entityTexts.Item3 + ")");
                SetOrRemove("Field", true, sanitizedTexts.Item1);
                SetOrRemove("InterfaceIdx", sanitizedTexts.Item2 != "", sanitizedTexts.Item2);
                SetOrRemove("InterfaceValue", sanitizedTexts.Item3 != "", " " + sanitizedTexts.Item3);
            }
            CompleteConnection("Source", propConnection.Source, propConnection.SourceField);
            CompleteConnection("Target", propConnection.Target, propConnection.TargetField);
            (panels["PART_ConnectionTargetRealm"] as Image).Source = GetPropFlagImage(propConnection.Flags);
            (panels["PART_ConnectionTargetType"] as TextBlock).Text = GetPropFlagType(propConnection.Flags);

            //(sp.Children[14] as TextBlock).Text = GetPropFlagType(propConnection.Flags);
            //(sp.Children[13] as Image).Source = GetPropFlagImage(propConnection.Flags);
            //sanitizedTexts = Sanitize(propConnection.Target, propConnection.TargetField);
            //if (sanitizedTexts.Item2 != "")
            //    (sp.Children[12] as TextBlock).Text = " " + sanitizedTexts.Item2;
            //else
            //    sp.Children.RemoveAt(12);
            //(sp.Children[11] as TextBlock).Text = sanitizedTexts.Item1;

            //entityTexts = GetEntity(propConnection.Target);
            //if (entityTexts.Item3 != "")
            //    (sp.Children[9] as TextBlock).Text = "(" + entityTexts.Item3 + ")";
            //else
            //    sp.Children.RemoveAt(9);
            //(sp.Children[8] as TextBlock).Text = entityTexts.Item2;

            //if (entityTexts.Item1 != "")
            //    (sp.Children[7] as TextBlock).Text = entityTexts.Item1 + "/";
            //else
            //    sp.Children.RemoveAt(7);

            //sanitizedTexts = Sanitize(propConnection.Source, propConnection.SourceField);
            //if (sanitizedTexts.Item2 != "")
            //    (sp.Children[5] as TextBlock).Text = " " + sanitizedTexts.Item2;
            //else
            //    sp.Children.RemoveAt(5);
            //(sp.Children[4] as TextBlock).Text = sanitizedTexts.Item1;

            //entityTexts = GetEntity(propConnection.Source);
            //if (entityTexts.Item3 != "")
            //    (sp.Children[2] as TextBlock).Text = "(" + entityTexts.Item3 + ")";
            //else
            //    sp.Children.RemoveAt(2);
            //(sp.Children[1] as TextBlock).Text = entityTexts.Item2;
            //if (entityTexts.Item1 != "")
            //    (sp.Children[0] as TextBlock).Text = entityTexts.Item1 + "/";
            //else
            //    sp.Children.RemoveAt(0);
        }
        private ImageSourceConverter imageSourceConv = new ImageSourceConverter();

        //Flags dictionary for property connections
        public Dictionary<uint, string> flagToImageNameDictionary = new Dictionary<uint, string>()
        {
          { 0, "Invalid" },
          { 1, "ClientAndServer" },
          { 2, "Client" },
          { 3, "Server" },
          { 4, "NetworkedClient" },
          { 5, "NetworkedClientAndServer" }
        };
        public Dictionary<uint, string> flagTypeToNameDictionary = new Dictionary<uint, string>()
        {
            {0, "Default" },
            {1, "Interface" },  //Going into an interface
            {2, "Exposed" },    //Going to an object
            {3, "Invalid" }
        };
        public string GetPropFlagType(uint flags)
        {
            uint Type = (flags & 48) >> 4;
            bool NonStatic = (flags & 8) != 0;

            return flagTypeToNameDictionary[Type] + (NonStatic == true ? ", true" : "");
        }

        public ImageSource GetPropFlagImage(uint flags)
        {
            uint Realm = flags & 7;
            string imageName = string.Format("{0}.png", flagToImageNameDictionary[Realm]);

            return (ImageSource)imageSourceConv.ConvertFromString(string.Format("pack://application:,,,/ConnectionPlugin;component/Resources/{0}", imageName));
        }

        public ImageSource GetEventFlagImage(Enum targetType)
        {
            string imageName = string.Format("{0}.png", targetType.ToString().Replace("EventConnectionTargetType_", ""));
            return (ImageSource)imageSourceConv.ConvertFromString(string.Format("pack://application:,,,/ConnectionPlugin;component/Resources/{0}", imageName));
        }
        private void CommentButton_Click(object sender, RoutedEventArgs e)
        {
            //
        }
        public string[] ClutterNames =
        {
            "EntityData",
            "erenceObjectData",
            "ObjectData",
            "ComponentData",
            "DescriptorData",
            "Data",
        };

        protected virtual (string, string, string, string) GetEntity(PointerRef pr, EbxAsset parentAsset)
        {
            string ExternalName = "";
            string Index = "";
            string Type = "";
            string objIndex = "";

            object resolvedPr;
            string GetIndex(dynamic blueprint, Guid classGuid, dynamic internalObj)
            {
                if (TypeLibrary.IsSubClassOf(blueprint.GetType().Name, "Blueprint"))
                {
                    int idx = 0;
                    foreach (dynamic reference in blueprint.Objects)
                    {
                        if (reference.Internal.__InstanceGuid == classGuid || (internalObj != null && reference.Internal.__InstanceGuid == internalObj))
                            return "[" + idx + "]";
                        idx++;
                    }
                }
                else
                    App.Logger.LogWarning("Not blueprint type");
                return "";
            }
            if (pr.Type == PointerRefType.Internal)
            {
                resolvedPr = pr.Internal;
                objIndex = GetIndex(parentAsset.RootObject, pr.External.ClassGuid, ((dynamic)pr.Internal).__InstanceGuid);
            }
            else if (pr.Type == PointerRefType.External && App.AssetManager.GetEbxEntry(pr.External.FileGuid) != null)
            {
                ExternalName = App.AssetManager.GetEbxEntry(pr.External.FileGuid).Filename;
                EbxAsset importAsset = App.AssetManager.GetEbx(App.AssetManager.GetEbxEntry(pr.External.FileGuid));
                resolvedPr = importAsset.GetObject(pr.External.ClassGuid);
                objIndex = GetIndex(importAsset.RootObject, pr.External.ClassGuid, null);
            }
            else
                return ("", "(null)", "", "");

            Type = resolvedPr.GetType().Name;

            string ExtraInfo = App.PluginManager.GetPointerRefIdOverride(resolvedPr, 35);

            for (int i = 0; i < ClutterNames.Length; i++)
            {
                if (Type.EndsWith(ClutterNames[i]))
                {
                    Type = Type.Remove(Type.Length - ClutterNames[i].Length);
                }
            }
            return (ExternalName, Index + Type, ExtraInfo, objIndex);
        }
        private int FNVHashDefault(string FNVInput)
        {
            int FNV_offset_basis = 5381;
            int FNV_prime = 33;
            for (int i = 0; i < FNVInput.Length; i++)
            {
                byte b = (byte)FNVInput[i];
                FNV_offset_basis = (FNV_offset_basis * FNV_prime) ^ b;
            }

            return FNV_offset_basis;
        }

        private uint HashStringId(string stringId)
        {
            uint result = 0xFFFFFFFF;
            for (int i = 0; i < stringId.Length; i++)
                result = stringId[i] + 33 * result;
            return result;
        }
        private Dictionary<string, string> LocalInterfaceVariable = new Dictionary<string, string>();
        protected virtual (string, string, string) Sanitize(PointerRef pr, string value, bool isSource = false)
        {
            string retVal = value;
            string interfaceIdx = "";
            string interfaceValue = "";

            int idx = 0;
            if (pr.Type == PointerRefType.Internal && TypeLibrary.IsSubClassOf(pr.Internal, "InterfaceDescriptorData"))
            {
                interfaceIdx = "[INVALID]";
                dynamic interfaceDesc = pr.Internal;
                foreach (dynamic field in interfaceDesc.Fields)
                {
                    if (field.Name == value)
                    {
                        interfaceIdx = "[" + idx.ToString() + "]";
                        if (field.ValueRef.Type != PointerRefType.Null)
                        {
                            var entry = App.AssetManager.GetEbxEntry(field.ValueRef.External.FileGuid);
                            interfaceValue += " (" + entry.Type + " '" + entry.Filename + "')";
                        }
                        else
                        {
                            if (ProfilesLibrary.DataVersion == (int)ProfileVersion.PlantsVsZombiesBattleforNeighborville || ProfilesLibrary.DataVersion == (int)ProfileVersion.NeedForSpeedHeat)
                            {
                                string val = field.BoxedValue.ToString();
                                if (val != "(null)")
                                {
                                    interfaceValue += " (" + val + ")";
                                }
                            }
                            else
                            {
                                if (field.Value != "")
                                {
                                    interfaceValue += " (" + field.Value + ")";
                                }
                            }
                        }
                        break;
                    }
                    idx++;
                }
            }
            if (value.StartsWith("0x"))
                retVal = value.Remove(0, 2);
            if (value == "00000000")
                retVal = "Self";
            return (retVal, interfaceIdx, interfaceValue);
        }

        public FrostyAssetEditor GetParentEditor()
        {
            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
            while (!(parent.GetType().IsSubclassOf(typeof(FrostyAssetEditor)) || parent is FrostyAssetEditor))
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            return (parent as FrostyAssetEditor);
        }
    }
}
