using FrostySdk;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using Frosty.Hash;
using FrostySdk.Resources;
using Frosty.Core.Handlers;
using Frosty.Core.Mod;
using Frosty.Core.IO;
using System.Threading;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using Frosty.Core.Windows;
using System.Globalization;
using static Frosty.Core.FrostyProject;
using System.Xml.Linq;

namespace Frosty.Core
{
    public sealed class FrostyProject
    {
        /*
          Project Versions:
            1 - Initial Version
            2 - (Unknown)
            3 - Assets can store multiple linked assets of CHUNK and RES type
            4 - (Unknown)
            5 - (Unknown)
            6 - New texture streaming changes (retroactively fixes old textures)
              - Stores mod details
            7 - Ebx now stored as objects rather than compressed byte streams
            8 - Chunk H32 now stored (retroactively calculate h32 for old projects)
            9 - Changed to a binary format, and added custom action handlers
            10 - TODO
            11 - Merging of defined res files (eg. ShaderBlockDepot)
            12 - Legacy files now use determinstic guids and added user data (retroactively fix old legacy files)
            13 - Merging of defined ebx files
            14 - H32 and FirstMip are now stored even if chunk was only added to bundles
        */

        private const uint FormatVersion = 14;

        private const ulong Magic = 0x00005954534F5246;

        public string DisplayName
        {
            get
            {
                if (filename == "")
                    return "New Project.fbproject";

                FileInfo fi = new FileInfo(filename);
                return fi.Name;
            }
        }
        public string Filename 
        { 
            get => filename;
            set => filename = value;
        }
        public bool IsDirty => App.AssetManager.GetDirtyCount() != 0 || modSettings.IsDirty;

        public ModSettings ModSettings => modSettings;

        private string filename;
        private DateTime creationDate;
        private DateTime modifiedDate;
        public uint gameVersion;

        // mod export settings
        private ModSettings modSettings;

        public FrostyProject()
        {
            filename = "";
            creationDate = DateTime.Now;
            modifiedDate = DateTime.Now;
            gameVersion = 0;
            modSettings = new ModSettings { Author = Config.Get("ModAuthor", "") };
            //modSettings = new ModSettings {Author = Config.Get("ModSettings", "Author", "")};
            modSettings.ClearDirtyFlag();
        }

        public bool Load(string inFilename)
        {
            filename = inFilename;

            ulong magic = 0;
            using (NativeReader reader = new NativeReader(new FileStream(inFilename, FileMode.Open, FileAccess.Read)))
            {
                magic = reader.ReadULong();
                if (magic == Magic)
                    return InternalLoad(reader, inFilename);
            }
            return LegacyLoad(inFilename);
        }

        public void Save(string overrideFilename = "", bool updateDirtyState = true, bool overrideData = false)
        {
            string actualFilename = filename;
            if (!string.IsNullOrEmpty(overrideFilename))
                actualFilename = overrideFilename;

            modifiedDate = DateTime.Now;
            gameVersion = App.FileSystem.Head;

            FileInfo fi = new FileInfo(actualFilename);
            if (!fi.Directory.Exists)
            {
                // create output directory
                Directory.CreateDirectory(fi.DirectoryName);
            }

            bool saveUsingFolderSystem = Config.Get<bool>("FbprojectFolderSystem", false) && updateDirtyState;


            if (!saveUsingFolderSystem)
            {
                // save to temporary file first
                string tempFilename = fi.FullName + ".tmp";

                using (NativeWriter writer = new NativeWriter(new FileStream(tempFilename, FileMode.Create)))
                {
                    writer.Write(Magic);
                    writer.Write(FormatVersion);
                    writer.WriteNullTerminatedString(ProfilesLibrary.ProfileName);
                    writer.Write(creationDate.Ticks);
                    writer.Write(modifiedDate.Ticks);
                    writer.Write(gameVersion);

                    writer.WriteNullTerminatedString(modSettings.Title);
                    writer.WriteNullTerminatedString(modSettings.Author);
                    writer.WriteNullTerminatedString(modSettings.Category);
                    writer.WriteNullTerminatedString(modSettings.Version);
                    writer.WriteNullTerminatedString(modSettings.Description);

                    if (modSettings.Icon != null && modSettings.Icon.Length != 0)
                    {
                        writer.Write(modSettings.Icon.Length);
                        writer.Write(modSettings.Icon);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        byte[] buf = modSettings.GetScreenshot(i);
                        if (buf != null && buf.Length != 0)
                        {
                            writer.Write(buf.Length);
                            writer.Write(buf);
                        }
                        else
                        {
                            writer.Write(0);
                        }
                    }

                    // -----------------------------------------------------------------------------
                    // added data
                    // -----------------------------------------------------------------------------

                    // @todo: superbundles
                    writer.Write(0);

                    // bundles
                    long sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    int count = 0;
                    foreach (BundleEntry entry in App.AssetManager.EnumerateBundles(modifiedOnly: true))
                    {
                        if (entry.Added)
                        {
                            writer.WriteNullTerminatedString(entry.Name);
                            writer.WriteNullTerminatedString(App.AssetManager.GetSuperBundle(entry.SuperBundleId).Name);
                            writer.Write((int)entry.Type);
                            count++;
                        }
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // ebx
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true))
                    {
                        if (entry.IsAdded)
                        {
                            writer.WriteNullTerminatedString(entry.Name);
                            writer.Write(entry.Guid);
                            count++;
                        }
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // res
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                    {
                        if (entry.IsAdded)
                        {
                            writer.WriteNullTerminatedString(entry.Name);
                            writer.Write(entry.ResRid);
                            writer.Write(entry.ResType);
                            writer.Write(entry.ResMeta);
                            count++;
                        }
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // chunks
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
                    {
                        if (entry.IsAdded)
                        {
                            writer.Write(entry.Id);
                            writer.Write(entry.H32);
                            count++;
                        }
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // -----------------------------------------------------------------------------
                    // modified data
                    // -----------------------------------------------------------------------------

                    // ebx
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true, includeLinked: true))
                    {
                        writer.WriteNullTerminatedString(entry.Name);
                        SaveLinkedAssets(entry, writer);

                        // bundles the asset has been added to
                        writer.Write(entry.AddedBundles.Count);
                        foreach (int bid in entry.AddedBundles)
                            writer.WriteNullTerminatedString(App.AssetManager.GetBundleEntry(bid).Name);

                        // if the asset has been modified
                        writer.Write(entry.HasModifiedData);
                        if (entry.HasModifiedData)
                        {
                            // mark asset as only transient modified
                            writer.Write(entry.ModifiedEntry.IsTransientModified);
                            writer.WriteNullTerminatedString(entry.ModifiedEntry.UserData);

                            ModifiedResource modifiedResource = entry.ModifiedEntry.DataObject as ModifiedResource;
                            byte[] buf = null;
                            bool bCustomHandler = modifiedResource != null;

                            if (bCustomHandler)
                            {
                                // asset is using a custom handler
                                buf = modifiedResource.Save();
                            }
                            else
                            {
                                // asset is using just regular data
                                EbxAsset asset = entry.ModifiedEntry.DataObject as EbxAsset;
                                using (EbxBaseWriter ebxWriter = EbxBaseWriter.CreateProjectWriter(new MemoryStream(), EbxWriteFlags.IncludeTransient))
                                {
                                    ebxWriter.WriteAsset(asset);
                                    buf = ebxWriter.ToByteArray();
                                }
                            }

                            writer.Write(bCustomHandler);
                            writer.Write(buf.Length);
                            writer.Write(buf);


                            if (updateDirtyState)
                                entry.ModifiedEntry.IsDirty = false;
                        }

                        if (updateDirtyState)
                            entry.IsDirty = false;

                        count++;
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // res
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                    {
                        writer.WriteNullTerminatedString(entry.Name);
                        SaveLinkedAssets(entry, writer);

                        // bundles the asset has been added to
                        writer.Write(entry.AddedBundles.Count);
                        foreach (int bid in entry.AddedBundles)
                            writer.WriteNullTerminatedString(App.AssetManager.GetBundleEntry(bid).Name);

                        // if the asset has been modified
                        writer.Write(entry.HasModifiedData);
                        if (entry.HasModifiedData)
                        {
                            writer.Write(entry.ModifiedEntry.Sha1);
                            writer.Write(entry.ModifiedEntry.OriginalSize);
                            if (entry.ModifiedEntry.ResMeta != null)
                            {
                                writer.Write(entry.ModifiedEntry.ResMeta.Length);
                                writer.Write(entry.ModifiedEntry.ResMeta);
                            }
                            else
                            {
                                // no res meta
                                writer.Write(0);
                            }
                            writer.WriteNullTerminatedString(entry.ModifiedEntry.UserData);

                            byte[] buffer = entry.ModifiedEntry.Data;
                            if (entry.ModifiedEntry.DataObject != null)
                            {
                                ModifiedResource md = entry.ModifiedEntry.DataObject as ModifiedResource;
                                buffer = md.Save();
                            }

                            writer.Write(buffer.Length);
                            writer.Write(buffer);
                            if (updateDirtyState)
                                entry.ModifiedEntry.IsDirty = false;
                        }

                        if (updateDirtyState)
                            entry.IsDirty = false;

                        count++;
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // chunks
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    foreach (ChunkAssetEntry entry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
                    {
                        writer.Write(entry.Id);

                        // bundles the asset has been added to
                        writer.Write(entry.AddedBundles.Count);
                        foreach (int bid in entry.AddedBundles)
                            writer.WriteNullTerminatedString(App.AssetManager.GetBundleEntry(bid).Name);

                        writer.Write(entry.HasModifiedData ? entry.ModifiedEntry.FirstMip : entry.FirstMip);
                        writer.Write(entry.HasModifiedData ? entry.ModifiedEntry.H32 : entry.H32);

                        // if the asset has been modified
                        writer.Write(entry.HasModifiedData);
                        if (entry.HasModifiedData)
                        {
                            writer.Write(entry.ModifiedEntry.Sha1);
                            writer.Write(entry.ModifiedEntry.LogicalOffset);
                            writer.Write(entry.ModifiedEntry.LogicalSize);
                            writer.Write(entry.ModifiedEntry.RangeStart);
                            writer.Write(entry.ModifiedEntry.RangeEnd);
                            writer.Write(entry.ModifiedEntry.AddToChunkBundle);
                            writer.WriteNullTerminatedString(entry.ModifiedEntry.UserData);

                            writer.Write(entry.ModifiedEntry.Data.Length);
                            writer.Write(entry.ModifiedEntry.Data);
                            if (updateDirtyState)
                                entry.ModifiedEntry.IsDirty = false;
                        }

                        if (updateDirtyState)
                            entry.IsDirty = false;

                        count++;
                    }

                    writer.Position = sizePosition;
                    writer.Write(count);
                    writer.Position = writer.Length;

                    // custom actions
                    sizePosition = writer.Position;
                    writer.Write(0xDEADBEEF);

                    count = 0;
                    ILegacyCustomActionHandler legacyHandler = new LegacyCustomActionHandler();
                    legacyHandler.SaveToProject(writer);

                    writer.Position = sizePosition;
                    writer.Write(1);
                    writer.Position = writer.Length;

                    if (updateDirtyState)
                        modSettings.ClearDirtyFlag();
                }

                if (File.Exists(tempFilename))
                {
                    bool isValid = false;

                    // check project file to ensure it saved correctly
                    using (FileStream fs = new FileStream(tempFilename, FileMode.Open, FileAccess.Read))
                    {
                        if (fs.Length > 0)
                        {
                            isValid = true;
                        }
                    }

                    if (isValid)
                    {
                        // replace existing project
                        File.Delete(fi.FullName);
                        File.Move(tempFilename, fi.FullName);
                    }
                }

            }
            else
            {
                using (NativeWriter writer = new NativeWriter(new FileStream(actualFilename, FileMode.Create)))
                {
                    writer.Write(Magic);
                    writer.Write(FormatVersion);
                    writer.WriteNullTerminatedString(ProfilesLibrary.ProfileName);
                    writer.Write(creationDate.Ticks);
                    writer.Write(modifiedDate.Ticks);
                    writer.Write(gameVersion);
                }

                string folderName = fi.FullName.Substring(0, fi.FullName.IndexOf(".fbproject"));

                string bundlesFolder = string.Format("{0}//bundles//", folderName);
                string ebxFolder = string.Format("{0}//ebx//", folderName);
                string resFolder = string.Format("{0}//res//", folderName);
                string chkFolder = string.Format("{0}//chunks//", folderName);

                if (!Directory.Exists(folderName))
                    Directory.CreateDirectory(folderName);
                else if (overrideData)
                {
                    System.IO.DirectoryInfo di = new DirectoryInfo(folderName);
                    foreach (FileInfo file in di.EnumerateFiles())
                        file.Delete();
                    foreach (DirectoryInfo dir in di.GetDirectories())
                        dir.Delete(true);

                    //Directory.Delete(folderName);
                    //Directory.CreateDirectory(folderName);
                }
                else
                {
                    if (Directory.Exists(bundlesFolder))
                    {
                        List<string> files = System.IO.Directory.GetFiles(bundlesFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            string bunName = file.Substring(bundlesFolder.Length).Replace("\\", "/");
                            int bunId = App.AssetManager.GetBundleId(bunName);
                            if (bunId == -1)
                                File.Delete(file + ".json");
                        }
                    }
                    if (Directory.Exists(ebxFolder))
                    {
                        List<string> files = System.IO.Directory.GetFiles(ebxFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            string ebxName = file.Substring(ebxFolder.Length).Replace("\\", "/");
                            EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(ebxName);
                            if (refEntry == null || !refEntry.IsModified)
                            {
                                File.Delete(file + ".json");
                                if (File.Exists(file + ".bin"))
                                    File.Delete(file + ".bin");
                            }
                        }
                    }
                    if (Directory.Exists(resFolder))
                    {
                        List<string> files = System.IO.Directory.GetFiles(resFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            string resName = file.Substring(resFolder.Length).Replace("\\", "/");
                            ResAssetEntry refEntry = App.AssetManager.GetResEntry(resName);
                            if (refEntry == null || !refEntry.IsModified)
                            {
                                File.Delete(file + ".json");
                                if (File.Exists(file + ".res"))
                                    File.Delete(file + ".res");
                            }
                        }
                    }
                    if (Directory.Exists(chkFolder))
                    {
                        List<string> files = System.IO.Directory.GetFiles(chkFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            string chkName = file.Substring(chkFolder.Length).Replace("\\", "/");
                            ChunkAssetEntry refEntry = App.AssetManager.GetChunkEntry(new Guid(chkName));
                            if (refEntry == null || !refEntry.IsModified)
                            {
                                File.Delete(file + ".json");
                                if (File.Exists(file + ".chunk"))
                                    File.Delete(file + ".chunk");
                            }
                        }
                    }
                }

                File.WriteAllText(string.Format("{0}//ModSettings.json", folderName), JsonConvert.SerializeObject(new ModSettingsJson(modSettings), Formatting.Indented));

                if (modSettings.Icon != null && modSettings.Icon.Length != 0)
                    using (NativeWriter writer = new NativeWriter(new FileStream(string.Format("{0}//ModIcon.png", folderName), FileMode.Create)))
                        writer.Write(modSettings.Icon);

                for (int i = 0; i < 4; i++)
                {
                    byte[] buf = modSettings.GetScreenshot(i);
                    if (buf != null && buf.Length != 0)
                        using (NativeWriter writer = new NativeWriter(new FileStream(string.Format("{0}//ModScreenshot{1}.png", folderName, i + 1), FileMode.Create)))
                            writer.Write(buf);

                }

                foreach (BundleEntry bEntry in App.AssetManager.EnumerateBundles(modifiedOnly: true))
                {
                    if (!bEntry.Added)
                        continue;

                    FileInfo bundleJson = new FileInfo(string.Format("{0}{1}.json", bundlesFolder, bEntry.Name));
                    if (!bundleJson.Directory.Exists)
                        Directory.CreateDirectory(bundleJson.DirectoryName);
                    BundleJson bundle = new BundleJson(bEntry);
                    File.WriteAllText(bundleJson.FullName, JsonConvert.SerializeObject(bundle, Formatting.Indented));
                }

                Parallel.ForEach(App.AssetManager.EnumerateEbx(modifiedOnly: true, includeLinked: true), refEntry =>
                {
                    string intendedFileName = string.Format("{0}{1}.json", ebxFolder, refEntry.Name);
                    if (intendedFileName.Count() >= 260 || intendedFileName.LastIndexOf("//") >= 248)
                    {
                        App.Logger.LogError(string.Format("Project Writing Error: Cannot write asset \"{0}\" as the intended path is too long \"{1}\"", refEntry.DisplayName, intendedFileName));
                        return;
                    }

                    FileInfo ebxJson = new FileInfo(string.Format("{0}{1}.json", ebxFolder, refEntry.Name));
                    if (!ebxJson.Directory.Exists)
                        Directory.CreateDirectory(ebxJson.DirectoryName);
                    EbxJson ebx = new EbxJson(refEntry);
                    File.WriteAllText(ebxJson.FullName, JsonConvert.SerializeObject(ebx, Formatting.Indented));

                    string binName = string.Format("{0}{1}.bin", ebxFolder, refEntry.Name);
                    if (refEntry.HasModifiedData && (overrideData || refEntry.ModifiedEntry.IsDirty || !File.Exists(binName)))
                    {
                        ModifiedResource modifiedResource = refEntry.ModifiedEntry.DataObject as ModifiedResource;
                        byte[] buf = null;
                        bool bCustomHandler = modifiedResource != null;

                        if (bCustomHandler)
                        {
                            // asset is using a custom handler
                            buf = modifiedResource.Save();
                        }
                        else
                        {
                            // asset is using just regular data
                            EbxAsset asset = refEntry.ModifiedEntry.DataObject as EbxAsset;

                            using (EbxBaseWriter ebxWriter = EbxBaseWriter.CreateProjectWriter(new MemoryStream(), EbxWriteFlags.IncludeTransient))
                            {
                                ebxWriter.WriteAsset(asset);
                                buf = ebxWriter.ToByteArray();
                            }
                        }
                        using (NativeWriter writer = new NativeWriter(new FileStream(binName, FileMode.Create)))
                            writer.Write(buf);

                        if (updateDirtyState)
                            refEntry.ModifiedEntry.IsDirty = false;
                    }
                    if (updateDirtyState)
                        refEntry.IsDirty = false;
                });

                foreach (ResAssetEntry resEntry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                {
                    FileInfo resFileInfo = new FileInfo(string.Format("{0}{1}.json", resFolder, resEntry.Name));
                    if (!resFileInfo.Directory.Exists)
                        Directory.CreateDirectory(resFileInfo.DirectoryName);

                    ResJson resJson = new ResJson(resEntry);
                    File.WriteAllText(resFileInfo.FullName, JsonConvert.SerializeObject(resJson, Formatting.Indented));

                    string resName = string.Format("{0}{1}.res", resFolder, resEntry.Name);
                    if (resEntry.HasModifiedData && (overrideData || resEntry.ModifiedEntry.IsDirty || !File.Exists(resName)))
                    {
                        using (NativeWriter writer = new NativeWriter(new FileStream(resName, FileMode.Create)))
                        {
                            byte[] buffer = resEntry.ModifiedEntry.Data;
                            if (resEntry.ModifiedEntry.DataObject != null)
                            {
                                ModifiedResource md = resEntry.ModifiedEntry.DataObject as ModifiedResource;
                                buffer = md.Save();
                            }

                            writer.Write(buffer);
                        }

                        if (updateDirtyState)
                            resEntry.ModifiedEntry.IsDirty = false;
                    }
                    if (updateDirtyState)
                        resEntry.IsDirty = false;
                }

                foreach (ChunkAssetEntry chkEntry in App.AssetManager.EnumerateChunks(modifiedOnly: true))
                {
                    FileInfo chkFileInfo = new FileInfo(string.Format("{0}{1}.json", chkFolder, chkEntry.Name));
                    if (!chkFileInfo.Directory.Exists)
                        Directory.CreateDirectory(chkFileInfo.DirectoryName);

                    ChkJson chkJson = new ChkJson(chkEntry);
                    File.WriteAllText(chkFileInfo.FullName, JsonConvert.SerializeObject(chkJson, Formatting.Indented));

                    string chkName = string.Format("{0}{1}.chunk", chkFolder, chkEntry.Name);
                    if (chkEntry.HasModifiedData && (overrideData || chkEntry.ModifiedEntry.IsDirty || !File.Exists(chkName)))
                    {
                        using (NativeWriter writer = new NativeWriter(new FileStream(chkName, FileMode.Create)))
                            writer.Write(chkEntry.ModifiedEntry.Data);

                        if (updateDirtyState)
                            chkEntry.ModifiedEntry.IsDirty = false;
                    }

                    if (updateDirtyState)
                        chkEntry.IsDirty = false;
                }
            }
        }

        public ModSettings GetModSettings()
        {
            return modSettings;
        }

        public void WriteToMod(string filename, ModSettings overrideSettings, bool editorLaunch, CancellationToken cancelToken)
        {
            using (FrostyModWriter writer = new FrostyModWriter(new FileStream(filename, FileMode.Create), overrideSettings))
            {
                writer.WriteProject(this, editorLaunch, cancelToken);
            }
        }

        public static void SaveLinkedAssets(AssetEntry entry, NativeWriter writer)
        {
            writer.Write(entry.LinkedAssets.Count);
            foreach (AssetEntry linkedEntry in entry.LinkedAssets)
            {
                writer.WriteNullTerminatedString(linkedEntry.AssetType);
                if (linkedEntry is ChunkAssetEntry assetEntry)
                    writer.Write(assetEntry.Id);
                else
                    writer.WriteNullTerminatedString(linkedEntry.Name);
            }
        }

        public static List<AssetEntry> LoadLinkedAssets(NativeReader reader)
        {
            int numItems = reader.ReadInt();
            List<AssetEntry> linkedEntries = new List<AssetEntry>();

            for (int i = 0; i < numItems; i++)
            {
                string type = reader.ReadNullTerminatedString();
                if (type == "ebx")
                {
                    string name = reader.ReadNullTerminatedString();
                    EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(name);
                    if (ebxEntry != null)
                        linkedEntries.Add(ebxEntry);
                }
                else if (type == "res")
                {
                    string name = reader.ReadNullTerminatedString();
                    ResAssetEntry resEntry = App.AssetManager.GetResEntry(name);
                    if (resEntry != null)
                        linkedEntries.Add(resEntry);
                }
                else if (type == "chunk")
                {
                    Guid id = reader.ReadGuid();
                    ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(id);
                    if (chunkEntry != null)
                        linkedEntries.Add(chunkEntry);
                }
                else
                {
                    string name = reader.ReadNullTerminatedString();
                    AssetEntry customEntry = App.AssetManager.GetCustomAssetEntry(type, name);
                    if (customEntry != null)
                        linkedEntries.Add(customEntry);
                }
            }

            return linkedEntries;
        }

        public static void LoadLinkedAssets(DbObject asset, AssetEntry entry, uint version)
        {
            if (version == 2)
            {
                // old projects can only store one linked asset
                string linkedAssetType = asset.GetValue<string>("linkedAssetType");
                if (linkedAssetType == "res")
                {
                    string name = asset.GetValue<string>("linkedAssetId");
                    entry.LinkedAssets.Add(App.AssetManager.GetResEntry(name));
                }
                else if (linkedAssetType == "chunk")
                {
                    Guid id = asset.GetValue<Guid>("linkedAssetId");
                    entry.LinkedAssets.Add(App.AssetManager.GetChunkEntry(id));
                }
            }
            else
            {
                foreach (DbObject linkedAsset in asset.GetValue<DbObject>("linkedAssets"))
                {
                    string type = linkedAsset.GetValue<string>("type");
                    if (type == "ebx")
                    {
                        string name = linkedAsset.GetValue<string>("id");
                        EbxAssetEntry ebxEntry = App.AssetManager.GetEbxEntry(name);
                        if (ebxEntry != null)
                            entry.LinkedAssets.Add(ebxEntry);
                    }
                    else if (type == "res")
                    {
                        string name = linkedAsset.GetValue<string>("id");
                        ResAssetEntry resEntry = App.AssetManager.GetResEntry(name);
                        if (resEntry != null)
                            entry.LinkedAssets.Add(resEntry);
                    }
                    else if (type == "chunk")
                    {
                        Guid id = linkedAsset.GetValue<Guid>("id");
                        ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(id);
                        if (chunkEntry != null)
                            entry.LinkedAssets.Add(chunkEntry);
                    }
                    else
                    {
                        string name = linkedAsset.GetValue<string>("id");
                        AssetEntry customEntry = App.AssetManager.GetCustomAssetEntry(type, name);
                        if (customEntry != null)
                            entry.LinkedAssets.Add(customEntry);
                    }
                }
            }
        }

        private bool InternalLoad(NativeReader reader, string fbprojName)
        {
            uint version = reader.ReadUInt();
            if (version > FormatVersion)
                return false;

            // 9 is the first version to support the new format, any earlier versions stored
            // here are invalid

            if (version < 9)
                return false;

            string gameProfile = reader.ReadNullTerminatedString();
            if (gameProfile.ToLower() != ProfilesLibrary.ProfileName.ToLower())
                return false;


            FrostyTaskWindow.Show("Loading Project", "", (task) =>
            {
                task.Update(filename);
                Dictionary<int, AssetEntry> h32map = new Dictionary<int, AssetEntry>();

                creationDate = new DateTime(reader.ReadLong());
                modifiedDate = new DateTime(reader.ReadLong());
                gameVersion = reader.ReadUInt();

                bool folderStrucure = reader.BaseStream.Length == reader.BaseStream.Position;
                if (!folderStrucure)
                {
                    modSettings.Title = reader.ReadNullTerminatedString();
                    modSettings.Author = reader.ReadNullTerminatedString();
                    modSettings.Category = reader.ReadNullTerminatedString();
                    modSettings.Version = reader.ReadNullTerminatedString();
                    modSettings.Description = reader.ReadNullTerminatedString();

                    int size = reader.ReadInt();
                    if (size > 0)
                        modSettings.Icon = reader.ReadBytes(size);

                    for (int i = 0; i < 4; i++)
                    {
                        size = reader.ReadInt();
                        if (size > 0)
                            modSettings.SetScreenshot(i, reader.ReadBytes(size));
                    }

                    modSettings.ClearDirtyFlag();

                    // -----------------------------------------------------------------------------
                    // added data
                    // -----------------------------------------------------------------------------

                    // superbundles
                    int numItems = reader.ReadInt();

                    // bundles
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        string name = reader.ReadNullTerminatedString();
                        string sbName = reader.ReadNullTerminatedString();
                        BundleType type = (BundleType)reader.ReadInt();

                        App.AssetManager.AddBundle(name, type, App.AssetManager.GetSuperBundleId(sbName));
                    }

                    // ebx
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        EbxAssetEntry entry = new EbxAssetEntry
                        {
                            Name = reader.ReadNullTerminatedString(),
                            Guid = reader.ReadGuid()
                        };
                        App.AssetManager.AddEbx(entry);
                    }

                    // res
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        ResAssetEntry entry = new ResAssetEntry
                        {
                            Name = reader.ReadNullTerminatedString(),
                            ResRid = reader.ReadULong(),
                            ResType = reader.ReadUInt(),
                            ResMeta = reader.ReadBytes(0x10)
                        };
                        App.AssetManager.AddRes(entry);
                    }

                    // chunks
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        ChunkAssetEntry newEntry = new ChunkAssetEntry
                        {
                            Id = reader.ReadGuid(),
                            H32 = reader.ReadInt()
                        };
                        App.AssetManager.AddChunk(newEntry);
                    }

                    // -----------------------------------------------------------------------------
                    // modified data
                    // -----------------------------------------------------------------------------

                    // ebx
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        string name = reader.ReadNullTerminatedString();
                        List<AssetEntry> linkedEntries = LoadLinkedAssets(reader);
                        List<int> bundles = new List<int>();

                        if (version >= 13)
                        {
                            int length = reader.ReadInt();
                            for (int j = 0; j < length; j++)
                            {
                                string bundleName = reader.ReadNullTerminatedString();
                                int bid = App.AssetManager.GetBundleId(bundleName);
                                if (bid != -1)
                                    bundles.Add(bid);
                            }
                        }

                        bool isModified = reader.ReadBoolean();

                        bool isTransientModified = false;
                        string userData = "";
                        byte[] data = null;
                        bool modifiedResource = false;

                        if (isModified)
                        {
                            isTransientModified = reader.ReadBoolean();
                            if (version >= 12)
                                userData = reader.ReadNullTerminatedString();

                            if (version < 13)
                            {
                                int length = reader.ReadInt();
                                for (int j = 0; j < length; j++)
                                {
                                    string bundleName = reader.ReadNullTerminatedString();
                                    int bid = App.AssetManager.GetBundleId(bundleName);
                                    if (bid != -1)
                                        bundles.Add(bid);
                                }
                            }

                            if (version >= 13)
                                modifiedResource = reader.ReadBoolean();
                            data = reader.ReadBytes(reader.ReadInt());
                        }

                        EbxAssetEntry entry = App.AssetManager.GetEbxEntry(name);
                        if (entry != null)
                        {
                            entry.LinkedAssets.AddRange(linkedEntries);
                            entry.AddedBundles.AddRange(bundles);

                            if (isModified)
                            {
                                entry.ModifiedEntry = new ModifiedAssetEntry
                                {
                                    IsTransientModified = isTransientModified,
                                    UserData = userData
                                };

                                if (modifiedResource)
                                {
                                    // store as modified resource data object
                                    entry.ModifiedEntry.DataObject = ModifiedResource.Read(data);
                                    entry.ModifiedEntry.Sha1 = Utils.GenerateSha1(data);
                                }
                                else
                                {
                                    if (!entry.IsAdded && App.PluginManager.GetCustomHandler(entry.Type) != null)
                                    {
                                        // @todo: throw some kind of error
                                    }

                                    // store as a regular ebx
                                    using (EbxReader ebxReader = EbxReader.CreateProjectReader(new MemoryStream(data)))
                                    {
                                        EbxAsset asset = ebxReader.ReadAsset<EbxAsset>();
                                        entry.ModifiedEntry.DataObject = asset;

                                        if (entry.IsAdded)
                                            entry.Type = asset.RootObject.GetType().Name;
                                        entry.ModifiedEntry.DependentAssets.AddRange(asset.Dependencies);
                                        entry.ModifiedEntry.Sha1 = Utils.GenerateSha1(data);
                                    }
                                }

                                entry.OnModified();
                            }

                            int hash = Fnv1.HashString(entry.Name);
                            if (!h32map.ContainsKey(hash))
                                h32map.Add(hash, entry);
                        }
                    }

                    // res
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        string name = reader.ReadNullTerminatedString();
                        List<AssetEntry> linkedEntries = LoadLinkedAssets(reader);
                        List<int> bundles = new List<int>();

                        if (version >= 13)
                        {
                            int length = reader.ReadInt();
                            for (int j = 0; j < length; j++)
                            {
                                string bundleName = reader.ReadNullTerminatedString();
                                int bid = App.AssetManager.GetBundleId(bundleName);
                                if (bid != -1)
                                    bundles.Add(bid);
                            }
                        }

                        bool isModified = reader.ReadBoolean();

                        Sha1 sha1 = Sha1.Zero;
                        long originalSize = 0;
                        byte[] resMeta = null;
                        byte[] data = null;
                        string userData = "";

                        if (isModified)
                        {
                            sha1 = reader.ReadSha1();
                            originalSize = reader.ReadLong();

                            int length = reader.ReadInt();
                            if (length > 0)
                                resMeta = reader.ReadBytes(length);

                            if (version >= 12)
                                userData = reader.ReadNullTerminatedString();

                            if (version < 13)
                            {
                                length = reader.ReadInt();
                                for (int j = 0; j < length; j++)
                                {
                                    string bundleName = reader.ReadNullTerminatedString();
                                    int bid = App.AssetManager.GetBundleId(bundleName);
                                    if (bid != -1)
                                        bundles.Add(bid);
                                }
                            }

                            data = reader.ReadBytes(reader.ReadInt());
                        }

                        ResAssetEntry entry = App.AssetManager.GetResEntry(name);
                        if (entry != null)
                        {
                            entry.LinkedAssets.AddRange(linkedEntries);
                            entry.AddedBundles.AddRange(bundles);

                            if (isModified)
                            {
                                entry.ModifiedEntry = new ModifiedAssetEntry
                                {
                                    Sha1 = sha1,
                                    OriginalSize = originalSize,
                                    ResMeta = resMeta,
                                    UserData = userData
                                };

                                if (sha1 == Sha1.Zero)
                                {
                                    // store as modified resource data object
                                    entry.ModifiedEntry.DataObject = ModifiedResource.Read(data);
                                }
                                else
                                {
                                    if (!entry.IsAdded && App.PluginManager.GetCustomHandler((ResourceType)entry.ResType) != null)
                                    {
                                        // @todo: throw some kind of error here
                                    }

                                    // store as normal data
                                    entry.ModifiedEntry.Data = data;
                                }

                                entry.OnModified();
                            }

                            int hash = Fnv1.HashString(entry.Name);
                            if (!h32map.ContainsKey(hash))
                                h32map.Add(hash, entry);
                        }
                    }

                    // chunks
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        Guid id = reader.ReadGuid();
                        List<int> bundles = new List<int>();

                        if (version >= 13)
                        {
                            int length = reader.ReadInt();
                            for (int j = 0; j < length; j++)
                            {
                                string bundleName = reader.ReadNullTerminatedString();
                                int bid = App.AssetManager.GetBundleId(bundleName);
                                if (bid != -1)
                                    bundles.Add(bid);
                            }
                        }

                        Sha1 sha1 = Sha1.Zero;
                        uint logicalOffset = 0;
                        uint logicalSize = 0;
                        uint rangeStart = 0;
                        uint rangeEnd = 0;
                        int firstMip = -1;
                        int h32 = 0;
                        bool addToChunkBundles = false;
                        string userData = "";
                        byte[] data = null;

                        if (version > 13)
                        {
                            firstMip = reader.ReadInt();
                            h32 = reader.ReadInt();
                        }

                        bool isModified = true;
                        if (version >= 13)
                            isModified = reader.ReadBoolean();

                        if (isModified)
                        {
                            sha1 = reader.ReadSha1();
                            logicalOffset = reader.ReadUInt();
                            logicalSize = reader.ReadUInt();
                            rangeStart = reader.ReadUInt();
                            rangeEnd = reader.ReadUInt();

                            if (version < 14)
                            {
                                firstMip = reader.ReadInt();
                                h32 = reader.ReadInt();
                            }

                            addToChunkBundles = reader.ReadBoolean();
                            if (version >= 12)
                                userData = reader.ReadNullTerminatedString();

                            if (version < 13)
                            {
                                int length = reader.ReadInt();
                                for (int j = 0; j < length; j++)
                                {
                                    string bundleName = reader.ReadNullTerminatedString();
                                    int bid = App.AssetManager.GetBundleId(bundleName);
                                    if (bid != -1)
                                        bundles.Add(bid);
                                }
                            }

                            data = reader.ReadBytes(reader.ReadInt());
                        }

                        ChunkAssetEntry entry = App.AssetManager.GetChunkEntry(id);

                        if (entry == null && isModified)
                        {
                            // hack: since chunks are not modified by FrostEd patches, instead a new one
                            // is added when something that uses a chunk is modified. If an existing chunk
                            // from a project is missing, a new one is created, and its linked resource
                            // is used to fill in the bundles (this may fail if a chunk is not meant to be
                            // in any bundles)

                            ChunkAssetEntry newEntry = new ChunkAssetEntry
                            {
                                Id = id,
                                H32 = h32
                            };
                            App.AssetManager.AddChunk(newEntry);

                            if (h32map.ContainsKey(newEntry.H32))
                            {
                                foreach (int bundleId in h32map[newEntry.H32].Bundles)
                                    newEntry.AddToBundle(bundleId);
                            }
                            entry = newEntry;
                        }

                        if (entry != null)
                        {
                            entry.AddedBundles.AddRange(bundles);
                            if (isModified)
                            {
                                entry.ModifiedEntry = new ModifiedAssetEntry
                                {
                                    Sha1 = sha1,
                                    LogicalOffset = logicalOffset,
                                    LogicalSize = logicalSize,
                                    RangeStart = rangeStart,
                                    RangeEnd = rangeEnd,
                                    FirstMip = firstMip,
                                    H32 = h32,
                                    AddToChunkBundle = addToChunkBundles,
                                    UserData = userData,
                                    Data = data
                                };
                                entry.OnModified();
                            }
                            else
                            {
                                entry.H32 = h32;
                                entry.FirstMip = firstMip;
                            }
                        }
                    }

                    // custom actions
                    numItems = reader.ReadInt();
                    for (int i = 0; i < numItems; i++)
                    {
                        string typeString = reader.ReadNullTerminatedString();

                        ILegacyCustomActionHandler actionHandler = new LegacyCustomActionHandler();
                        actionHandler.LoadFromProject(version, reader, typeString);

                        // @hack: fixes an issue where v11 projects incorrectly wrote a null custom handler
                        if (version < 12)
                            break;
                    }
                }
                else
                {
                    task.Update("Loading folder system");
                    string folderName = System.IO.Path.ChangeExtension(fbprojName, null);
                    if (!Directory.Exists(folderName))
                        App.Logger.LogError(String.Format("Project Loading Error: Fbproject \"{0}\" uses folder system but directory \"{1}\" does not exist", System.IO.Path.GetFileName(fbprojName), folderName));

                    string settingsName = folderName + @"/ModSettings.json";
                    if (File.Exists(settingsName))
                    {
                        ModSettingsJson settings = JsonConvert.DeserializeObject<ModSettingsJson>(File.ReadAllText(settingsName));
                        modSettings.Description = settings.Description != null ? settings.Description : "";
                        modSettings.Title = settings.Title != null ? settings.Title : "";
                        modSettings.Category = settings.Category != null ? settings.Category : "";
                        modSettings.Author = settings.Author != null ? settings.Author : "";
                        modSettings.Link = settings.Link != null ? settings.Link : "";
                        modSettings.Version = settings.Version != null ? settings.Version : "";
                    }



                    Dictionary<BundleEntry, Guid> bundlesToAssignBlueprints = new Dictionary<BundleEntry, Guid>();
                    Dictionary<AssetEntry, List<Guid>> assetsToLinkToEbx = new Dictionary<AssetEntry, List<Guid>>();
                    Dictionary<AssetEntry, List<ulong>> assetsToLinkToRes = new Dictionary<AssetEntry, List<ulong>>();
                    Dictionary<AssetEntry, List<Guid>> assetsToLinkToChunk = new Dictionary<AssetEntry, List<Guid>>();

                    string bundleFolder = string.Format("{0}//bundles//", folderName);
                    if (Directory.Exists(bundleFolder))
                    {
                        task.Update("Loading Bundles");
                        List<string> files = System.IO.Directory.GetFiles(bundleFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        int idx = 0;
                        int count = files.Count;
                        object padlock = new object();
                        Parallel.ForEach(files, file =>
                        {
                            BundleJson bunJson = JsonConvert.DeserializeObject<BundleJson>(File.ReadAllText(file + ".json"));
                            string bunName = file.Substring(bundleFolder.Length).Replace("\\", "/");
                            if (bunJson.OriginalName != null && bunName.ToLower() == bunJson.OriginalName.ToLower() && bunName != bunJson.OriginalName)
                            {
                                App.Logger.Log($"{bunName}-{bunJson.OriginalName}");
                                bunName = bunJson.OriginalName;
                            }
                            int bId = App.AssetManager.GetBundleId(bunName);
                            if (bId == -1)
                            {
                                lock (padlock)
                                {
                                    BundleEntry bEntry = App.AssetManager.AddBundle(bunName, bunJson.BundleType, App.AssetManager.GetSuperBundleId(bunJson.SuperBundle));
                                    if (bunJson.Blueprint != Guid.Empty)
                                        bundlesToAssignBlueprints.Add(bEntry, bunJson.Blueprint);
                                }
                            }
                        });
                    }

                    string ebxFolder = string.Format("{0}//ebx//", folderName);
                    if (Directory.Exists(ebxFolder))
                    {
                        task.Update("Loading Ebx");
                        List<string> files = System.IO.Directory.GetFiles(ebxFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        int idx = 0;
                        int count = files.Count;
                        object padlock = new object();

                        Parallel.ForEach(files, file =>
                        {
                            EbxJson ebxJson = JsonConvert.DeserializeObject<EbxJson>(File.ReadAllText(file + ".json"));
                            string ebxName = file.Substring(ebxFolder.Length).Replace("\\", "/");
                            if (ebxJson.OriginalName != null && ebxName.ToLower() == ebxJson.OriginalName.ToLower() && ebxName != ebxJson.OriginalName)
                            {
                                App.Logger.Log($"{ebxName}-{ebxJson.OriginalName}");
                                ebxName = ebxJson.OriginalName;
                            }
                            EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(ebxName);

                            if (refEntry == null)
                            {
                                refEntry = new EbxAssetEntry
                                {
                                    Name = ebxName,
                                    Guid = ebxJson.FileGuid,
                                };
                                lock (padlock)
                                {
                                    App.AssetManager.AddEbx(refEntry);
                                }
                            }

                            foreach (string bunName in ebxJson.AddedBundles)
                            {
                                int bunId = App.AssetManager.GetBundleId(bunName);
                                if (bunId != -1)
                                    refEntry.AddedBundles.Add(bunId);
                                else
                                    App.Logger.LogError(String.Format("Project Loading Error: Asset \"{0}\" is added to a bundle \"{1}\" which could not be found", ebxName, bunName));
                            }

                            if (ebxJson.LinkedEbx.Count > 0)
                                assetsToLinkToEbx.Add(refEntry, ebxJson.LinkedEbx);
                            if (ebxJson.LinkedRes.Count > 0)
                                assetsToLinkToRes.Add(refEntry, ebxJson.LinkedRes);
                            if (ebxJson.LinkedChunk.Count > 0)
                                assetsToLinkToChunk.Add(refEntry, ebxJson.LinkedChunk);

                            ReadModifiedEbxJsonFormat(refEntry, ebxJson, file);
                            lock (padlock)
                            {
                                task.Update(progress: (float)idx++ / count * 100);
                            }
                        });
                    }

                    string resFolder = string.Format("{0}//res//", folderName);
                    if (Directory.Exists(resFolder))
                    {
                        task.Update("Loading Res");
                        List<string> files = System.IO.Directory.GetFiles(resFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            ResJson resJson = JsonConvert.DeserializeObject<ResJson>(File.ReadAllText(file + ".json"));
                            string resName = file.Substring(resFolder.Length).Replace("\\", "/");
                            if (resJson.OriginalName != null && resName.ToLower() == resJson.OriginalName.ToLower() && resName != resJson.OriginalName)
                            {
                                App.Logger.Log($"{resName}-{resJson.OriginalName}");
                                resName = resJson.OriginalName;
                            }
                            ResAssetEntry resEntry = App.AssetManager.GetResEntry(resName);

                            if (resEntry == null)
                            {
                                resEntry = new ResAssetEntry
                                {
                                    Name = resName,
                                    ResRid = resJson.ResRid,
                                    ResType = resJson.ResType,
                                    ResMeta = StringToByteArray(resJson.ResMeta),
                                };
                                App.AssetManager.AddRes(resEntry);
                            }

                            foreach (string bunName in resJson.AddedBundles)
                            {
                                int bunId = App.AssetManager.GetBundleId(bunName);
                                if (bunId != -1)
                                    resEntry.AddedBundles.Add(bunId);
                                else
                                    App.Logger.LogError(String.Format("Project Loading Error: Res Asset \"{0}\" is added to a bundle \"{1}\" which could not be found", resName, bunName));
                            }

                            if (resJson.LinkedEbx.Count > 0)
                                assetsToLinkToEbx.Add(resEntry, resJson.LinkedEbx);
                            if (resJson.LinkedRes.Count > 0)
                                assetsToLinkToRes.Add(resEntry, resJson.LinkedRes);
                            if (resJson.LinkedChunk.Count > 0)
                                assetsToLinkToChunk.Add(resEntry, resJson.LinkedChunk);

                            ReadModifiedResJsonFormat(resEntry, resJson, file);
                        }
                    }

                    string chkFolder = string.Format("{0}//chunks//", folderName);
                    if (Directory.Exists(chkFolder))
                    {
                        task.Update("Loading Chunks");
                        List<string> files = System.IO.Directory.GetFiles(chkFolder, "*.json", SearchOption.AllDirectories).Select(o => System.IO.Path.ChangeExtension(o, null)).ToList();
                        foreach (string file in files)
                        {
                            ChkJson chkJson = JsonConvert.DeserializeObject<ChkJson>(File.ReadAllText(file + ".json"));
                            Guid chkId = new Guid(file.Substring(chkFolder.Length).Replace("\\", "/"));
                            ChunkAssetEntry chkEntry = App.AssetManager.GetChunkEntry(chkId);

                            if (chkEntry == null)
                            {
                                chkEntry = new ChunkAssetEntry
                                {
                                    Id = chkId,
                                    H32 = chkJson.H32,
                                };
                                App.AssetManager.AddChunk(chkEntry);
                            }

                            foreach (string bunName in chkJson.AddedBundles)
                            {
                                int bunId = App.AssetManager.GetBundleId(bunName);
                                if (bunId != -1)
                                    chkEntry.AddedBundles.Add(bunId);
                                else
                                    App.Logger.LogError(String.Format("Project Loading Error: Chunk Asset \"{0}\" is added to a bundle \"{1}\" which could not be found", chkId, bunName));
                            }

                            ReadModifiedChkJsonFormat(chkEntry, chkJson, file);

                            int hash = Fnv1.HashString(chkEntry.Name);
                        }
                    }

                    foreach (KeyValuePair<BundleEntry, Guid> pair in bundlesToAssignBlueprints)
                    {
                        EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(pair.Value);
                        if (refEntry != null)
                            pair.Key.Blueprint = refEntry;
                        else
                            App.Logger.LogError(String.Format("Project Loading Error: Could not assign a blueprint to bundle \"{0}\" as the blueprint guid \"{1}\" could not be found", pair.Key.DisplayName, pair.Value));
                    }
                    foreach (KeyValuePair<AssetEntry, List<Guid>> pair in assetsToLinkToEbx)
                    {
                        foreach (Guid guid in pair.Value)
                        {
                            EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(guid);
                            if (refEntry != null)
                                pair.Key.LinkAsset(refEntry);
                            else
                                App.Logger.LogError(String.Format("Project Loading Error: Could not link asset \"{0}\" to ebx with guid \"{1}\" as it could not be found", pair.Key.DisplayName, guid));
                        }
                    }
                    foreach (KeyValuePair<AssetEntry, List<ulong>> pair in assetsToLinkToRes)
                    {
                        foreach (ulong resRid in pair.Value)
                        {
                            ResAssetEntry resEntry = App.AssetManager.GetResEntry(resRid);
                            if (resEntry != null)
                                pair.Key.LinkAsset(resEntry);
                            else
                                App.Logger.LogError(String.Format("Project Loading Error: Could not link asset \"{0}\" to res with Id \"{1}\" as it could not be found", pair.Key.DisplayName, resRid));
                        }
                    }
                    foreach (KeyValuePair<AssetEntry, List<Guid>> pair in assetsToLinkToChunk)
                    {
                        foreach (Guid guid in pair.Value)
                        {
                            ChunkAssetEntry chkEntry = App.AssetManager.GetChunkEntry(guid);
                            if (chkEntry != null)
                                pair.Key.LinkAsset(chkEntry);
                            else
                                App.Logger.LogError(String.Format("Project Loading Error: Could not link asset \"{0}\" to chunk with guid \"{1}\" as it could not be found", pair.Key.DisplayName, guid));
                        }
                    }
                }
            });

            return true;
        }

        public static byte[] StringToByteArray(string hex)
        {
            if (hex == null)
                return null;
            else
                return hex.Split('-').Select(t => byte.Parse(t, NumberStyles.AllowHexSpecifier)).ToArray();
        }

        private void ReadModifiedEbxJsonFormat(EbxAssetEntry refEntry, EbxJson ebxJson, string fileName)
        {
            if (ebxJson.ModifiedEntry == null)
                return;
            EbxJsonModifiedData modifiedJson = ebxJson.ModifiedEntry;

            string binName = fileName + ".bin";
            if (!System.IO.File.Exists(fileName + ".bin"))
            {
                App.Logger.LogError(String.Format("Project Loading Error: Modified Asset \"{0}\" has no binary file \"{1}\". Cannot read modified data", refEntry.Name, binName));
                return;
            }

            if (modifiedJson.UsesCustomHandlerData)
            {
                if (!refEntry.IsAdded && App.PluginManager.GetCustomHandler(refEntry.Type) == null)
                {
                    App.Logger.LogError(String.Format("Project Loading Error: Asset \"{0}\" uses custom handler for type \"{1}\" which no loaded plugin can handle", refEntry.Name, refEntry.Type));
                    return;
                }
                else if (refEntry.IsAdded)
                {
                    App.Logger.LogError(String.Format("Project Loading Error: Asset \"{0}\" uses custom handler which is unsupporting for duplicated assets", refEntry.Name));
                    return;
                }
            }

            refEntry.ModifiedEntry = new ModifiedAssetEntry
            {
                IsTransientModified = modifiedJson.IsTransientModified,
                UserData = modifiedJson.UserData,
            };

            if (!modifiedJson.UsesCustomHandlerData)
            {
                using (NativeReader reader = new NativeReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                {
                    byte[] data = reader.ReadToEnd();
                    using (EbxReader ebxReader = EbxReader.CreateProjectReader(new MemoryStream(data)))
                    {
                        EbxAsset asset = ebxReader.ReadAsset<EbxAsset>();
                        if (refEntry.IsAdded)
                        {
                            if (((dynamic)asset.RootObject).Name != refEntry.Name)
                            {
                                ((dynamic)asset.RootObject).Name = refEntry.Name;
                                refEntry.IsDirty = true;
                                refEntry.ModifiedEntry.IsDirty = true;
                            }
                        }
                        refEntry.ModifiedEntry.DataObject = asset;
                        refEntry.ModifiedEntry.Sha1 = Utils.GenerateSha1(data);

                        if (refEntry.IsAdded)
                            refEntry.Type = asset.RootObject.GetType().Name;
                        refEntry.ModifiedEntry.DependentAssets.AddRange(asset.Dependencies);
                    }

                }
                using (EbxReader ebxReader = EbxReader.CreateProjectReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                {
                    EbxAsset asset = ebxReader.ReadAsset<EbxAsset>();
                    if (refEntry.IsAdded)
                    {
                        if (((dynamic)asset.RootObject).Name != refEntry.Name)
                        {
                            ((dynamic)asset.RootObject).Name = refEntry.Name;
                            refEntry.IsDirty = true;
                            refEntry.ModifiedEntry.IsDirty = true;
                        }
                    }
                    refEntry.ModifiedEntry.DataObject = asset;

                    refEntry.ModifiedEntry.Sha1 = Utils.GenerateSha1(refEntry.ModifiedEntry.Data);

                    if (refEntry.IsAdded)
                        refEntry.Type = asset.RootObject.GetType().Name;
                    refEntry.ModifiedEntry.DependentAssets.AddRange(asset.Dependencies);
                }
            }
            else
            {
                using (NativeReader reader = new NativeReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                {
                    byte[] data = reader.ReadToEnd();
                    refEntry.ModifiedEntry.DataObject = ModifiedResource.Read(data);
                    refEntry.ModifiedEntry.Sha1 = Utils.GenerateSha1(data);
                }
                if (modifiedJson.CustomHandlerDependencies != null)
                    refEntry.ModifiedEntry.DependentAssets.AddRange(modifiedJson.CustomHandlerDependencies);
            }
            refEntry.OnModified();
        }

        private void ReadModifiedResJsonFormat(ResAssetEntry resEntry, ResJson resJson, string fileName)
        {
            if (resJson.ModifiedEntry == null)
                return;
            ResJsonModifiedData modifiedJson = resJson.ModifiedEntry;

            string binName = fileName + ".res";
            if (!System.IO.File.Exists(fileName + ".res"))
            {
                App.Logger.LogError(String.Format("Project Loading Error: Modified Res Asset \"{0}\" has no res file \"{1}\". Cannot read modified data", resEntry.Name, binName));
                return;
            }
            Sha1 sha1 = new Sha1(modifiedJson.Sha1);
            if (sha1 == Sha1.Zero)
            {
                if (!resEntry.IsAdded && App.PluginManager.GetCustomHandler((ResourceType)resEntry.ResType) == null)
                {
                    App.Logger.LogError(String.Format("Project Loading Error: Res Asset \"{0}\" uses custom handler for type \"{1}\" which no loaded plugin can handle", resEntry.Name, resEntry.Type));
                    return;
                }
                else if (resEntry.IsAdded)
                {
                    App.Logger.LogError(String.Format("Project Loading Error: Res Asset \"{0}\" uses custom handler which is unsupporting for duplicated assets", resEntry.Name));
                    return;
                }
            }

            resEntry.ModifiedEntry = new ModifiedAssetEntry
            {
                Sha1 = sha1,
                OriginalSize = modifiedJson.OriginalSize,
                ResMeta = StringToByteArray(modifiedJson.ResMeta),
                UserData = modifiedJson.UserData,
            };

            if (sha1 != Sha1.Zero)
            {
                using (NativeReader reader = new NativeReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                {
                    resEntry.ModifiedEntry.Data = reader.ReadToEnd();
                }
            }
            else
            {
                using (NativeReader reader = new NativeReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                {
                    resEntry.ModifiedEntry.DataObject = ModifiedResource.Read(reader.ReadToEnd());
                }
            }
            resEntry.OnModified();
        }

        private void ReadModifiedChkJsonFormat(ChunkAssetEntry chkEntry, ChkJson chkJson, string fileName)
        {
            if (chkJson.ModifiedEntry == null)
                return;
            ChkJsonModifiedData modifiedJson = chkJson.ModifiedEntry;

            string binName = fileName + ".chunk";
            if (!System.IO.File.Exists(fileName + ".chunk"))
            {
                App.Logger.LogError(String.Format("Project Loading Error: Modified Chunk Asset \"{0}\" has no res file \"{1}\". Cannot read modified data", chkEntry.Name, binName));
                return;
            }

            byte[] data = null;
            using (NativeReader reader = new NativeReader(new FileStream(binName, FileMode.Open, FileAccess.Read)))
                data = reader.ReadToEnd();

            chkEntry.ModifiedEntry = new ModifiedAssetEntry
            {
                Sha1 = new Sha1(modifiedJson.Sha1),
                LogicalOffset = modifiedJson.LogicalOffset,
                LogicalSize = modifiedJson.LogicalSize,
                RangeStart = modifiedJson.RangeStart,
                RangeEnd = modifiedJson.RangeEnd,
                FirstMip = modifiedJson.FirstMip,
                H32 = modifiedJson.H32,
                AddToChunkBundle = modifiedJson.AddToChunkBundle,
                UserData = modifiedJson.UserData,
                Data = data
            };

            chkEntry.OnModified();
        }


        private bool LegacyLoad(string inFilename)
        {
            Dictionary<int, AssetEntry> h32map = new Dictionary<int, AssetEntry>();

            DbObject project = null;
            using (DbReader reader = new DbReader(new FileStream(inFilename, FileMode.Open, FileAccess.Read), null))
                project = reader.ReadDbObject();

            uint version = project.GetValue<uint>("version");
            if (version > FormatVersion)
                return false;

            string gameProfile = project.GetValue<string>("gameProfile", ProfilesLibrary.ProfileName);
            if (gameProfile.ToLower() != ProfilesLibrary.ProfileName.ToLower())
                return false;

            creationDate = new DateTime(project.GetValue<long>("creationDate"));
            modifiedDate = new DateTime(project.GetValue<long>("modifiedDate"));
            gameVersion = project.GetValue<uint>("gameVersion");

            DbObject modObj = project.GetValue<DbObject>("modSettings");
            if (modObj != null)
            {
                modSettings.Title = modObj.GetValue("title", "");
                modSettings.Author = modObj.GetValue("author", "");
                modSettings.Category = modObj.GetValue("category", "");
                modSettings.Version = modObj.GetValue("version", "");
                modSettings.Description = modObj.GetValue("description", "");
                modSettings.Icon = modObj.GetValue<byte[]>("icon");
                modSettings.SetScreenshot(0, modObj.GetValue<byte[]>("screenshot1"));
                modSettings.SetScreenshot(1, modObj.GetValue<byte[]>("screenshot2"));
                modSettings.SetScreenshot(2, modObj.GetValue<byte[]>("screenshot3"));
                modSettings.SetScreenshot(3, modObj.GetValue<byte[]>("screenshot4"));
                modSettings.ClearDirtyFlag();
            }

            // load added assets first
            DbObject addedObjs = project.GetValue<DbObject>("added");
            if (addedObjs != null)
            {
                foreach (DbObject sbObj in addedObjs.GetValue<DbObject>("superbundles"))
                    App.AssetManager.AddSuperBundle(sbObj.GetValue<string>("name"));

                foreach (DbObject bObj in addedObjs.GetValue<DbObject>("bundles"))
                {
                    App.AssetManager.AddBundle(
                        bObj.GetValue<string>("name"),
                        (BundleType)bObj.GetValue<int>("type"),
                        App.AssetManager.GetSuperBundleId(bObj.GetValue<string>("superbundle"))
                        );
                }

                foreach (DbObject ebx in addedObjs.GetValue<DbObject>("ebx"))
                {
                    EbxAssetEntry newEntry = new EbxAssetEntry
                    {
                        Name = ebx.GetValue<string>("name"),
                        Guid = ebx.GetValue<Guid>("guid"),
                        Type = ebx.GetValue<string>("type", "UnknownAsset")
                    };
                    App.AssetManager.AddEbx(newEntry);
                }

                foreach (DbObject res in addedObjs.GetValue<DbObject>("res"))
                {
                    ResAssetEntry newEntry = new ResAssetEntry
                    {
                        Name = res.GetValue<string>("name"),
                        ResRid = (ulong)res.GetValue<long>("resRid"),
                        ResType = (uint)res.GetValue<int>("resType")
                    };
                    App.AssetManager.AddRes(newEntry);
                }

                foreach (DbObject chunk in addedObjs.GetValue<DbObject>("chunks"))
                {
                    ChunkAssetEntry newEntry = new ChunkAssetEntry
                    {
                        Id = chunk.GetValue<Guid>("id"),
                        H32 = chunk.GetValue<int>("H32")
                    };
                    App.AssetManager.AddChunk(newEntry);
                }
            }

            if (version < 6)
            {
                // prior to v6. Only chunks could be added
                foreach (DbObject chunk in project.GetValue<DbObject>("chunks"))
                {
                    if (chunk.GetValue<bool>("added"))
                    {
                        ChunkAssetEntry newEntry = new ChunkAssetEntry {Id = chunk.GetValue<Guid>("id")};
                        App.AssetManager.AddChunk(newEntry);
                    }
                }
            }

            DbObject modifiedObjs = project.GetValue<DbObject>("modified") ?? project;

            foreach (DbObject res in modifiedObjs.GetValue<DbObject>("res"))
            {
                ResAssetEntry entry = App.AssetManager.GetResEntry(res.GetValue<string>("name"));
                if (entry == null)
                {
                    // not sure what to do in this scenario
                }

                if (entry != null)
                {
                    LoadLinkedAssets(res, entry, version);
                    if (res.HasValue("data"))
                    {
                        entry.ModifiedEntry = new ModifiedAssetEntry
                        {
                            Sha1 = res.GetValue<Sha1>("sha1"),
                            OriginalSize = res.GetValue<long>("originalSize"),
                            Data = res.GetValue<byte[]>("data"),
                            ResMeta = res.GetValue<byte[]>("meta")
                        };
                    }

                    if (res.HasValue("bundles"))
                    {
                        DbObject bundles = res.GetValue<DbObject>("bundles");
                        foreach (string bundle in bundles)
                            entry.AddedBundles.Add(App.AssetManager.GetBundleId(bundle));
                    }

                    int hash = Fnv1.HashString(entry.Name);
                    if (!h32map.ContainsKey(hash))
                        h32map.Add(hash, entry);
                }
            }
            foreach (DbObject ebx in modifiedObjs.GetValue<DbObject>("ebx"))
            {
                EbxAssetEntry entry = App.AssetManager.GetEbxEntry(ebx.GetValue<string>("name"));
                if (entry == null)
                {
                    // not sure what to do in this scenario
                }

                if (entry != null)
                {
                    LoadLinkedAssets(ebx, entry, version);
                    if (ebx.HasValue("data"))
                    {
                        entry.ModifiedEntry = new ModifiedAssetEntry();
                        byte[] data = ebx.GetValue<byte[]>("data");

                        if (version < 7)
                        {
                            // old ebx stored as compressed byte stream
                            using (CasReader reader = new CasReader(new MemoryStream(data)))
                                data = reader.Read();
                        }

                        using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(data)))
                        {
                            EbxAsset asset = reader.ReadAsset<EbxAsset>();
                            entry.ModifiedEntry.DataObject = asset;
                            entry.ModifiedEntry.Sha1 = Utils.GenerateSha1(entry.ModifiedEntry.Data);
                        }

                        if (ebx.HasValue("transient"))
                            entry.ModifiedEntry.IsTransientModified = true;
                    }

                    if (ebx.HasValue("bundles"))
                    {
                        DbObject bundles = ebx.GetValue<DbObject>("bundles");
                        foreach (string bundle in bundles)
                            entry.AddedBundles.Add(App.AssetManager.GetBundleId(bundle));
                    }

                    int hash = Fnv1.HashString(entry.Name);
                    if (!h32map.ContainsKey(hash))
                        h32map.Add(hash, entry);
                }
            }
            foreach (DbObject chunk in modifiedObjs.GetValue<DbObject>("chunks"))
            {
                Guid id = chunk.GetValue<Guid>("id");
                ChunkAssetEntry entry = App.AssetManager.GetChunkEntry(id);

                if (entry == null)
                {
                    // hack: since chunks are not modified by FrostEd patches, instead a new one
                    // is added when something that uses a chunk is modified. If an existing chunk
                    // from a project is missing, a new one is created, and its linked resource
                    // is used to fill in the bundles (this may fail if a chunk is not meant to be
                    // in any bundles)

                    ChunkAssetEntry newEntry = new ChunkAssetEntry
                    {
                        Id = chunk.GetValue<Guid>("id"),
                        H32 = chunk.GetValue<int>("H32")
                    };
                    App.AssetManager.AddChunk(newEntry);

                    if (h32map.ContainsKey(newEntry.H32))
                    {
                        foreach (int bundleId in h32map[newEntry.H32].Bundles)
                            newEntry.AddToBundle(bundleId);
                    }
                    entry = newEntry;
                }

                if (chunk.HasValue("data"))
                {
                    entry.ModifiedEntry = new ModifiedAssetEntry
                    {
                        Sha1 = chunk.GetValue<Sha1>("sha1"),
                        Data = chunk.GetValue<byte[]>("data"),
                        LogicalOffset = chunk.GetValue<uint>("logicalOffset"),
                        LogicalSize = chunk.GetValue<uint>("logicalSize"),
                        RangeStart = chunk.GetValue<uint>("rangeStart"),
                        RangeEnd = chunk.GetValue<uint>("rangeEnd"),
                        FirstMip = chunk.GetValue<int>("firstMip", -1),
                        H32 = chunk.GetValue<int>("h32", 0),
                        AddToChunkBundle = chunk.GetValue<bool>("addToChunkBundle", true)
                    };
                }
                else
                {
                    entry.FirstMip = chunk.GetValue<int>("firstMip", -1);
                    entry.H32 = chunk.GetValue<int>("h32", 0);
                }

                if (chunk.HasValue("bundles"))
                {
                    DbObject bundles = chunk.GetValue<DbObject>("bundles");
                    foreach (string bundle in bundles)
                        entry.AddedBundles.Add(App.AssetManager.GetBundleId(bundle));
                }
            }

            if (version < 8)
            {
                foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(modifiedOnly: true))
                {
                    foreach (AssetEntry linkedEntry in entry.LinkedAssets)
                    {
                        if (linkedEntry is ChunkAssetEntry)
                            linkedEntry.ModifiedEntry.H32 = Fnv1.HashString(entry.Name.ToLower());
                    }
                }
                foreach (ResAssetEntry entry in App.AssetManager.EnumerateRes(modifiedOnly: true))
                {
                    foreach (AssetEntry linkedEntry in entry.LinkedAssets)
                    {
                        if (linkedEntry is ChunkAssetEntry)
                            linkedEntry.ModifiedEntry.H32 = Fnv1.HashString(entry.Name.ToLower());
                    }
                }
            }

            return true;
        }


        public class ModSettingsJson
        {
            public string Title;
            public string Author;
            public string Category;
            public string Version;
            public string Description;
            public string Link;

            public ModSettingsJson()
            {

            }

            public ModSettingsJson(ModSettings modSettings)
            {
                Title = modSettings.Title;
                Author = modSettings.Author;
                Category = modSettings.Category;
                Version = modSettings.Version;
                Link = modSettings.Link;
            }
        }

        public class BundleJson
        {
            public string OriginalName;
            public string SuperBundle;
            public BundleType BundleType;
            public Guid Blueprint;

            public BundleJson()
            {

            }

            public BundleJson(BundleEntry bEntry)
            {
                OriginalName = bEntry.Name;
                SuperBundle = App.AssetManager.GetSuperBundle(bEntry.SuperBundleId).Name;
                BundleType = bEntry.Type;
                Blueprint = bEntry.Blueprint != null ? bEntry.Blueprint.Guid : Guid.Empty;
            }
        }

        public class EbxJsonModifiedData
        {
            public bool IsTransientModified;
            public string UserData;
            public bool UsesCustomHandlerData;
            public List<Guid> CustomHandlerDependencies;
        }

        public class EbxJson
        {
            public string OriginalName;
            public Guid FileGuid;
            public List<string> AddedBundles;
            //public Dictionary<string, string> LinkedAssets = new Dictionary<string, string>();
            public List<Guid> LinkedEbx = new List<Guid>();
            public List<ulong> LinkedRes = new List<ulong>();
            public List<Guid> LinkedChunk = new List<Guid>();
            public EbxJsonModifiedData ModifiedEntry;

            public EbxJson()
            {

            }
            public EbxJson(EbxAssetEntry refEntry)
            {
                OriginalName = refEntry.Name;
                FileGuid = refEntry.Guid;
                LinkedEbx = refEntry.LinkedAssets.Where(o => o is EbxAssetEntry j).ToList().Select(o => ((EbxAssetEntry)o).Guid).ToList();
                LinkedRes = refEntry.LinkedAssets.Where(o => o is ResAssetEntry j).ToList().Select(o => ((ResAssetEntry)o).ResRid).ToList();
                LinkedChunk = refEntry.LinkedAssets.Where(o => o is ChunkAssetEntry j).ToList().Select(o => ((ChunkAssetEntry)o).Id).ToList();
                AddedBundles = refEntry.AddedBundles.Select(o => App.AssetManager.GetBundleEntry(o).Name).ToList();
                //LinkedAssets = refEntry.LinkedAssets.ToDictionary(o => o.Name, o => o.AssetType);
                if (refEntry.HasModifiedData)
                {
                    ModifiedEntry = new EbxJsonModifiedData();
                    ModifiedEntry.IsTransientModified = refEntry.ModifiedEntry.IsTransientModified;
                    ModifiedEntry.UserData = refEntry.ModifiedEntry.UserData;
                    ModifiedEntry.UsesCustomHandlerData = (refEntry.ModifiedEntry.DataObject as ModifiedResource) != null;
                    ModifiedEntry.CustomHandlerDependencies = refEntry.EnumerateDependencies().Where(o => !refEntry.DependentAssets.Contains(o)).ToList();
                }
            }


        }

        public class ResJsonModifiedData
        {
            public string Sha1;
            public long OriginalSize;
            public string ResMeta;
            public string UserData;
        }

        public class ResJson
        {
            public string OriginalName;
            public ulong ResRid;
            public uint ResType;
            public string ResMeta;
            public List<string> AddedBundles;
            public ResJsonModifiedData ModifiedEntry;
            public List<Guid> LinkedEbx = new List<Guid>();
            public List<ulong> LinkedRes = new List<ulong>();
            public List<Guid> LinkedChunk = new List<Guid>();

            //public Dictionary<string, string> LinkedAssets = new Dictionary<string, string>();

            public ResJson()
            {

            }
            public ResJson(ResAssetEntry resEntry)
            {
                OriginalName = resEntry.Name;
                ResRid = resEntry.ResRid;
                ResType = resEntry.ResType;
                if (resEntry.ResMeta != null)
                    ResMeta = BitConverter.ToString(resEntry.ResMeta);
                LinkedEbx = resEntry.LinkedAssets.Where(o => o is EbxAssetEntry j).ToList().Select(o => ((EbxAssetEntry)o).Guid).ToList();
                LinkedRes = resEntry.LinkedAssets.Where(o => o is ResAssetEntry j).ToList().Select(o => ((ResAssetEntry)o).ResRid).ToList();
                LinkedChunk = resEntry.LinkedAssets.Where(o => o is ChunkAssetEntry j).ToList().Select(o => ((ChunkAssetEntry)o).Id).ToList();
                AddedBundles = resEntry.AddedBundles.Select(o => App.AssetManager.GetBundleEntry(o).Name).ToList();
                //LinkedAssets = resEntry.LinkedAssets.ToDictionary(o => o.Name, o => o.AssetType);

                if (resEntry.HasModifiedData)
                {
                    ModifiedEntry = new ResJsonModifiedData();
                    ModifiedEntry.Sha1 = resEntry.ModifiedEntry.Sha1.ToString();
                    ModifiedEntry.OriginalSize = resEntry.ModifiedEntry.OriginalSize;
                    if (resEntry.ModifiedEntry.ResMeta != null)
                        ModifiedEntry.ResMeta = BitConverter.ToString(resEntry.ModifiedEntry.ResMeta);
                    ModifiedEntry.UserData = resEntry.ModifiedEntry.UserData;
                }
            }

        }

        public class ChkJsonModifiedData
        {
            public string Sha1;
            public uint LogicalOffset;
            public uint LogicalSize;
            public uint RangeStart;
            public uint RangeEnd;
            public int FirstMip;
            public int H32;
            public bool AddToChunkBundle;
            public string UserData;
        }

        public class ChkJson
        {
            public int H32;
            public List<string> AddedBundles;
            public ChkJsonModifiedData ModifiedEntry;

            public ChkJson()
            {

            }
            public ChkJson(ChunkAssetEntry chkEntry)
            {
                H32 = chkEntry.H32;
                AddedBundles = chkEntry.AddedBundles.Select(o => App.AssetManager.GetBundleEntry(o).Name).ToList();

                if (chkEntry.HasModifiedData)
                {
                    ModifiedEntry = new ChkJsonModifiedData();
                    ModifiedEntry.Sha1 = chkEntry.ModifiedEntry.Sha1.ToString();
                    ModifiedEntry.LogicalOffset = chkEntry.ModifiedEntry.LogicalOffset;
                    ModifiedEntry.LogicalSize = chkEntry.ModifiedEntry.LogicalSize;
                    ModifiedEntry.RangeStart = chkEntry.ModifiedEntry.RangeStart;
                    ModifiedEntry.RangeEnd = chkEntry.ModifiedEntry.RangeEnd;
                    ModifiedEntry.FirstMip = chkEntry.ModifiedEntry.FirstMip;
                    ModifiedEntry.H32 = chkEntry.ModifiedEntry.H32;
                    ModifiedEntry.AddToChunkBundle = chkEntry.ModifiedEntry.AddToChunkBundle;
                    ModifiedEntry.UserData = chkEntry.ModifiedEntry.UserData;
                }
            }

        }

    }
}
