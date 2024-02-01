using Frosty.Core.Controls.Editors;
using FrostySdk.Ebx;
using System.Windows;
using Frosty.Core.Controls.Editors;
using FrostySdk.Ebx;
using System;
using System.Windows;
using System.Windows.Controls;
using FrostySdk.IO;
using FrostySdk;
using System.Collections.Generic;
using System.Linq;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Markup;
using System.Windows.Media;

namespace ConnectionPlugin.Editors
{
    public class LinkConnectionEditor : FrostyTypeEditor<LinkConnectionControl>
    {
        public LinkConnectionEditor()
        {
            ValueProperty = PropertyConnectionControl.ValueProperty;
            NotifyOnTargetUpdated = true;
        }
    }
    public class LinkConnectionControl : PropertyConnectionControl
    {
        static LinkConnectionControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(LinkConnectionControl), new FrameworkPropertyMetadata(typeof(LinkConnectionControl)));
        }
        protected override void RefreshUI()
        {
            dynamic linkConnection = Value;
            StackPanel sp = GetTemplateChild("PART_StackPanel") as StackPanel;

            EbxAsset parentAsset = GetParentEditor().Asset;
            Dictionary<string, FrameworkElement> panels = Enumerable.Range(0, sp.Children.Count).ToDictionary(idx => ((FrameworkElement)sp.Children[idx]).Name, idx => (FrameworkElement)sp.Children[idx]);

            void CompleteConnection(string type, PointerRef pr, string field)
            {
                (string, string, string, string) entityTexts = GetEntity(pr, parentAsset);
                (string, string, string) sanitizedTexts = Sanitize(pr, field, type == "Source");
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
            }
            CompleteConnection("Source", linkConnection.Source, linkConnection.SourceField);
            CompleteConnection("Target", linkConnection.Target, linkConnection.TargetField);
            //entityTexts = GetEntity(linkConnection.Source);
            //(sp.Children[0] as TextBlock).Text = entityTexts.Item1;
            //(sp.Children[2] as TextBlock).Text = Sanitize(linkConnection.Source, linkConnection.SourceField);
            //entityTexts = GetEntity(linkConnection.Target);
            //(sp.Children[4] as TextBlock).Text = entityTexts.Item1;
            //(sp.Children[6] as TextBlock).Text = Sanitize(linkConnection.Target, linkConnection.TargetField);
            //< TextBlock Opacity = "0.5" Text = "" TextTrimming = "CharacterEllipsis" />
            //                < TextBlock Opacity = "0.5" Text = "." />
            //                < TextBlock Text = "" TextTrimming = "CharacterEllipsis" />
            //                < Viewbox Width = "16" Height = "14" Margin = "4 0" VerticalAlignment = "Center" >
            //                    < Path Width = "30" Height = "30" HorizontalAlignment = "Left" VerticalAlignment = "Center" Stretch = "None" Fill = "#FF58B6EB" Opacity = "1" Data = "
            //                          M20 6.9277344C19.871 6.9277344 19.741187 6.9519063 19.617188 7.0039062C19.243188 7.1579062 19 7.5237344 19 7.9277344L19 11.998047L4 11.998047C2.895 11.998047 2 12.893047 2 13.998047L2 15.998047C2 17.103047 2.895 17.998047 4 17.998047L19 17.998047L19 22.070312C19 22.474312 19.243187 22.840141 19.617188 22.994141C19.991187 23.150141 20.420031 23.063344 20.707031 22.777344L27.777344 15.705078C28.168344 15.314078 28.168344 14.683969 27.777344 14.292969L20.707031 7.2207031C20.516031 7.0297031 20.26 6.9277344 20 6.9277344 z
            //                          "/>
            //                </ Viewbox >
            //                < TextBlock Opacity = "0.5" Text = "" TextTrimming = "CharacterEllipsis" />
            //                < TextBlock Opacity = "0.5" Text = "." />
            //                < TextBlock Text = "" TextTrimming = "CharacterEllipsis" />
        }

        protected override (string, string, string) Sanitize(PointerRef pr, string value, bool isSource = false)
        {
            string interfaceIdx = "";
            int idx = 0;
            if (pr.Type == PointerRefType.Internal && TypeLibrary.IsSubClassOf(pr.Internal, "InterfaceDescriptorData"))
            {
                interfaceIdx = "[INVALID]";
                dynamic interfaceDesc = pr.Internal;
                foreach (dynamic field in (isSource ? interfaceDesc.InputLinks : interfaceDesc.OutputLinks))
                {
                    if (field.Name == value)
                    {
                        interfaceIdx = "[" + idx.ToString() + "]";
                        break;
                    }
                    idx++;
                }
            }
            if (value.StartsWith("0x"))
                value = value.Remove(0, 2);
            if (value == "00000000")
                value = "Self";

            return (value, interfaceIdx, "");
        }
    }
}