using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Sdk.AnthemDemo;
using Frosty.Core.Windows;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Microsoft.WindowsAPICodePack.Shell.PropertySystem.SystemProperties.System;
using static System.Windows.Forms.LinkLabel;

namespace EbxToXmlPlugin
{
    public class BulkExporting
    {
        public static string TopLevel = "Tools";

        public static string SubLevel = "Export Bulk";

        public static ImageSource pluginimageSource = new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Database.png") as ImageSource;
        public class EbxToXmlMenuExtension : MenuExtension
        {
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/EbxToXmlPlugin;component/Images/EbxToXml.png") as ImageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "EBX to XML";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting EBX", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".xml";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);

                                EbxAsset asset = App.AssetManager.GetEbx(entry);
                                using (EbxXmlWriter writer = new EbxXmlWriter(new FileStream(fullPath + filename, FileMode.Create), App.AssetManager))
                                    writer.WriteObjects(asset.Objects);
                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported EBX to " + outDir, "Frosty Editor");
                }
            });
        }

        public class EbxToBinMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "EBX to BIN";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting EBX", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".bin";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetEbxStream(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported EBX to " + outDir, "Frosty Editor");
                }
            });
        }

        public class ChunksMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Chunks";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting Chunks", "", (task) =>
                    {
                        uint totalCount = (uint)App.AssetManager.EnumerateChunks().ToList().Count;
                        uint idx = 0;

                        foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".chunk";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Filename);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported chunk to " + outDir, "Frosty Editor");
                }
            });
        }

        public class ResMenuExtension : MenuExtension
        {
            public override ImageSource Icon => pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Res";

            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.InitialDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    string outDir = dialog.FileName;
                    FrostyTaskWindow.Show("Exporting Res", "", (task) =>
                    {
                        uint totalCount = (uint)App.AssetManager.EnumerateRes().ToList().Count;
                        uint idx = 0;

                        foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);

                            string fullPath = outDir + "/" + entry.Path + "/";

                            string filename = entry.Filename + ".res";
                            filename = string.Concat(filename.Split(Path.GetInvalidFileNameChars()));

                            if (File.Exists(fullPath + filename))
                                continue;

                            try
                            {
                                DirectoryInfo di = new DirectoryInfo(fullPath);
                                if (!di.Exists)
                                    Directory.CreateDirectory(di.FullName);
                                using (NativeWriter writer = new NativeWriter(new FileStream(fullPath + filename, FileMode.Create), false, true))
                                {
                                    using (NativeReader reader = new NativeReader(App.AssetManager.GetRes(entry)))
                                        writer.Write(reader.ReadToEnd());
                                }

                            }
                            catch (Exception)
                            {
                                App.Logger.Log("Failed to export {0}", entry.Name);
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported res to " + outDir, "Frosty Editor");
                }
            });
        }

        public class ObjectTypeMenuExtension : MenuExtension
        {
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/EbxToXmlPlugin;component/Images/EbxToXml.png") as ImageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Classes Usage to CSV";
            public static bool HasProperty(object obj, string propertyName)
            {
                return obj.GetType().GetProperty(propertyName) != null;
            }

            public static object forLock = new object();
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                Dictionary<string, (int, int)> classUsage = new Dictionary<string, (int, int)>();
                void AddToList(string typeName, bool isConnection)
                {
                    lock (forLock)
                    {
                        if (classUsage.ContainsKey(typeName))
                        {
                            if (!isConnection)
                                classUsage[typeName] = (classUsage[typeName].Item1 + 1, classUsage[typeName].Item2);
                            else
                                classUsage[typeName] = (classUsage[typeName].Item1, classUsage[typeName].Item2 + 1);
                        }
                        else
                        {
                            if (!isConnection)
                                classUsage[typeName] = (1, 0);
                            else
                                classUsage[typeName] = (0, 1);
                        }

                    }
                }

                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Stat Events", "*.csv (Text File)|*.csv", "StatEvents");

                int forCount = App.AssetManager.EnumerateEbx().ToList().Count;
                int forIdx = 0;
                if (sfd.ShowDialog())
                {
                    FrostyTaskWindow.Show("Exporting Stats", "", (task) =>
                    {
                        Parallel.ForEach(App.AssetManager.EnumerateEbx(), parEntry =>
                        {
                            EbxAsset parAsset = App.AssetManager.GetEbx(parEntry);
                            dynamic refRoot = parAsset.RootObject;

                            Dictionary<EbxAssetEntry, EbxAsset> externalAsset = new Dictionary<EbxAssetEntry, EbxAsset>();
                            void GetTypeOfPR(PointerRef pr)
                            {
                                if (pr.Type == PointerRefType.Internal)
                                    AddToList(pr.Internal.GetType().Name, true);
                                else if(pr.Type == PointerRefType.External)
                                {
                                    EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(pr.External.FileGuid);
                                    if (refEntry == null)
                                        return;
                                    if (!externalAsset.ContainsKey(refEntry))
                                        externalAsset.Add(refEntry, App.AssetManager.GetEbx(refEntry));
                                    dynamic obj = externalAsset[refEntry].GetObject(pr.External.ClassGuid);
                                    if (obj != null)
                                        AddToList(obj.GetType().Name, true);
                                }
                            }

                            foreach (dynamic obj in parAsset.Objects)
                            {
                                AddToList(obj.GetType().Name, false);

                                if (HasProperty(obj, "PropertyConnections"))
                                {
                                    foreach (dynamic PropertyConnection in obj.PropertyConnections)
                                    {
                                        GetTypeOfPR(PropertyConnection.Source);
                                        GetTypeOfPR(PropertyConnection.Target);
                                    }
                                }
                                if (HasProperty(obj, "LinkConnections"))
                                {
                                    foreach (dynamic LinkConnection in obj.LinkConnections)
                                    {
                                        GetTypeOfPR(LinkConnection.Source);
                                        GetTypeOfPR(LinkConnection.Target);
                                    }
                                }
                                if (HasProperty(obj, "EventConnections"))
                                {
                                    foreach (dynamic EventConnection in obj.EventConnections)
                                    {
                                        GetTypeOfPR(EventConnection.Source);
                                        GetTypeOfPR(EventConnection.Target);
                                    }
                                }

                            }

                            lock (forLock)
                            {
                                task.Update(progress: (float)forIdx++ / forCount * 100);
                            }
                        });


                        using (NativeWriter writer = new NativeWriter(new FileStream(sfd.FileName, FileMode.Create), false, true))
                        {
                            writer.WriteLine("Type, Instance Count, Connection Reference Count");
                            foreach (string str in classUsage.Keys)
                            {
                                writer.WriteLine($"{str},{classUsage[str].Item1},{classUsage[str].Item2}");
                            }
                        }
                    });
                }
            });
        }

        public class HashesMenuExtension : MenuExtension
        {
            internal static ImageSource imageSource = pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Hashes List";
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Properties.png") as ImageSource;

            public Dictionary<CString, int> Hashes = new Dictionary<CString, int>();
            public static bool HasProperty(object obj, string propertyName)
            {
                return obj.GetType().GetProperty(propertyName) != null;
            }
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings Usage List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
                if (sfd.ShowDialog())
                {
                    string outDir = sfd.FileName;
                    FrostyTaskWindow.Show("Exporting Hash List", "", (task) =>
                    {
                        int totalCount = App.AssetManager.EnumerateEbx().ToList().Count;
                        int idx = 0;
                        object forLock = new object();
                        Parallel.ForEach(App.AssetManager.EnumerateEbx(), entry =>
                        {
                            EbxAsset refAsset = App.AssetManager.GetEbx(entry);
                            dynamic refRoot = refAsset.RootObject;
                            lock (forLock)
                            {
                                task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);
                                foreach (dynamic obj in refAsset.Objects)
                                {
                                    if (HasProperty(obj, "PropertyConnections"))
                                    {
                                        foreach (dynamic PorpertyConnection in obj.PropertyConnections)
                                        {
                                            foreach (CString Field in new List<dynamic> { PorpertyConnection.SourceField, PorpertyConnection.TargetField })
                                            {
                                                if (!Hashes.ContainsKey(Field))
                                                {
                                                    Hashes.Add(Field, 1);
                                                }
                                                else
                                                {
                                                    Hashes[Field]++;
                                                }
                                            }
                                        }
                                    }
                                    if (HasProperty(obj, "LinkConnections"))
                                    {
                                        foreach (dynamic LinkConnection in obj.LinkConnections)
                                        {
                                            foreach (CString Field in new List<dynamic> { LinkConnection.SourceField, LinkConnection.TargetField })
                                            {
                                                if (!Hashes.ContainsKey(Field))
                                                {
                                                    Hashes.Add(Field, 1);
                                                }
                                                else
                                                {
                                                    Hashes[Field]++;
                                                }
                                            }
                                        }
                                    }
                                    if (HasProperty(obj, "EventConnections"))
                                    {
                                        foreach (dynamic EventConnection in obj.EventConnections)
                                        {
                                            foreach (CString Field in new List<dynamic> { EventConnection.SourceEvent.Name, EventConnection.TargetEvent.Name })
                                            {
                                                if (!Hashes.ContainsKey(Field))
                                                {
                                                    Hashes.Add(Field, 1);
                                                }
                                                else
                                                {
                                                    Hashes[Field]++;
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        });

                        Hashes = Hashes.OrderByDescending(pair => pair.Value).ToDictionary(pair => pair.Key, pair => pair.Value);
                        using (NativeWriter writer = new NativeWriter(new FileStream(outDir, FileMode.Create), false, true))
                        {
                            writer.WriteLine("Hash, Solved Name, Connection count");
                            foreach(KeyValuePair<CString, int> pair in Hashes)
                            {
                                uint hash = (uint)Utils.HashString(pair.Key);
                                bool isKnown = !pair.Key.ToString().StartsWith("0x");
                                writer.WriteLine($"{(!isKnown ? pair.Key.ToString() : "0x" + hash.ToString("X").ToLower())}, {(!isKnown ? "Unknown" : pair.Key.ToString())}, {pair.Value}");
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported Hashes List to " + outDir, "Frosty Editor");
                }
            });
        }
        public class InputsOutputsMenuExtension : MenuExtension
        {
            internal static ImageSource imageSource = pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Object Input/Outputs List";
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Properties.png") as ImageSource;

            public Dictionary<CString, int> Hashes = new Dictionary<CString, int>();
            public static bool HasProperty(object obj, string propertyName)
            {
                return obj.GetType().GetProperty(propertyName) != null;
            }
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Localized Strings Usage List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
                if (sfd.ShowDialog())
                {
                    string outDir = sfd.FileName;
                    FrostyTaskWindow.Show("Exporting Hash List", "", (task) =>
                    {
                        uint totalCount = App.AssetManager.GetEbxCount();
                        uint idx = 0;

                        Dictionary<string, List<string>> typeInputs = new Dictionary<string, List<string>>();
                        Dictionary<string, List<string>> typeOutputs = new Dictionary<string, List<string>>();

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx())
                        {
                            task.Update(entry.Name, (idx++ / (double)totalCount) * 100.0d);
                            //if (entry.Name != "S6_2/Geonosis_02/Levels/Geonosis_02/Mode1")
                            //    continue;
                            if (!TypeLibrary.IsSubClassOf(entry.Type, "Blueprint"))
                                continue;

                            EbxAsset refAsset = App.AssetManager.GetEbx(entry);
                            dynamic refRoot = refAsset.RootObject;

                            Dictionary<EbxAssetEntry, EbxAsset> openedAssets = new Dictionary<EbxAssetEntry, EbxAsset>();

                            void CheckConInputOrOutput(dynamic pr, dynamic field, Dictionary<string, List<string>> dictRef)
                            {
                                string type = null;
                                if (pr.Type == PointerRefType.Internal)
                                {
                                    type = pr.Internal.GetType().Name;
                                }
                                else if (pr.Type == PointerRefType.External)
                                {
                                    EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(pr.External.FileGuid);
                                    if (refEntry == null || refEntry.IsAdded)
                                        return;

                                    if (!openedAssets.ContainsKey(refEntry))
                                        openedAssets.Add(refEntry, App.AssetManager.GetEbx(refEntry, true));
                                    type = openedAssets[refEntry].GetObject(pr.External.ClassGuid).GetType().Name;
                                }
                                else
                                    return;
                                if (!dictRef.ContainsKey(type))
                                    dictRef.Add(type, new List<string>() { field });
                                else if (!dictRef[type].Contains(field))
                                    dictRef[type].Add(field);
                            }

                            foreach (dynamic propCon in refRoot.PropertyConnections)
                            {
                                CheckConInputOrOutput(propCon.Source, propCon.SourceField, typeInputs);
                                CheckConInputOrOutput(propCon.Target, propCon.TargetField, typeOutputs);
                            }
                            foreach (dynamic propCon in refRoot.LinkConnections)
                            {
                                CheckConInputOrOutput(propCon.Source, propCon.SourceField, typeInputs);
                                CheckConInputOrOutput(propCon.Target, propCon.TargetField, typeOutputs);
                            }
                            foreach (dynamic propCon in refRoot.EventConnections)
                            {
                                CheckConInputOrOutput(propCon.Source, propCon.SourceEvent.Name, typeInputs);
                                CheckConInputOrOutput(propCon.Target, propCon.TargetEvent.Name, typeOutputs);
                            }



                        }
                        using (NativeWriter writer = new NativeWriter(new FileStream(outDir, FileMode.Create), false, true))
                        {
                            List<string> types = new List<string>(typeInputs.Keys.ToList());
                            types.AddRange(typeOutputs.Keys.ToList());
                            types = types.Distinct().ToList();
                            types.Sort();
                            foreach (string type in types)
                            {
                                writer.WriteLine(type);
                                if (typeInputs.ContainsKey(type))
                                {
                                    writer.WriteLine(string.Format("\tInputs ({0}): ", typeInputs[type].Count));
                                    typeInputs[type].Sort();
                                    foreach (string input in typeInputs[type])
                                        writer.WriteLine("\t\t" + input);
                                }
                                if (typeOutputs.ContainsKey(type))
                                {
                                    writer.WriteLine(string.Format("\tOutputs ({0}): ", typeOutputs[type].Count));
                                    typeOutputs[type].Sort();
                                    foreach (string input in typeOutputs[type])
                                        writer.WriteLine("\t\t" + input);
                                }
                            }



                            //writer.WriteLine("Unknown Strings:");
                            //foreach (string str in UnknownHashes)
                            //{
                            //    writer.WriteLine(str);
                            //}
                            //writer.WriteLine("\nKnown Strings:");
                            //foreach (string str in KnownHashes)
                            //{
                            //    writer.WriteLine(str);
                            //}
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported Hashes List to " + outDir, "Frosty Editor");
                }
            });
        }
        public class SchematicChannelMenuExtension : MenuExtension
        {
            internal static ImageSource imageSource = pluginimageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Schametics Channel Hash List";
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Properties.png") as ImageSource;

            public Dictionary<string, Dictionary<EbxAssetEntry, int>> hashList = new Dictionary<string, Dictionary<EbxAssetEntry, int>>();

            public static bool HasProperty(object obj, string propertyName)
            {
                return obj.GetType().GetProperty(propertyName) != null;
            }
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save SchematicsChannel Property Hash List", "*.txt (Text File)|*.txt", "LocalizedStringsUsage");
                if (sfd.ShowDialog())
                {
                    string outDir = sfd.FileName;
                    FrostyTaskWindow.Show("Exporting Hash List", "", (task) =>
                    {

                        foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(type: "SchematicChannelAsset"))
                        {

                            EbxAsset refAsset = App.AssetManager.GetEbx(entry);
                            dynamic refRoot = refAsset.RootObject;
                            foreach (dynamic prop in refRoot.Properties)
                            {
                                string propHash = Utils.GetString((int)prop.FieldTypeHash);
                                if (!hashList.ContainsKey(propHash))
                                    hashList.Add(propHash, new Dictionary<EbxAssetEntry, int>() { { entry, 1 } });
                                else if (!hashList[propHash].ContainsKey(entry))
                                    hashList[propHash].Add(entry, 1);
                                else
                                    hashList[propHash][entry]++;

                            }
                        }
                        using (NativeWriter writer = new NativeWriter(new FileStream(outDir, FileMode.Create), false, true))
                        {
                            foreach (string hash in hashList.Keys)
                            {
                                writer.WriteLine(string.Format("Hash: \"{0}\"\nOccurences: {1}\nAssets: {2}", hash, hashList[hash].Sum(x => x.Value), hashList[hash].Count));
                                foreach (KeyValuePair<EbxAssetEntry, int> pair in hashList[hash])
                                    writer.WriteLine(String.Format("\t{0} ({1})", pair.Key.Name, pair.Value));
                            }
                        }
                    });

                    FrostyMessageBox.Show("Successfully exported Hashes List to " + outDir, "Frosty Editor");
                }
            });
        }

        public class StatEventToCSVMenuExtension : MenuExtension
        {
            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/EbxToXmlPlugin;component/Images/EbxToXml.png") as ImageSource;

            public override string TopLevelMenuName => TopLevel;
            public override string SubLevelMenuName => SubLevel;

            public override string MenuItemName => "Stat Events to CSV";

            public class StatCriteriaData
            {
                public string Code;
                public string StatEvent;
                public string ParamX;
                public string ParamY;
            }
            public override RelayCommand MenuItemClicked => new RelayCommand((o) =>
            {
                FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Stat Events", "*.csv (Text File)|*.csv", "StatEvents");
                if (sfd.ShowDialog())
                {
                    FrostyTaskWindow.Show("Exporting Stats", "", (task) =>
                    {
                        AssetManager AM = App.AssetManager;
                        EbxAssetEntry statEntry = AM.GetEbxEntry("Persistence/StatCategories/StatCategories");
                        dynamic statRoot = AM.GetEbx(statEntry).RootObject;

                        Dictionary<Guid, EbxAsset> parmAssets = new Dictionary<Guid, EbxAsset>();
                        //Dictionary<Guid, string> characterParms = new Dictionary<Guid, string>() 
                        //{
                        //    { new Guid("57572638-50ce-4f85-b3d1-f4453b7d2471"), "Jakku" },
                        //    { new Guid("d7ac1754-fa71-4ba0-8544-7fb76f293cc4"), "Kamino" },
                        //    { new Guid("3deede54-2aa1-4569-bef5-6ea40f162aa1"), "Kashyyyk" },
                        //    { new Guid("bffdb8c4-5e8f-4986-9009-f04556169045"), "Naboo" },
                        //    { new Guid("41cd348a-976f-4a79-a0ac-a765168d54fe"), "Starkiller Base" },
                        //    { new Guid("b71a5130-107b-45a7-990b-b793a1b533b6"), "Takodana" },
                        //    { new Guid("06917f2d-a079-4144-81c2-3e318dcfe96b"), "Yavin IV" },
                        //    { new Guid("bdf05f48-a736-4d7b-a94e-76f7444d83dd"), "Tatooine - Mos Eisley" },
                        //    { new Guid("78ae8971-c7a2-4510-b30e-a6053a9066b5"), "Crait" },
                        //    { new Guid("f08f1a3f-82f9-4b78-976e-da53b55f69bd"), "Endor" },
                        //    { new Guid("d14f3c34-7d2c-4e7f-a280-ccf41a3559de"), "Kessel" },
                        //    { new Guid("7aac91db-fa6c-448f-8a9a-0752ac1f322f"), "Hoth" },
                        //    { new Guid("c382b2f5-b5cd-4938-9f36-9f922bfe3390"), "Death Star 2" },
                        //    { new Guid("c45deca9-015e-4423-a8fb-b029cf5b0b3d"), "Bespin" },
                        //    { new Guid("9e41450d-f179-40d1-b17f-83701c245e49"), "Geonosis" },
                        //    { new Guid("788d6b41-26ab-454d-a3f5-7cdfb4026d07"), "All Infantry Maps" },
                        //    { new Guid("0f81c5f2-083b-4069-8e9f-f735c16857b7"), "All Maps" },
                        //    { new Guid("a0254e04-2f95-4ebd-adcc-fbd5a24f8e0a"), "All Starfighter Maps"},
                        //    { new Guid("870aaf4c-1e4a-4919-bd44-9e9fee0e4dd2"), "Starfighter - Ryloth"},
                        //    { new Guid("86f0aa8f-4299-4ce8-8f3f-d149d04d4171"), "Starfighter - Kamino"},
                        //    { new Guid("eea7d0fa-4320-4d6c-8fbc-887f21a71e7b"), "Starfighter - Endor"},
                        //    { new Guid("a364835b-9a98-47ae-88d1-087996b52b84"), "Starfighter - Fondor"},
                        //    { new Guid("6133588e-f46d-4a55-8785-d58d1406c299"), "Starfighter - D'Qar"},
                        //    { new Guid("ec234c0b-ae2b-41d4-b2a1-b5a8fad964f3"), "Starfighter - Unknown Regions"},
                        //    { new Guid("7db61850-bf03-4229-9c3d-48d2563799b5"), "Heroes"},
                        //    { new Guid("16db341b-fc1a-49c5-8832-9c0e79d9d801"), "Villains"},
                        //    { new Guid("6cebddfb-556b-4b06-ba9c-1b6abe53d36c"), "Heroes & Villains"},
                        //    { new Guid("ac2c1500-ec76-4901-b9ab-be0945567651"), "Weapons"},
                        //};

                        //dynamic classRankData = AM.GetEbx(AM.GetEbxEntry("Persistence/Ranks/ClassRankData")).RootObject;

                        //foreach (dynamic classList in classRankData.ClassRankInfos)
                        //{
                        //    foreach (dynamic subclassExt in classList.Internal.ClassRanks)
                        //    {
                        //        dynamic subclass = subclassExt.Internal;
                        //        Guid statGuid = subclass.StatCategory.External.FileGuid;
                        //        if (!parmAssets.ContainsKey(statGuid))
                        //            parmAssets.Add(statGuid, AM.GetEbx(AM.GetEbxEntry(statGuid)));
                        //        dynamic statObj = parmAssets[statGuid].GetObject(subclass.StatCategory.External.ClassGuid);
                        //        characterParms.Add(subclass.StatCategory.External.ClassGuid, subclass.CharacterClass.ToString().Substring(8));
                        //    }
                        //}
                        List<StatCriteriaData> statsList = new List<StatCriteriaData>();
                        foreach (dynamic statExt in statRoot.GeneralStatisticsCriterias)
                        {
                            StatCriteriaData statData = new StatCriteriaData() { ParamX = "", ParamY = "" };
                            dynamic stat = statExt.Internal;
                            statData.Code = stat.Code;
                            statData.StatEvent = stat.StatEvent.ToString();

                            string GetParamName(PointerRef parmRef)
                            {
                                if (parmRef.Type == PointerRefType.External)
                                {
                                    Guid yGuid = parmRef.External.FileGuid;
                                    if (!parmAssets.ContainsKey(yGuid))
                                        parmAssets.Add(yGuid, AM.GetEbx(AM.GetEbxEntry(yGuid)));
                                    Guid yClassGuid = parmRef.External.ClassGuid;
                                    //if (characterParms.ContainsKey(yClassGuid))
                                    //    return characterParms[yClassGuid];
                                    //else
                                    return AM.GetEbx(AM.GetEbxEntry(yGuid)).GetObject(yClassGuid).Code;
                                }
                                return "";
                            }

                            statData.ParamY = GetParamName(stat.ParamY);
                            if (stat.ParamX.Count == 1)
                                statData.ParamX = GetParamName(stat.ParamX[0]);
                            else if (stat.ParamX.Count > 1)
                                throw new Exception();

                            statsList.Add(statData);
                        }

                        using (NativeWriter writer = new NativeWriter(new FileStream(sfd.FileName, FileMode.Create), false, true))
                        {
                            writer.WriteLine("Code, StatEvent, ParamX, ParamY");
                            foreach (StatCriteriaData stat in statsList)
                            {
                                writer.WriteLine(String.Format("{0},{1},{2},{3}", stat.Code, stat.StatEvent, stat.ParamX, stat.ParamY));

                            }
                        }
                    }
                    );
                }
            });
        }
    }
}
