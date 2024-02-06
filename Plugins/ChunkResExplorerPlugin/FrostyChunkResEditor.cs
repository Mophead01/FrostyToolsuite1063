using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Frosty.Core.Controls;
using Frosty.Core.Windows;
using Frosty.Core;
using System.Windows.Media;
using FrostySdk;
using Frosty.Hash;
using FrostySdk.Attributes;
using static ChunkResEditorPlugin.FrostyChunkResEditor;
using static ChunkResEditorPlugin.FrostyShaderBlockViewer;
using System.Collections.Generic;
using System.Reflection;
using MeshSetPlugin.Resources;
using RootInstanceEntiresPlugin;

namespace ChunkResEditorPlugin
{
    public class FrostyChunkExportCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67

        public void Execute(object parameter)
        {
            if (parameter is FrameworkElement param && param.Tag is FrostyChunkResEditor explorer)
            {
                explorer.ExportChunk();
            }
        }
    }
    public class FrostyChunkImportCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67

        public void Execute(object parameter)
        {
            if (parameter is FrameworkElement param && param.Tag is FrostyChunkResEditor explorer)
            {
                explorer.ImportChunk();
            }
        }
    }
    public class FrostyChunkRevertCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67

        public void Execute(object parameter)
        {
            if (parameter is FrameworkElement param && param.Tag is FrostyChunkResEditor explorer)
            {
                explorer.RevertChunk();
            }
        }
    }
    public class FrostyChunkRightClickCommand : ICommand
    {
        public bool CanExecute(object parameter)
        {
            return true;
        }

#pragma warning disable 67
        public event EventHandler CanExecuteChanged;
#pragma warning restore 67

        public void Execute(object parameter)
        {
            if (parameter is ListBoxItem lbi) 
                lbi.IsSelected = true;
        }
    }

    [TemplatePart(Name = PART_ChunksListBox, Type = typeof(ListBox))]
    [TemplatePart(Name = PART_ChunksBundlesBox, Type = typeof(ListBox))]
    [TemplatePart(Name = PART_ResExplorer, Type = typeof(FrostyDataExplorer))]
    [TemplatePart(Name = PART_ResBundlesBox, Type = typeof(ListBox))]
    [TemplatePart(Name = PART_ResExportMenuItem, Type = typeof(MenuItem))]
    [TemplatePart(Name = PART_ResImportMenuItem, Type = typeof(MenuItem))]
    [TemplatePart(Name = PART_RevertMenuItem, Type = typeof(MenuItem))]
    [TemplatePart(Name = PART_ChunkFilter, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_ChunkModified, Type = typeof(CheckBox))]
    public class FrostyChunkResEditor : FrostyBaseEditor
    {
        public override ImageSource Icon => ChunkResEditorMenuExtension.imageSource;

        private const string PART_ChunksListBox = "PART_ChunksListBox";
        private const string PART_ChunksBundlesBox = "PART_ChunksBundlesBox";
        private const string PART_ResExplorer = "PART_ResExplorer";
        private const string PART_ResBundlesBox = "PART_ResBundlesBox";
        private const string PART_ResExportMenuItem = "PART_ResExportMenuItem";
        private const string PART_ResImportMenuItem = "PART_ResImportMenuItem";
        private const string PART_RevertMenuItem = "PART_RevertMenuItem";
        private const string PART_ChunkFilter = "PART_ChunkFilter";
        private const string PART_ChunkModified = "PART_ChunkModified";

        private ListBox chunksListBox;
        private ListBox chunksBundleBox;
        private FrostyDataExplorer resExplorer;
        private ListBox resBundleBox;
        private TextBox chunkFilterTextBox;
        private CheckBox chunkModifiedBox;
        private ILogger logger;

        static FrostyChunkResEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyChunkResEditor), new FrameworkPropertyMetadata(typeof(FrostyChunkResEditor)));
        }

        public FrostyChunkResEditor(ILogger inLogger = null)
        {
            logger = inLogger;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            chunksBundleBox = GetTemplateChild(PART_ChunksBundlesBox) as ListBox;
            resBundleBox = GetTemplateChild(PART_ResBundlesBox) as ListBox;
            chunksListBox = GetTemplateChild(PART_ChunksListBox) as ListBox;
            resExplorer = GetTemplateChild(PART_ResExplorer) as FrostyDataExplorer;
            chunkFilterTextBox = GetTemplateChild(PART_ChunkFilter) as TextBox;
            chunkModifiedBox = GetTemplateChild(PART_ChunkModified) as CheckBox;

            resExplorer.SelectionChanged += ResExplorer_SelectionChanged;
            MenuItem mi = GetTemplateChild(PART_ResExportMenuItem) as MenuItem;
            mi.Click += ResExportMenuItem_Click;

            mi = GetTemplateChild(PART_ResImportMenuItem) as MenuItem;
            mi.Click += ResImportMenuItem_Click;

            mi = GetTemplateChild(PART_RevertMenuItem) as MenuItem;
            mi.Click += ResRevertMenuItem_Click;

            Loaded += FrostyChunkResEditor_Loaded;
            chunksListBox.SelectionChanged += ChunksListBox_SelectionChanged;
            chunkFilterTextBox.LostFocus += ChunkFilterTextBox_LostFocus;
            chunkFilterTextBox.KeyUp += ChunkFilterTextBox_KeyUp;
            chunkModifiedBox.Checked += ChunkFilterTextBox_LostFocus;
            chunkModifiedBox.Unchecked += ChunkFilterTextBox_LostFocus;
            resExplorer.SelectedAssetDoubleClick += ResExplorer_SelectedAssetDoubleClick;
        }

        public void ResExplorer_SelectedAssetDoubleClick(object sender, RoutedEventArgs e)
        {
            FrostyTaskWindow.Show("Getting ShaderBlockDepot Info", "", (task) =>
            {
                if (!RootInstanceEbxEntryDb.IsLoaded)
                    RootInstanceEbxEntryDb.LoadEbxRootInstanceEntries(task);
                if (!HashCache.IsLoaded)
                    HashCache.LoadHashCache(task);
            });

            string type = resExplorer.SelectedAsset.Type;
            if (type == "ShaderBlockDepot")
            {
                ResAssetEntry resEntry = resExplorer.SelectedAsset as ResAssetEntry;
                App.EditorWindow.OpenEditor(resEntry.DisplayName, new FrostyShaderBlockViewer(resEntry, logger));
            }
        }

        private void ResExplorer_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (resExplorer.SelectedAsset != null)
            {
                resBundleBox.Items.Clear();
                ResAssetEntry SelectedRes = (ResAssetEntry)resExplorer.SelectedAsset;
                resBundleBox.Items.Add("Selected resource is in Bundles: ");
                foreach (int bundle in SelectedRes.Bundles)
                {
                    resBundleBox.Items.Add(App.AssetManager.GetBundleEntry(bundle).Name);
                }
                if (SelectedRes.AddedBundles.Count != 0)
                {
                    resBundleBox.Items.Add("Added to Bundles:");
                    foreach (int bundle in SelectedRes.AddedBundles)
                    {
                        resBundleBox.Items.Add(App.AssetManager.GetBundleEntry(bundle).Name);
                    }
                }
            }
            else
            {
                resBundleBox.Items.Clear();
                resBundleBox.Items.Add("No res selected");
            }
        }

        private void ChunksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (chunksListBox.SelectedIndex != -1)
            {
                chunksBundleBox.Items.Clear();
                ChunkAssetEntry SelectedChk = (ChunkAssetEntry)chunksListBox.SelectedItem;
                string FirstLine = "Selected chunk is in Bundles: ";
                if (SelectedChk.FirstMip != -1)
                    FirstLine += " (FirstMip:" + SelectedChk.FirstMip + ")";
                if (App.FileSystem.GetManifestChunk(SelectedChk.Id) != null)
                {
                        chunksBundleBox.Items.Add("Selected chunk is a Manifest chunk.");
                }
                else if (SelectedChk.Bundles.Count == 0 && SelectedChk.AddedBundles.Count == 0)
                {
                    chunksBundleBox.Items.Add("Selected chunk is only in SuperBundles.");
                }
                if (SelectedChk.Bundles.Count != 0)
                {
                    chunksBundleBox.Items.Add(FirstLine);
                    foreach (int bundle in SelectedChk.Bundles)
                    {
                        chunksBundleBox.Items.Add(App.AssetManager.GetBundleEntry(bundle).Name);
                    }
                }
                if (SelectedChk.AddedBundles.Count != 0)
                {
                    chunksBundleBox.Items.Add("Added to Bundles:");
                    foreach (int bundle in SelectedChk.AddedBundles)
                    {
                        chunksBundleBox.Items.Add(App.AssetManager.GetBundleEntry(bundle).Name);
                    }
                }
            }
            else
            {
                chunksBundleBox.Items.Clear();
                chunksBundleBox.Items.Add("No chunk selected");
            }
        }

        private void ChunkFilterTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                UpdateFilter();
        }

        private void ChunkFilterTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            if (chunkFilterTextBox.Text == "" & chunkModifiedBox.IsChecked == false)
            {
                chunksListBox.Items.Filter = null;
                return;
            }
            else if (chunkFilterTextBox.Text != "" & chunkModifiedBox.IsChecked == false)
            {
                chunksListBox.Items.Filter = new Predicate<object>((object a) => ((ChunkAssetEntry)a).Id.ToString().Contains(chunkFilterTextBox.Text.ToLower()));
            }
            else if (chunkFilterTextBox.Text == "" & chunkModifiedBox.IsChecked == true)
            {
                chunksListBox.Items.Filter = new Predicate<object>((object a) => ((ChunkAssetEntry)a).IsModified);
            }
            else if (chunkFilterTextBox.Text != "" & chunkModifiedBox.IsChecked == true)
            {
                chunksListBox.Items.Filter = new Predicate<object>((object a) => (((ChunkAssetEntry)a).IsModified) & ((ChunkAssetEntry)a).Id.ToString().Contains(chunkFilterTextBox.Text.ToLower()));
            }
        }

        private void ResRevertMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!(resExplorer.SelectedAsset is ResAssetEntry selectedAsset) || !selectedAsset.IsModified)
                return;

            FrostyTaskWindow.Show("Reverting Asset", "", (task) => { App.AssetManager.RevertAsset(selectedAsset, suppressOnModify: false); });
            resExplorer.RefreshItems();
        }

        private void ResImportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ResAssetEntry selectedAsset = resExplorer.SelectedAsset as ResAssetEntry;
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Open Resource", "*.res (Resource Files)|*.res", "Res");

            if (ofd.ShowDialog())
            {
                FrostyTaskWindow.Show("Importing Asset", "", (task) =>
                {
                    using (NativeReader reader = new NativeReader(new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read)))
                    {
                        byte[] resMeta = reader.ReadBytes(0x10);
                        byte[] buffer = reader.ReadToEnd();

                        if (App.PluginManager.GetCustomHandler((ResourceType)selectedAsset.ResType) != null)
                        {
                            // @todo: throw some kind of error
                        }

                        // @todo
                        //if (selectedAsset.ResType == (uint)ResourceType.ShaderBlockDepot)
                        //{
                        //    // treat manually imported shaderblocks as if every block has been modified

                        //    ShaderBlockDepot sbd = new ShaderBlockDepot(resMeta);
                        //    using (NativeReader subReader = new NativeReader(new MemoryStream(buffer)))
                        //        sbd.Read(subReader, App.AssetManager, selectedAsset, null);

                        //    for (int j = 0; j < sbd.ResourceCount; j++)
                        //    {
                        //        var sbr = sbd.GetResource(j);
                        //        if (sbr is ShaderPersistentParamDbBlock || sbr is MeshParamDbBlock)
                        //            sbr.IsModified = true;
                        //    }

                        //    App.AssetManager.ModifyRes(selectedAsset.Name, sbd);
                        //}

                        //else
                        {
                            App.AssetManager.ModifyRes(selectedAsset.Name, buffer, resMeta);
                        }
                    }
                });
                resExplorer.RefreshItems();
            }
        }

        private void ResExportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ResAssetEntry selectedAsset = resExplorer.SelectedAsset as ResAssetEntry;
            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Resource", "*.res (Resource Files)|*.res", "Res", selectedAsset.Filename);

            Stream resStream = App.AssetManager.GetRes(selectedAsset);
            if (resStream == null)
                return;

            if (sfd.ShowDialog())
            {
                FrostyTaskWindow.Show("Exporting Asset", "", (task) =>
                {
                    using (NativeWriter writer = new NativeWriter(new FileStream(sfd.FileName, FileMode.Create)))
                    {
                        // write res meta first
                        writer.Write(selectedAsset.ResMeta);

                        // followed by remaining data
                        using (NativeReader reader = new NativeReader(resStream))
                            writer.Write(reader.ReadToEnd());
                    }
                });
                logger?.Log("Resource saved to {0}", sfd.FileName);
            }
        }

        public void ImportChunk()
        {
            ChunkAssetEntry selectedAsset = chunksListBox.SelectedItem as ChunkAssetEntry;
            FrostyOpenFileDialog ofd = new FrostyOpenFileDialog("Open Chunk", "*.chunk (Chunk Files)|*.chunk", "Chunk");

            if (ofd.ShowDialog())
            {
                FrostyTaskWindow.Show("Importing Chunk", "", (task) =>
                {
                    using (NativeReader reader = new NativeReader(new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read)))
                    {
                        byte[] buffer = reader.ReadToEnd();
                        App.AssetManager.ModifyChunk(selectedAsset.Id, buffer);
                    }
                });
                RefreshChunksListBox(selectedAsset);
            }
        }

        public void ExportChunk()
        {
            ChunkAssetEntry selectedAsset = chunksListBox.SelectedItem as ChunkAssetEntry;
            FrostySaveFileDialog sfd = new FrostySaveFileDialog("Save Chunk", "*.chunk (Chunk Files)|*.chunk", "Chunk", selectedAsset.Filename);

            Stream chunkStream = App.AssetManager.GetChunk(selectedAsset);
            if (chunkStream == null)
                return;

            if (sfd.ShowDialog())
            {
                FrostyTaskWindow.Show("Exporting Chunk", "", (task) =>
                {
                    using (NativeWriter writer = new NativeWriter(new FileStream(sfd.FileName, FileMode.Create)))
                    {
                        using (NativeReader reader = new NativeReader(chunkStream))
                            writer.Write(reader.ReadToEnd());
                    }
                });
                logger?.Log("Chunk saved to {0}", sfd.FileName);
            }
        }

        public void RevertChunk()
        {
            ChunkAssetEntry selectedAsset = chunksListBox.SelectedItem as ChunkAssetEntry;
            if (selectedAsset == null || !selectedAsset.IsModified)
                return;

            FrostyTaskWindow.Show("Reverting Chunk", "", (task) => { App.AssetManager.RevertAsset(selectedAsset, suppressOnModify: false); });
            RefreshChunksListBox(selectedAsset);
        }

        private void FrostyChunkResEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (resExplorer.ItemsSource != null)
                return;

            resExplorer.ItemsSource = App.AssetManager.EnumerateRes();
            chunksListBox.ItemsSource = App.AssetManager.EnumerateChunks();
            chunksListBox.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription("DisplayName", System.ComponentModel.ListSortDirection.Ascending));
        }

        private void RefreshChunksListBox(ChunkAssetEntry selectedAsset)
        {
            chunksListBox.Items.Refresh();
        }
    }


    [DisplayName("ShaderBlockDepot")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class ShaderBlockDepot
    {
        [Category("Annotations")]
        [IsReadOnly]
        [EbxFieldMeta(EbxFieldType.String)]
        public string ResourceId { get; set; }
        [Category("Annotations")]
        [IsReadOnly]
        [EbxFieldMeta(EbxFieldType.String)]
        public string Guid { get; set; }
        [Category("Annotations")]
        [IsReadOnly]
        [EbxFieldMeta(EbxFieldType.String)]
        public string ResMeta { get; set; }
        [Category("Annotations")]
        [IsReadOnly]
        [EbxFieldMeta(EbxFieldType.String)]
        public string Name { get; set; }
        [Category("Testing")]
        [IsReadOnly]
        [EbxFieldMeta(EbxFieldType.Int32)]
        public int ResourceCount { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<ShaderEntry> ShaderBlockEntries { get; set; } = new List<ShaderEntry>();
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<MeshVariationBlock> MeshVariationDbBlocks { get; set; } = new List<MeshVariationBlock>();
    }

    [DisplayName("ShaderBlockResource")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class BlockResource
    {

    }

    [DisplayName("ShaderStaticParamDbBlock")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class ShaderStaticParamBlock : BlockResource
    {
        [EbxFieldMeta(EbxFieldType.String)]
        public string NameHash { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<BlockResource> ShaderBlockResources { get; set; } = new List<BlockResource>();
    }

    [DisplayName("ShaderPersistentParamDbBlock")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class ShaderPersistentParamBlock : BlockResource
    {
        [EbxFieldMeta(EbxFieldType.String)]
        public string NameHash { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<Parameters> Parameters { get; set; } = new List<Parameters>();
    }

    [DisplayName("ShaderBlockParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Parameters
    {

    }

    [DisplayName("TextureParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Textures : Parameters
    {
        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.String)]
        public string Value { get; set; }
    }
    [DisplayName("ConditionalParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Conditional : Parameters
    {
        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.Int16)]
        public byte Index { get; set; }
    }
    [DisplayName("BoolParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Bools : Parameters
    {
        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.Boolean)]
        public bool Value { get; set; }
    }
    [DisplayName("FloatParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Floats : Parameters
    {
        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.Array)]
        public float[] Value { get; set; } = new float[4];
    }
    [DisplayName("IntParameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Int32s : Parameters
    {
        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.UInt32)]
        public uint Value { get; set; }
    }

    [DisplayName("Int64Parameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Int64s : Parameters
    {

        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.Int64)]
        public long Value { get; set; }
    }
    [DisplayName("Float32Parameters")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class Float32 : Parameters
    {

        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.Float32)]
        public float Value { get; set; }
    }
    [DisplayName("Unknown")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class UnknownParam : Parameters
    {

        [EbxFieldMeta(EbxFieldType.CString)]
        public string ParamName { get; set; }
        [EbxFieldMeta(EbxFieldType.CString)]
        public string TypeName { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public object Value { get; set; }
    }

    [DisplayName("MeshParamDbBlock")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class MeshParamBlock : BlockResource
    {
        [EbxFieldMeta(EbxFieldType.String)]
        public string NameHash { get; set; }
        [EbxFieldMeta(EbxFieldType.Guid)]
        public Guid MeshAssetGuid { get; set; }
        [EbxFieldMeta(EbxFieldType.UInt32)]
        public int LodIndex { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<Parameters> Parameters { get; set; } = new List<Parameters>();
    }

    [DisplayName("MeshVariationDbBlock")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class MeshVariationBlock : BlockResource
    {
        [EbxFieldMeta(EbxFieldType.String)]
        public string NameHash { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<MeshParamDbBlock> MeshParamDbBlocks { get; set; } = new List<MeshParamDbBlock>();
    }

    [DisplayName("MeshVariationDbBlock")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class MeshParamDbBlock
    {
        [EbxFieldMeta(EbxFieldType.Guid)]
        public Guid MeshAssetGuid { get; set; }
        [EbxFieldMeta(EbxFieldType.UInt32)]
        public uint LodIndex { get; set; }
    }

    [DisplayName("ShaderBlockEntry")]
    [EbxClassMeta(EbxFieldType.Struct)]
    public class ShaderEntry : BlockResource
    {
        [EbxFieldMeta(EbxFieldType.String)]
        public string NameHash { get; set; }
        [EbxFieldMeta(EbxFieldType.Struct)]
        public List<dynamic> ShaderStaticParamDbBlocks { get; set; } = new List<dynamic>();

    }
    [TemplatePart(Name = PART_ShaderBlockDepot, Type = typeof(FrostyPropertyGrid))]
    public class FrostyShaderBlockViewer : FrostyBaseEditor
    {
        private const string PART_ShaderBlockDepot = "PART_ShaderBlockDepot";
        private FrostyPropertyGrid pgShaderBlockDepot;
        private ResAssetEntry shaderblockEntry;
        private ShaderBlockDepot blockDepot = new ShaderBlockDepot();
        private ILogger logger;
        private bool firstTime = true;
        static FrostyShaderBlockViewer()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FrostyShaderBlockViewer), new FrameworkPropertyMetadata(typeof(FrostyShaderBlockViewer)));
        }
        public FrostyShaderBlockViewer(ResAssetEntry resEntry, ILogger inlogger = null)
        {
            shaderblockEntry = resEntry;
            logger = inlogger;
        }
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            pgShaderBlockDepot = GetTemplateChild(PART_ShaderBlockDepot) as FrostyPropertyGrid;
            Loaded += FrostyShaderBlockViewer_Loaded;
        }

        private void FrostyShaderBlockViewer_Loaded(object sender, RoutedEventArgs e)
        {
            //if (!File.Exists("../FrostyEditor/test.txt"))
            //{
                //List<string> filecontent = new List<string>();
                //string error = "";
                //Dictionary<uint, List<string>> u1dic = new Dictionary<uint, List<string>>();
                //Dictionary<ulong, List<string>> u2dic = new Dictionary<ulong, List<string>>();
                //foreach (ResAssetEntry res in App.AssetManager.EnumerateRes(resType: (uint)Fnv1.HashString("shaderblockdepot")))
                //{
                //    using (NativeReader reader = new NativeReader(new MemoryStream(res.ResMeta)))
                //    {
                //        uint type = reader.ReadUInt();
                //        if (type != 0x5B06000A)
                //            error += res.Name + ": " + type.ToString("X4");
                //        uint u1 = reader.ReadUInt();
                //        //error += u1.ToString() + ", ";
                //        if (!u1dic.ContainsKey(u1))
                //            u1dic.Add(u1, new List<string>());
                //        u1dic[u1].Add(res.Name);
                //        uint u2 = reader.ReadUInt();
                //        uint u3 = reader.ReadUInt();
                //        if (u2 / 8 != u3 || u2 % u3 != 0)
                //            error += "Name: " + res.Name + " ";

                //    }
                //}
                //filecontent.Add(error);
                //filecontent.Add("u1: " + u1dic.Count.ToString());
                //foreach (KeyValuePair<uint, List<string>> kv in u1dic)
                //{
                //    bool graph = false;
                //    bool mesh = false;
                //    filecontent.Add(kv.Key.ToString("X4") + " Count: " + kv.Value.Count.ToString());
                //    foreach (string s in kv.Value)
                //    {
                //        filecontent.Add("    -" + s);
                //        if (s.Contains("graph/blocks"))
                //            graph = true;
                //        if (s.Contains("mesh/blocks"))
                //            mesh = true;
                //    }
                //    filecontent.Add("Graph: " + graph.ToString() + " Mesh: " + mesh.ToString());
                //}
                //File.WriteAllLines("../FrostyEditor/test.txt", filecontent.ToArray());
                //List<string> types = new List<string>();
                //foreach (ResAssetEntry res in App.AssetManager.EnumerateRes())
                //{
                //    if (!types.Contains(res.Type))
                //        types.Add(res.Type);
                //}
                //File.WriteAllLines("../FrostyEditor/test.txt", types.ToArray());
            //}
            if (firstTime)
            {
                MeshSetPlugin.Resources.ShaderBlockDepot shaderBlockDepot = App.AssetManager.GetResAs<MeshSetPlugin.Resources.ShaderBlockDepot>(shaderblockEntry);
                blockDepot = new ShaderBlockDepot();
                Dictionary<string, MeshVariationBlock> variationBlocks = new Dictionary<string, MeshVariationBlock>();
                for (int i = 0; i < shaderBlockDepot.ResourceCount; i++)
                {
                    ShaderBlockResource resource = shaderBlockDepot.GetResource(i);

                    if (resource is ShaderBlockEntry)
                    {
                        ShaderBlockEntry shaderBlockEntry = resource as ShaderBlockEntry;
                        ShaderEntry shaderEntry = new ShaderEntry();
                        shaderEntry.NameHash = shaderBlockEntry.Hash.ToString("X");
                        foreach (var block in shaderBlockEntry.Resources)
                        {
                            if (block is ShaderStaticParamDbBlock)
                            {
                                ShaderStaticParamBlock staticParamBlock = new ShaderStaticParamBlock();
                                ShaderStaticParamDbBlock shaderStaticParamDbBlock = block as ShaderStaticParamDbBlock;
                                staticParamBlock.NameHash = shaderStaticParamDbBlock.Hash.ToString("X");

                                foreach (var res in shaderStaticParamDbBlock.Resources)
                                {
                                    if (res is ShaderStaticParamDbBlock)
                                    {
                                        ShaderStaticParamBlock staticParamBlock2 = new ShaderStaticParamBlock();
                                        ShaderStaticParamDbBlock shaderStaticParamDbBlock2 = res as ShaderStaticParamDbBlock;
                                        staticParamBlock2.NameHash = shaderStaticParamDbBlock2.Hash.ToString("X");

                                        foreach (var res2 in shaderStaticParamDbBlock2.Resources)
                                        {
                                            if (res2 is ShaderPersistentParamDbBlock)
                                            {
                                                ShaderPersistentParamDbBlock persistentParamDbBlock = res2 as ShaderPersistentParamDbBlock;
                                                ShaderPersistentParamBlock shaderPersistentParam = new ShaderPersistentParamBlock();
                                                shaderPersistentParam.NameHash = persistentParamDbBlock.Hash.ToString("X");
                                                foreach (ParameterEntry param in persistentParamDbBlock.Parameters)
                                                {
                                                    if (param.TypeHash == 0x9638b221)
                                                    {
                                                        Bools bools = new Bools();
                                                        bools.ParamName = HashCache.GetHashString(param.NameHash);
                                                        bools.Value = (bool)param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(bools);
                                                    }

                                                    else if (param.TypeHash == 0x0b87fa95)
                                                    {
                                                        Floats floats = new Floats();
                                                        floats.ParamName = HashCache.GetHashString(param.NameHash);
                                                        floats.Value = (float[])param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(floats);
                                                    }

                                                    else if (param.TypeHash == 0xb0bc3c22)
                                                    {
                                                        Int32s int32 = new Int32s();
                                                        int32.ParamName = HashCache.GetHashString(param.NameHash);
                                                        int32.Value = (uint)param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(int32);
                                                    }

                                                    else if (param.TypeHash == 0xad0abfd3)
                                                    {
                                                        Guid guid = (Guid)param.GetValue();
                                                        EbxAssetEntry assetEntry = RootInstanceEbxEntryDb.GetEbxEntryByRootInstanceGuid(guid);
                                                        Guid fileGuid = assetEntry.Guid;
                                                        string item = assetEntry.Name;
                                                        Textures textures = new Textures();
                                                        textures.ParamName = HashCache.GetHashString(param.NameHash);
                                                        textures.Value = item;
                                                        shaderPersistentParam.Parameters.Add(textures);
                                                    }

                                                    else if (param.TypeHash == 0x0d1cfa1b)
                                                    {
                                                        Conditional conditional = new Conditional();
                                                        conditional.ParamName = HashCache.GetHashString(param.NameHash);
                                                        conditional.Index = (byte)param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(conditional);
                                                    }

                                                    else if (param.TypeHash == 0x7f39a7b4)
                                                    {
                                                        Float32 float32 = new Float32();
                                                        float32.ParamName = HashCache.GetHashString(param.NameHash);
                                                        float32.Value = (float)param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(float32);
                                                    }

                                                    else
                                                    {
                                                        UnknownParam u = new UnknownParam();
                                                        u.ParamName = HashCache.GetHashString(param.NameHash);
                                                        u.TypeName = TypeLibrary.GetType(param.TypeHash).Name;
                                                        u.Value = param.GetValue();
                                                        shaderPersistentParam.Parameters.Add(u);
                                                    }
                                                }
                                                staticParamBlock2.ShaderBlockResources.Add(shaderPersistentParam);
                                            }
                                            else if (res2 is MeshSetPlugin.Resources.MeshParamDbBlock)
                                            {
                                                MeshSetPlugin.Resources.MeshParamDbBlock meshParamDbBlock = res2 as MeshSetPlugin.Resources.MeshParamDbBlock;
                                                MeshParamBlock paramBlock = new MeshParamBlock();
                                                paramBlock.NameHash = meshParamDbBlock.Hash.ToString("X");
                                                paramBlock.LodIndex = meshParamDbBlock.LodIndex;
                                                paramBlock.MeshAssetGuid = meshParamDbBlock.MeshAssetGuid;
                                                foreach (ParameterEntry param in meshParamDbBlock.Parameters)
                                                {
                                                    if (param.TypeHash == 0xb0bc3c22)
                                                    {
                                                        Int32s int32 = new Int32s();
                                                        int32.ParamName = HashCache.GetHashString(param.NameHash);
                                                        int32.Value = (uint)param.GetValue();
                                                        paramBlock.Parameters.Add(int32);
                                                    }
                                                    else if (param.TypeHash == 0x0d1cfa1b)
                                                    {
                                                        Conditional conditional = new Conditional();
                                                        conditional.ParamName = HashCache.GetHashString(param.NameHash);
                                                        conditional.Index = (byte)param.GetValue();
                                                        paramBlock.Parameters.Add(conditional);
                                                    }
                                                    else if (param.TypeHash == 0x7f39a7b4)
                                                    {
                                                        Float32 float32 = new Float32();
                                                        float32.ParamName = HashCache.GetHashString(param.NameHash);
                                                        float32.Value = (float)param.GetValue();
                                                        paramBlock.Parameters.Add(float32);
                                                    }
                                                    else
                                                    {
                                                        UnknownParam u = new UnknownParam();
                                                        u.ParamName = HashCache.GetHashString(param.NameHash);
                                                        u.TypeName = TypeLibrary.GetType(param.TypeHash).Name;
                                                        u.Value = param.GetValue();
                                                        paramBlock.Parameters.Add(u);
                                                    }
                                                }
                                                staticParamBlock2.ShaderBlockResources.Add(paramBlock);
                                            }
                                        }
                                        staticParamBlock.ShaderBlockResources.Add(staticParamBlock2);
                                    }
                                    else if (res is ShaderPersistentParamDbBlock)
                                    {
                                        ShaderPersistentParamDbBlock persistentParamDbBlock = res as ShaderPersistentParamDbBlock;
                                        ShaderPersistentParamBlock shaderPersistentParam = new ShaderPersistentParamBlock();
                                        shaderPersistentParam.NameHash = persistentParamDbBlock.Hash.ToString("X");
                                        foreach (ParameterEntry param in persistentParamDbBlock.Parameters)
                                        {
                                            if (param.TypeHash == 0x9638b221)
                                            {
                                                Bools bools = new Bools();
                                                bools.ParamName = HashCache.GetHashString(param.NameHash);
                                                bools.Value = (bool)param.GetValue();
                                                shaderPersistentParam.Parameters.Add(bools);
                                            }

                                            else if (param.TypeHash == 0x0b87fa95)
                                            {
                                                Floats floats = new Floats();
                                                floats.ParamName = HashCache.GetHashString(param.NameHash);
                                                floats.Value = (float[])param.GetValue();
                                                shaderPersistentParam.Parameters.Add(floats);
                                            }

                                            else if (param.TypeHash == 0xb0bc3c22)
                                            {
                                                Int32s int32 = new Int32s();
                                                int32.ParamName = HashCache.GetHashString(param.NameHash);
                                                int32.Value = (uint)param.GetValue();
                                                shaderPersistentParam.Parameters.Add(int32);
                                            }

                                            else if (param.TypeHash == 0xad0abfd3)
                                            {
                                                Guid guid = (Guid)param.GetValue();
                                                EbxAssetEntry assetEntry = RootInstanceEbxEntryDb.GetEbxEntryByRootInstanceGuid(guid);
                                                Guid fileGuid = assetEntry.Guid;
                                                string item = assetEntry.Name;
                                                Textures textures = new Textures();
                                                textures.ParamName = HashCache.GetHashString(param.NameHash);
                                                textures.Value = item;
                                                shaderPersistentParam.Parameters.Add(textures);
                                            }

                                            else if (param.TypeHash == 0x0d1cfa1b)
                                            {
                                                Conditional conditional = new Conditional();
                                                conditional.ParamName = HashCache.GetHashString(param.NameHash);
                                                conditional.Index = (byte)param.GetValue();
                                                shaderPersistentParam.Parameters.Add(conditional);
                                            }

                                            else if (param.TypeHash == 0x7f39a7b4)
                                            {
                                                Float32 float32 = new Float32();
                                                float32.ParamName = HashCache.GetHashString(param.NameHash);
                                                float32.Value = (float)param.GetValue();
                                                shaderPersistentParam.Parameters.Add(float32);
                                            }

                                            else
                                            {
                                                UnknownParam u = new UnknownParam();
                                                u.ParamName = HashCache.GetHashString(param.NameHash);
                                                u.TypeName = TypeLibrary.GetType(param.TypeHash).Name;
                                                u.Value = param.GetValue();
                                                shaderPersistentParam.Parameters.Add(u);
                                            }
                                        }
                                        staticParamBlock.ShaderBlockResources.Add(shaderPersistentParam);
                                    }
                                    else if (res is MeshSetPlugin.Resources.MeshParamDbBlock)
                                    {
                                        MeshSetPlugin.Resources.MeshParamDbBlock meshParamDbBlock = res as MeshSetPlugin.Resources.MeshParamDbBlock;
                                        MeshParamBlock paramBlock = new MeshParamBlock();
                                        paramBlock.NameHash = meshParamDbBlock.Hash.ToString("X");
                                        paramBlock.LodIndex = meshParamDbBlock.LodIndex;
                                        paramBlock.MeshAssetGuid = meshParamDbBlock.MeshAssetGuid;
                                        foreach (ParameterEntry param in meshParamDbBlock.Parameters)
                                        {
                                            if (param.TypeHash == 0xb0bc3c22)
                                            {
                                                Int32s int32 = new Int32s();
                                                int32.ParamName = HashCache.GetHashString(param.NameHash);
                                                int32.Value = (uint)param.GetValue();
                                                paramBlock.Parameters.Add(int32);
                                            }
                                            else if (param.TypeHash == 0x0d1cfa1b)
                                            {
                                                Conditional conditional = new Conditional();
                                                conditional.ParamName = HashCache.GetHashString(param.NameHash);
                                                conditional.Index = (byte)param.GetValue();
                                                paramBlock.Parameters.Add(conditional);
                                            }
                                            else if (param.TypeHash == 0xcc971f4)
                                            {
                                                Int64s int64 = new Int64s();
                                                int64.ParamName = HashCache.GetHashString(param.NameHash);
                                                int64.Value = (long)param.GetValue();
                                                paramBlock.Parameters.Add(int64);
                                            }
                                            else if (param.TypeHash == 0x7f39a7b4)
                                            {
                                                Float32 float32 = new Float32();
                                                float32.ParamName = HashCache.GetHashString(param.NameHash);
                                                float32.Value = (float)param.GetValue();
                                                paramBlock.Parameters.Add(float32);
                                            }
                                            else
                                            {
                                                UnknownParam u = new UnknownParam();
                                                u.ParamName = HashCache.GetHashString(param.NameHash);
                                                u.TypeName = TypeLibrary.GetType(param.TypeHash).Name;
                                                u.Value = param.GetValue();
                                                paramBlock.Parameters.Add(u);
                                            }
                                        }
                                        staticParamBlock.ShaderBlockResources.Add(paramBlock);
                                    }
                                }
                                shaderEntry.ShaderStaticParamDbBlocks.Add(staticParamBlock);
                            }
                            else if (block is ShaderPersistentParamDbBlock)
                            {
                                ShaderPersistentParamDbBlock persistentParamDbBlock = block as ShaderPersistentParamDbBlock;
                                ShaderPersistentParamBlock shaderPersistentParam = new ShaderPersistentParamBlock();
                                shaderPersistentParam.NameHash = persistentParamDbBlock.Hash.ToString("X");
                                foreach (ParameterEntry param in persistentParamDbBlock.Parameters)
                                {
                                    if (param.TypeHash == 0x9638b221)
                                    {
                                        Bools bools = new Bools();
                                        bools.ParamName = HashCache.GetHashString(param.NameHash);
                                        bools.Value = (bool)param.GetValue();
                                        shaderPersistentParam.Parameters.Add(bools);
                                    }

                                    else if (param.TypeHash == 0x0b87fa95)
                                    {
                                        Floats floats = new Floats();
                                        floats.ParamName = HashCache.GetHashString(param.NameHash);
                                        floats.Value = (float[])param.GetValue();
                                        shaderPersistentParam.Parameters.Add(floats);
                                    }

                                    else if (param.TypeHash == 0xb0bc3c22)
                                    {
                                        Int32s int32 = new Int32s();
                                        int32.ParamName = HashCache.GetHashString(param.NameHash);
                                        int32.Value = (uint)param.GetValue();
                                        shaderPersistentParam.Parameters.Add(int32);
                                    }

                                    else if (param.TypeHash == 0xad0abfd3)
                                    {
                                        Guid guid = (Guid)param.GetValue();
                                        EbxAssetEntry assetEntry = RootInstanceEbxEntryDb.GetEbxEntryByRootInstanceGuid(guid);
                                        Guid fileGuid = assetEntry.Guid;
                                        string item = assetEntry.Name;
                                        Textures textures = new Textures();
                                        textures.ParamName = HashCache.GetHashString(param.NameHash);
                                        textures.Value = item;
                                        shaderPersistentParam.Parameters.Add(textures);
                                    }

                                    else if (param.TypeHash == 0x0d1cfa1b)
                                    {
                                        Conditional conditional = new Conditional();
                                        conditional.ParamName = HashCache.GetHashString(param.NameHash);
                                        conditional.Index = (byte)param.GetValue();
                                        shaderPersistentParam.Parameters.Add(conditional);
                                    }

                                    else if (param.TypeHash == 0x7f39a7b4)
                                    {
                                        Float32 float32 = new Float32();
                                        float32.ParamName = HashCache.GetHashString(param.NameHash);
                                        float32.Value = (float)param.GetValue();
                                        shaderPersistentParam.Parameters.Add(float32);
                                    }
                                    else
                                    {
                                        UnknownParam u = new UnknownParam();
                                        u.ParamName = HashCache.GetHashString(param.NameHash);
                                        u.TypeName = TypeLibrary.GetType(param.TypeHash).Name;
                                        u.Value = param.GetValue();
                                        shaderPersistentParam.Parameters.Add(u);
                                    }
                                }
                                shaderEntry.ShaderStaticParamDbBlocks.Add(shaderPersistentParam);
                            }
                        }
                        blockDepot.ShaderBlockEntries.Add(shaderEntry);
                    }

                    else if (resource is ShaderBlockMeshVariationEntry)
                    {
                        ShaderBlockMeshVariationEntry meshVariationDbBlock = resource as ShaderBlockMeshVariationEntry;
                        MeshVariationBlock variationBlock = new MeshVariationBlock();
                        variationBlock.NameHash = resource.Hash.ToString("X");
                        //AssetEntry assetEntry = App.AssetManager.GetEbxEntry(new Guid("f343a49c-c8f0-7998-8fba-1522efa4abc0"));
                        //string name = assetEntry.Name;
                        for (int j = 0; j < meshVariationDbBlock.RvmShaderRefGuids.Count; j++)
                        {
                            MeshParamDbBlock un = new MeshParamDbBlock();
                            un.MeshAssetGuid = meshVariationDbBlock.RvmShaderRefGuids[j];
                            un.LodIndex = Convert.ToUInt32(meshVariationDbBlock.RvmShaderRefInts[j]);

                            variationBlock.MeshParamDbBlocks.Add(un);
                        }

                        variationBlocks.Add(variationBlock.NameHash, variationBlock);
                        blockDepot.MeshVariationDbBlocks.Add(variationBlock);
                    }
                }
                blockDepot.ResourceId = shaderblockEntry.ResRid.ToString("X");
                blockDepot.Name = shaderblockEntry.Name;
                blockDepot.Guid = new Guid(shaderBlockDepot.ResourceMeta).ToString();
                blockDepot.ResMeta = "";
                foreach (byte b in shaderBlockDepot.ResourceMeta)
                    blockDepot.ResMeta += b.ToString("X2") + " ";
                blockDepot.ResourceCount = shaderBlockDepot.ResourceCount;
                pgShaderBlockDepot.SetClass(blockDepot);
                firstTime = false;
            }

        }
        public static ulong CalculateHash(string name, Type type)
        {
            string typeName = type.Name;
            string typeModule = type.GetCustomAttribute<EbxClassMetaAttribute>().Namespace;

            byte[] buffer = null;
            using (NativeWriter writer = new NativeWriter(new MemoryStream()))
            {
                writer.Write(0x01);
                writer.Write(name.Length);
                writer.Write(typeName.Length);
                writer.Write(typeModule.Length);
                writer.WriteFixedSizedString(name, name.Length);
                writer.WriteFixedSizedString(typeName, typeName.Length);
                writer.WriteFixedSizedString(typeModule, typeModule.Length);
                buffer = writer.ToByteArray();
            }

            return ((CityHash.Hash64(buffer) & 0xFFFFFFFFFFFF) | ((ulong)Fnv1.HashString(name.ToLower()) << 48));
        }
    }
    }
