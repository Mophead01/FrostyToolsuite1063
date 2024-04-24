﻿using AtlasTexturePlugin;
using DuplicationPlugin.Windows;
using Frosty.Core;
using Frosty.Core.Viewport;
using Frosty.Core.Windows;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using MeshSetPlugin.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DuplicationPlugin
{
    public class DuplicationTool
    {
        #region --Extensions--

        public class SoundWaveExtension : DuplicateAssetExtension
        {
            public override string AssetType => "SoundWaveAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                EbxAssetEntry refEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                EbxAsset refAsset = App.AssetManager.GetEbx(refEntry);
                dynamic refRoot = refAsset.RootObject;
                
                foreach (dynamic chkref in refRoot.Chunks)
                {
                    ChunkAssetEntry soundChunk = App.AssetManager.GetChunkEntry(chkref.ChunkId);
                    ChunkAssetEntry newSoundChunk = DuplicateChunk(soundChunk);
                    chkref.ChunkId = newSoundChunk.Id;
                }
                App.AssetManager.ModifyEbx(refEntry.Name, refAsset);

                return refEntry;
            }
        }

        public class PathfindingExtension : DuplicateAssetExtension
        {

            public override string AssetType => "PathfindingBlobAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                EbxAssetEntry refEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                EbxAsset refAsset = App.AssetManager.GetEbx(refEntry);
                dynamic refRoot = refAsset.RootObject;
                ChunkAssetEntry pathfindingChunk = App.AssetManager.GetChunkEntry(refRoot.Blob.BlobId);
                if (pathfindingChunk != null)
                {
                    ChunkAssetEntry newPathfindingChunk = DuplicateChunk(pathfindingChunk);
                    if (newPathfindingChunk != null)
                    {
                        refRoot.Blob.BlobId = pathfindingChunk.Id;
                    }
                }

                App.AssetManager.ModifyEbx(refEntry.Name, refAsset);

                return refEntry;
            }
        }

        #region --Bundles--

        public class BlueprintBundleExtension : DuplicateAssetExtension
        {
            public override string AssetType => "BlueprintBundle";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                // Add new bundle
                BundleEntry oldBundle = App.AssetManager.GetBundleEntry(newEntry.AddedBundles[0]);
                BundleEntry newBundle = App.AssetManager.AddBundle("win32/" + (ProfilesLibrary.DataVersion == (int)ProfileVersion.StarWarsBattlefrontII ? newName : newName.ToLower()), BundleType.BlueprintBundle, oldBundle.SuperBundleId);

                newEntry.AddedBundles.Clear();
                newEntry.AddedBundles.Add(App.AssetManager.GetBundleId(newBundle));

                newBundle.Blueprint = newEntry;

                return newEntry;
            }
        }

        public class SubWorldDataExtension : DuplicateAssetExtension
        {
            public override string AssetType => "SubWorldData";
            public static Dictionary<string, List<string>> ForcedBundleTransfers
            {
                get
                {
                    string strList = Config.Get<string>("ForcedBundleTransfers", null, ConfigScope.Game);
                    Dictionary<string, List<string>> returnList = new Dictionary<string, List<string>>();
                    if (strList == null)
                        return returnList;
                    foreach (string subStr in strList.Split('$'))
                    {
                        if (subStr.Split(':').Count() < 2)
                            continue;
                        string assetName = subStr.Split(':')[0];
                        returnList.Add(assetName, new List<string>());
                        foreach (string subStr2 in subStr.Split(':')[1].Split('£'))
                            returnList[assetName].Add(subStr2);
                    }
                    return returnList;
                }
                set
                {
                    Config.Add("ForcedBundleTransfers", string.Join("$", value.ToList().Select(pair => $"{pair.Key}:{string.Join("£", pair.Value)}")), ConfigScope.Game);
                }
            }

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                // Add new bundle
                BundleEntry oldBundle = App.AssetManager.GetBundleEntry(newEntry.AddedBundles[0]);
                BundleEntry newBundle = App.AssetManager.AddBundle("win32/" + (ProfilesLibrary.DataVersion == (int)ProfileVersion.StarWarsBattlefrontII ? newName : newName.ToLower()), BundleType.SubLevel, oldBundle.SuperBundleId);
                if (!ForcedBundleTransfers.ContainsKey(newBundle.Name))
                {
                    ForcedBundleTransfers = new Dictionary<string, List<string>>(ForcedBundleTransfers) { { newBundle.Name, new List<string>() { oldBundle.Name } } };
                    Config.Save();
                }

                newEntry.AddedBundles.Clear();
                newEntry.AddedBundles.Add(App.AssetManager.GetBundleId(newBundle));

                newBundle.Blueprint = newEntry;
                newEntry.LinkAsset(entry);

                if (TypeLibrary.IsSubClassOf(entry.Type, "LevelData"))
                {
                    EbxAssetEntry descCopyEntry = App.AssetManager.GetEbxEntry($"{entry.Name.ToLower()}/description");
                    if (descCopyEntry == null)
                    {
                        App.Logger.LogError($"Cannot find description asset to dupe for {entry.Name}");
                        return newEntry;
                    }
                    string descNewName = $"{newName.ToLower()}/description";
                    EbxAssetEntry descNewEntry = base.DuplicateAsset(descCopyEntry, descNewName, false, null);
                    EbxAsset descNewAsset = App.AssetManager.GetEbx(descNewEntry);
                    dynamic descNewRoot = descNewAsset.RootObject;

                    EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);
                    descNewRoot.LevelGuid = newAsset.RootInstanceGuid;

                    descNewRoot.LevelName = new CString(newName);
                    foreach(dynamic bunInfo in descNewRoot.Bundles)
                    {
                        if (bunInfo.Name.ToString() == entry.Name)
                            bunInfo.Name = new CString(newName);
                    }

                    EbxAssetEntry levelEntry = App.AssetManager.GetEbxEntry("LevelListReport");
                    EbxAsset levelAsset = App.AssetManager.GetEbx(levelEntry);
                    dynamic levelRoot = levelAsset.RootObject;

                    levelRoot.BuiltLevels.Add(descNewEntry.Guid);
                    App.AssetManager.ModifyEbx(levelEntry.Name, levelAsset);
                    App.AssetManager.ModifyEbx(descNewEntry.Name, descNewAsset);
                }

                return newEntry;
            }
        }

        #endregion

        #region --Meshes--

        public class ClothWrappingExtension : DuplicateAssetExtension
        {
            public override string AssetType => "ClothWrappingAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);
                EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);
                dynamic newRoot = newAsset.RootObject;

                // Duplicate the res
                ResAssetEntry resEntry = App.AssetManager.GetResEntry(newRoot.ClothWrappingAssetResource);
                ResAssetEntry newResEntry = DuplicateRes(resEntry, newEntry.Name, ResourceType.EAClothEntityData);

                // Update the ebx
                newRoot.ClothWrappingAssetResource = newResEntry.ResRid;
                newEntry.LinkAsset(newResEntry);

                // Modify the ebx
                App.AssetManager.ModifyEbx(newEntry.Name, newAsset);

                return newEntry;
            }
        }

        public class ClothExtension : DuplicateAssetExtension
        {
            public override string AssetType => "ClothAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);
                EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);
                dynamic newRoot = newAsset.RootObject;

                // Duplicate the res
                ResAssetEntry resEntry = App.AssetManager.GetResEntry(newRoot.ClothAssetResource);
                ResAssetEntry newResEntry = DuplicateRes(resEntry, newEntry.Name, ResourceType.EAClothAssetData);

                // Update the ebx
                newRoot.ClothAssetResource = newResEntry.ResRid;
                newEntry.LinkAsset(newResEntry);

                // Modify the ebx
                App.AssetManager.ModifyEbx(newEntry.Name, newAsset);

                return newEntry;
            }
        }

        public class ObjectVariationExtension : DuplicateAssetExtension
        {
            public override string AssetType => "ObjectVariation";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                EbxAssetEntry newAssetEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                //Get the ebx and root object from our duped entry
                EbxAsset newEbx = App.AssetManager.GetEbx(newAssetEntry);
                dynamic newRootObject = newEbx.RootObject;

                // Get the ebx and root object from the original entry
                EbxAsset oldEbx = App.AssetManager.GetEbx(entry);
                dynamic oldRootObject = oldEbx.RootObject;

                // The NameHash needs to be the 32 bit Fnv1 of the lowercased name
                newRootObject.NameHash = (uint)Utils.HashString(newName, true);

                // SWBF2 has fancy res files for object variations, we need to dupe these. Other games just need the namehash
                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII))
                {
                    // Get the original name hash, this will be useful for when we change it
                    uint nameHash = oldRootObject.NameHash;

                    // find a Mesh Variation entry with our NameHash
                    MeshVariation meshVariation = MeshVariationDb.FindVariations(nameHash, true).First();

                    // Get meshSet
                    EbxAssetEntry meshEntry = App.AssetManager.GetEbxEntry(meshVariation.MeshGuid);
                    EbxAsset meshAsset = App.AssetManager.GetEbx(meshEntry);
                    dynamic meshRoot = meshAsset.RootObject;
                    ResAssetEntry meshRes = App.AssetManager.GetResEntry(meshRoot.MeshSetResource);
                    MeshSet meshSet = App.AssetManager.GetResAs<MeshSet>(meshRes);

                    foreach (object matObject in newEbx.RootObjects) // For each material in the new variation
                    {
                        // Check if this is actually a material
                        if (TypeLibrary.IsSubClassOf(matObject.GetType(), "MeshMaterialVariation") && ((dynamic)matObject).Shader.TextureParameters.Count == 0)
                        {
                            // Get the material object as a dynamic, so we can see and edit its properties
                            dynamic matProperties = matObject as dynamic;

                            AssetClassGuid guid = matProperties.GetInstanceGuid();

                            foreach (MeshVariationMaterial mvm in meshVariation.Materials) // For each material in the original assets MVDB Entry
                            {
                                if (mvm.MaterialVariationClassGuid == guid.ExportedGuid) //If it has the same guid
                                {
                                    // We then use its texture params as the texture params in the variation
                                    foreach (dynamic texParam in (dynamic)mvm.TextureParameters)
                                    {
                                        matProperties.Shader.TextureParameters.Add(texParam);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Dupe sbd
                    ResAssetEntry resEntry = App.AssetManager.GetResEntry(entry.Name.ToLower() + "/" + meshEntry.Filename + "_" + (uint)Utils.HashString(meshEntry.Name, true) + "/shaderblocks_variation/blocks");
                    ResAssetEntry newResEntry = DuplicateRes(resEntry, newName.ToLower() + "/" + meshEntry.Filename + "_" + (uint)Utils.HashString(meshEntry.Name, true) + "/shaderblocks_variation/blocks", ResourceType.ShaderBlockDepot);
                    ShaderBlockDepot newShaderBlockDepot = App.AssetManager.GetResAs<ShaderBlockDepot>(newResEntry);

                    for (int i = 0; i < newShaderBlockDepot.ResourceCount; i++)
                    {
                        ShaderBlockResource res = newShaderBlockDepot.GetResource(i);
                        if (!(res is MeshParamDbBlock))
                            res.ChangeHash(newRootObject.NameHash);
                    }

                    // Change the references in the sbd
                    for (int lod = 0; lod < meshSet.Lods.Count; lod++)
                    {
                        ShaderBlockEntry sbEntry = newShaderBlockDepot.GetSectionEntry(lod);
                        ShaderBlockMeshVariationEntry sbMvEntry = newShaderBlockDepot.GetResource(sbEntry.Index + 1) as ShaderBlockMeshVariationEntry;

                        sbEntry.SetHash(meshSet.NameHash, newRootObject.NameHash, lod);
                        sbMvEntry.SetHash(meshSet.NameHash, newRootObject.NameHash, lod);
                    }

                    App.AssetManager.ModifyRes(newResEntry.Name, newShaderBlockDepot);
                    newAssetEntry.LinkAsset(newResEntry);
                }

                App.Logger.Log("Duped {0} with a namehash of {1}", newAssetEntry.Filename, newRootObject.NameHash.ToString());

                App.AssetManager.ModifyEbx(newName, newEbx);
                return newAssetEntry;
            }
        }

        public class MeshExtension : DuplicateAssetExtension
        {
            public override string AssetType => "MeshAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                //2017 battlefront meshes always have lowercase names. This doesn't apply to all games, but its still safer to do so
                newName = newName.ToLower();

                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);
                EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);

                // Get the original asset root object data
                EbxAsset asset = App.AssetManager.GetEbx(entry);
                dynamic meshProperties = asset.RootObject;
                dynamic newMeshProperties = newAsset.RootObject;

                //Get the original res entry and duplicate it
                ResAssetEntry resAsset = App.AssetManager.GetResEntry(meshProperties.MeshSetResource);
                ResAssetEntry newResAsset = DuplicateRes(resAsset, newName, ResourceType.MeshSet);

                //Since this is a mesh we need to get the meshSet for the duplicated entry and set it up
                MeshSet meshSet = App.AssetManager.GetResAs<MeshSet>(newResAsset);
                meshSet.FullName = newResAsset.Name;

                //Go through all of the lods and duplicate their chunks
                foreach (MeshSetLod lod in meshSet.Lods)
                {
                    lod.Name = newResAsset.Name;
                    //Double check that the lod actually has a chunk id. If it doesn't this means the data is inline and we don't need to worry
                    if (lod.ChunkId != Guid.Empty)
                    {
                        //Get the original chunk and dupe it
                        ChunkAssetEntry chunk = App.AssetManager.GetChunkEntry(lod.ChunkId);
                        ChunkAssetEntry newChunk = DuplicateChunk(chunk);

                        //Now set the params for the lod
                        lod.ChunkId = newChunk.Id;

                        //Link the res and chunk
                        newResAsset.LinkAsset(newChunk);
                    }
                }

                //Set our new mesh's properties
                newMeshProperties.MeshSetResource = newResAsset.ResRid;
                newMeshProperties.NameHash = (uint)Utils.HashString(newName);

                //Link the res and ebx
                newEntry.LinkAsset(newResAsset);

                // Stuff for SBDs since SWBF2 is weird
                if (ProfilesLibrary.IsLoaded(ProfileVersion.StarWarsBattlefrontII))
                {
                    // Restore the texture params
                    MeshVariation meshVariation = MeshVariationDb.GetVariations(entry.Guid).Variations[0];

                    foreach (object matObject in newAsset.Objects) // For each material in the new variation
                    {
                        // Check if this is actually a material
                        if (TypeLibrary.IsSubClassOf(matObject.GetType(), "MeshMaterial") && ((dynamic)matObject).Shader.TextureParameters.Count == 0)
                        {
                            // Get the material object as a dynamic, so we can see and edit its properties
                            dynamic matProperties = matObject as dynamic;

                            AssetClassGuid guid = matProperties.GetInstanceGuid();

                            foreach (MeshVariationMaterial mvm in meshVariation.Materials) // For each material in the original assets MVDB Entry
                            {
                                if (mvm.MaterialGuid == guid.ExportedGuid) // If it has the same guid
                                {
                                    // We then use its texture params as the texture params in the variation
                                    foreach (dynamic texParam in (dynamic)mvm.TextureParameters)
                                    {
                                        matProperties.Shader.TextureParameters.Add(texParam);
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Duplicate the sbd
                    ResAssetEntry oldShaderBlock = App.AssetManager.GetResEntry(entry.Name.ToLower() + "_mesh/blocks");
                    ResAssetEntry newShaderBlock = DuplicateRes(oldShaderBlock, newResAsset.Name + "_mesh/blocks", ResourceType.ShaderBlockDepot);
                    ShaderBlockDepot newShaderBlockDepot = App.AssetManager.GetResAs<ShaderBlockDepot>(newShaderBlock);

                    // TODO: hacky way to generate unique hashes
                    for (int i = 0; i < newShaderBlockDepot.ResourceCount; i++)
                    {
                        ShaderBlockResource res = newShaderBlockDepot.GetResource(i);
                        res.ChangeHash(meshSet.NameHash);
                    }

                    // Change the references in the sbd
                    for (int lod = 0; lod < meshSet.Lods.Count; lod++)
                    {
                        ShaderBlockEntry sbEntry = newShaderBlockDepot.GetSectionEntry(lod);
                        ShaderBlockMeshVariationEntry sbMvEntry = newShaderBlockDepot.GetResource(sbEntry.Index + 1) as ShaderBlockMeshVariationEntry;

                        // calculate new entry hash
                        sbEntry.SetHash(meshSet.NameHash, 0, lod);
                        sbMvEntry.SetHash(meshSet.NameHash, 0, lod);

                        // Update the mesh guid
                        for (int section = 0; section < meshSet.Lods[lod].Sections.Count; section++)
                        {
                            MeshParamDbBlock mesh = sbEntry.GetMeshParams(section);
                            mesh.MeshAssetGuid = newAsset.RootInstanceGuid;
                        }
                    }

                    App.AssetManager.ModifyRes(newShaderBlock.Name, newShaderBlockDepot);

                    newResAsset.LinkAsset(newShaderBlock);
                }

                //Modify the res and ebx
                App.AssetManager.ModifyRes(newResAsset.Name, meshSet);
                App.AssetManager.ModifyEbx(newName, newAsset);

                return newEntry;
            }
        }

        #endregion

        #region --Textures--

        public class AtlasTextureExtension : DuplicateAssetExtension
        {
            public override string AssetType => "AtlasTextureAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);
                EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);

                // Get the original asset root object data
                EbxAsset asset = App.AssetManager.GetEbx(entry);
                dynamic textureAsset = asset.RootObject;

                // Get the original chunk and res entries
                ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
                AtlasTexture texture = App.AssetManager.GetResAs<AtlasTexture>(resEntry);
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(texture.ChunkId);

                // Duplicate the chunk
                ChunkAssetEntry newChunkEntry = DuplicateChunk(chunkEntry);

                // Duplicate the res
                ResAssetEntry newResEntry = DuplicateRes(resEntry, newName, ResourceType.AtlasTexture);
                ((dynamic)newAsset.RootObject).Resource = newResEntry.ResRid;
                AtlasTexture newTexture = App.AssetManager.GetResAs<AtlasTexture>(newResEntry);

                // Set the data in the Atlas Texture
                newTexture.SetData(texture.Width, texture.Height, newChunkEntry.Id, App.AssetManager);
                newTexture.SetNameHash((uint)Utils.HashString($"Output/Win32/{newResEntry.Name}.res", true));

                // Link the newly duplicated ebx, chunk, and res entries together
                newResEntry.LinkAsset(newChunkEntry);
                newEntry.LinkAsset(newResEntry);

                // Modify ebx and res
                App.AssetManager.ModifyEbx(newEntry.Name, newAsset);
                App.AssetManager.ModifyRes(newResEntry.Name, newTexture);

                return newEntry;
            }
        }

        public class SvgImageExtension : DuplicateAssetExtension
        {
            public override string AssetType => "SvgImage";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                EbxAssetEntry refEntry = base.DuplicateAsset(entry, newName, createNew, newType);

                EbxAsset refAsset = App.AssetManager.GetEbx(refEntry);
                dynamic refRoot = refAsset.RootObject;

                ResAssetEntry resEntry = App.AssetManager.GetResEntry(refRoot.Resource);

                ResAssetEntry newResEntry = DuplicateRes(resEntry, refEntry.Name, ResourceType.SvgImage);
                if (newResEntry != null)
                {
                    refRoot.Resource = newResEntry.ResRid;
                    App.AssetManager.ModifyEbx(refEntry.Name, refAsset);
                }

                return refEntry;
            }
        }

        public class TextureExtension : DuplicateAssetExtension
        {
            public override string AssetType => "TextureBaseAsset";

            public override EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                // Duplicate the ebx
                EbxAssetEntry newEntry = base.DuplicateAsset(entry, newName, createNew, newType);
                EbxAsset newAsset = App.AssetManager.GetEbx(newEntry);

                // Get the original asset root object data
                EbxAsset asset = App.AssetManager.GetEbx(entry);
                dynamic textureAsset = asset.RootObject;

                // Get the original chunk and res entries
                ResAssetEntry resEntry = App.AssetManager.GetResEntry(textureAsset.Resource);
                Texture texture = App.AssetManager.GetResAs<Texture>(resEntry);
                ChunkAssetEntry chunkEntry = App.AssetManager.GetChunkEntry(texture.ChunkId);

                // Duplicate the chunk
                ChunkAssetEntry newChunkEntry = DuplicateChunk(chunkEntry);

                // Duplicate the res
                ResAssetEntry newResEntry = DuplicateRes(resEntry, newName, ResourceType.Texture);
                ((dynamic)newAsset.RootObject).Resource = newResEntry.ResRid;
                Texture newTexture = App.AssetManager.GetResAs<Texture>(newResEntry);
                newTexture.ChunkId = newChunkEntry.Id;
                newTexture.AssetNameHash = (uint)Utils.HashString(newResEntry.Name, true);

                // Link the newly duplicates ebx, chunk, and res entries together
                newResEntry.LinkAsset(newChunkEntry);
                newEntry.LinkAsset(newResEntry);

                // Modify ebx and res
                App.AssetManager.ModifyEbx(newEntry.Name, newAsset);
                App.AssetManager.ModifyRes(newResEntry.Name, newTexture);

                return newEntry;
            }
        }

        #endregion

        public class DuplicateAssetExtension
        {
            public virtual string AssetType => null;

            public virtual EbxAssetEntry DuplicateAsset(EbxAssetEntry entry, string newName, bool createNew, Type newType)
            {
                EbxAsset asset = App.AssetManager.GetEbx(entry);
                EbxAsset newAsset = null;

                if (createNew)
                {
                    newAsset = new EbxAsset(TypeLibrary.CreateObject(newType.Name));
                }
                else
                {
                    using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort))
                    {
                        writer.WriteAsset(asset);
                        byte[] buf = writer.ToByteArray();
                        using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                            newAsset = reader.ReadAsset<EbxAsset>();
                    }
                }

                newAsset.SetFileGuid(Guid.NewGuid());

                dynamic obj = newAsset.RootObject;
                obj.Name = newName;

                AssetClassGuid guid;
                if (asset.RootInstanceGuid == entry.Guid && !createNew)
                    guid = new AssetClassGuid(newAsset.FileGuid, -1);
                else
                    guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
                obj.SetInstanceGuid(guid);

                EbxAssetEntry newEntry = App.AssetManager.AddEbx(newName, newAsset);

                newEntry.AddedBundles.AddRange(entry.EnumerateBundles());
                newEntry.ModifiedEntry.DependentAssets.AddRange(newAsset.Dependencies);

                return newEntry;
            }
        }

        #endregion

        #region --Chunk and res support--

        public static ChunkAssetEntry DuplicateChunk(ChunkAssetEntry entry, Texture texture = null)
        {
            byte[] random = new byte[16];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            while (true)
            {
                rng.GetBytes(random);

                random[15] |= 1;

                if (App.AssetManager.GetChunkEntry(new Guid(random)) == null)
                {
                    break;
                }
                else
                {
                    App.Logger.Log("Randomised onto old guid: " + random.ToString());
                }
            }
            Guid newGuid;
            using (NativeReader reader = new NativeReader(App.AssetManager.GetChunk(entry)))
            {
                newGuid = App.AssetManager.AddChunk(reader.ReadToEnd(), new Guid(random), texture, entry.EnumerateBundles().ToArray());
            }

            ChunkAssetEntry newEntry = App.AssetManager.GetChunkEntry(newGuid);

            App.Logger.Log(string.Format("Duped chunk {0} to {1}", entry.Name, newGuid));
            return newEntry;
        }

        public static ResAssetEntry DuplicateRes(ResAssetEntry entry, string name, ResourceType resType)
        {
            if (App.AssetManager.GetResEntry(name) == null)
            {
                ResAssetEntry newEntry;
                using (NativeReader reader = new NativeReader(App.AssetManager.GetRes(entry)))
                {
                    newEntry = App.AssetManager.AddRes(name, resType, entry.ResMeta, reader.ReadToEnd(), entry.EnumerateBundles().ToArray());
                }

                App.Logger.Log(string.Format("Duped res {0} to {1}", entry.Name, newEntry.Name));
                return newEntry;
            }
            else
            {
                App.Logger.Log(name + " already has a res files");
                return null;
            }
        }

        #endregion

        public class DuplicateContextMenuItem : DataExplorerContextMenuExtension
        {
            private Dictionary<string, DuplicateAssetExtension> extensions = new Dictionary<string, DuplicateAssetExtension>();

            public DuplicateContextMenuItem()
            {
                foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
                {
                    if (type.IsSubclassOf(typeof(DuplicateAssetExtension)))
                    {
                        var extension = (DuplicateAssetExtension)Activator.CreateInstance(type);
                        extensions.Add(extension.AssetType, extension);
                    }
                }
                extensions.Add("null", new DuplicateAssetExtension());
            }

            public override string ContextItemName => "Duplicate";

            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyEditor;component/Images/Add.png") as ImageSource;

            public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
            {
                EbxAssetEntry entry = App.SelectedAsset as EbxAssetEntry;

                DuplicateAssetWindow win = new DuplicateAssetWindow(entry);
                if (win.ShowDialog() == false)
                    return;

                string newName = win.SelectedPath + "/" + win.SelectedName;
                newName = newName.Trim('/');

                string sourceName = entry.Name;
                if (sourceName.ToLower() == sourceName)
                    newName = newName.ToLower();
                else
                {
                    List<string> sourceNameSplit = sourceName.Split('/').ToList();
                    List<string> newNameSplit = newName.Split('/').ToList();
                    string newNameConstructor = "";
                    for (int i = 0; i < sourceNameSplit.Count(); i++)
                    {
                        if (sourceNameSplit[i].ToLower() == newNameSplit[i].ToLower())
                            newNameConstructor += $"{sourceNameSplit[i]}/";
                        else
                            break;
                    }
                    newNameConstructor += newName.Substring(newNameConstructor.Length);
                    newName = newNameConstructor;
                }

                Type newType = win.SelectedType;
                if (win.IsToBeRenamed == false)
                {
                    FrostyTaskWindow.Show("Duplicating asset", "", (task) =>
                    {
                        if (!MeshVariationDb.IsLoaded)
                            MeshVariationDb.LoadVariations(task);

                        try
                        {
                            string key = "null";
                            foreach (string typekey in extensions.Keys)
                            {
                                if (TypeLibrary.IsSubClassOf(entry.Type, typekey))
                                {
                                    key = typekey;
                                    break;
                                }
                            }

                            task.Update("Duplicating asset...");
                            extensions[key].DuplicateAsset(entry, newName, newType != null, newType);
                        }
                        catch (Exception e)
                        {
                            App.Logger.Log($"Failed to duplicate {entry.Name}");
                        }
                    });
                }
                else
                {
                    string OldName = entry.Name;
                    newName = newName.Trim('/');
                    FrostyTaskWindow.Show("Renaming asset", "", (task) =>
                    {
                        EbxAsset asset = App.AssetManager.GetEbx(entry);
                        List<int> bundles = entry.EnumerateBundles().ToList();
                        List<AssetEntry> linkedAssets = entry.LinkedAssets.ToList();
                        EbxAsset newAsset = null;
                        using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort | EbxWriteFlags.IncludeTransient))
                        {
                            writer.WriteAsset(asset);
                            byte[] buf = writer.ToByteArray();
                            try
                            {
                                using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                                    newAsset = reader.ReadAsset<EbxAsset>();
                            }
                            catch
                            {
                                newAsset = null;
                                App.Logger.Log("Error reading " + entry.Name + ". Please try again");
                            }
                        }
                        if (newAsset != null)
                        {
                            dynamic obj = newAsset.RootObject;
                            obj.Name = newName;
                            App.AssetManager.RevertAsset(entry);
                            if (App.AssetManager.GetEbxEntry(newAsset.FileGuid) != null)
                            {
                                App.Logger.Log(OldName + " has the same external guid as " + App.AssetManager.GetEbxEntry(newAsset.FileGuid).Name + ", randomising external and internal guid.");
                                newAsset.SetFileGuid(Guid.NewGuid());
                                AssetClassGuid guid = new AssetClassGuid(Utils.GenerateDeterministicGuid(newAsset.Objects, (Type)obj.GetType(), newAsset.FileGuid), -1);
                                obj.SetInstanceGuid(guid);
                            }
                            EbxAssetEntry newEntry =  App.AssetManager.AddEbx(newName, newAsset);
                            foreach(int bunId in bundles)
                                newEntry.AddToBundle(bunId);
                            foreach (AssetEntry linkedAsset in linkedAssets)
                                newEntry.LinkAsset(linkedAsset);
                            App.Logger.Log(OldName + " has been renamed to " + newName);
                        }
                        foreach(string bunName in new List<string> { "win32/" + OldName, "win32/" + OldName.ToLower()})
                        {
                            int bunId = App.AssetManager.GetBundleId(bunName);
                            if (bunId != -1 && App.AssetManager.GetBundleEntry(bunId).Added)
                                App.AssetManager.GetBundleEntry(bunId).Name = "win32/" + newName;

                        }
                    });
                }
                App.EditorWindow.DataExplorer.RefreshAll();
            });
        }
        public class FilterTypeContextMenuItem : DataExplorerContextMenuExtension
        {
            public override string ContextItemName => "Copy Type";

            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;Component/Images/Copy.png") as ImageSource;

            public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
            {
                EbxAssetEntry entry = App.SelectedAsset as EbxAssetEntry;
                try
                {
                    Clipboard.SetText("type:" + entry.Type);
                    App.Logger.Log("Copied type:" + entry.Type + " to clipboard");
                }
                catch 
                {
                    App.Logger.LogWarning("Could not set Clipboard value. Please retry");
                }
            });
        }

        public static EbxAssetEntry copyEntry = null;

        public class CopyDataContextMenuItem : DataExplorerContextMenuExtension
        {
            public override string ContextItemName => "Copy Data";

            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;Component/Images/Copy.png") as ImageSource;

            public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
            {
                copyEntry = App.SelectedAsset as EbxAssetEntry;
            });
        }

        public class PasteDataContextMenuItem : DataExplorerContextMenuExtension
        {
            public override string ContextItemName => "Paste Data";

            public override ImageSource Icon => new ImageSourceConverter().ConvertFromString("pack://application:,,,/FrostyCore;Component/Images/Paste.png") as ImageSource;

            public override RelayCommand ContextItemClicked => new RelayCommand((o) =>
            {
                EbxAssetEntry pasteEntry = App.SelectedAsset as EbxAssetEntry;
                if (copyEntry == null)
                    App.Logger.LogError("No asset data to copy");
                else if (pasteEntry == null)
                    App.Logger.LogError("No asset select to paste upon");
                else if (pasteEntry.Type != copyEntry.Type)
                    App.Logger.LogError(String.Format("Cannot paste a {0} over a {1}", copyEntry.Type, pasteEntry.Type));
                else
                {
                    EbxAsset copyAsset = App.AssetManager.GetEbx(copyEntry);
                    EbxAsset pasteAsset = App.AssetManager.GetEbx(pasteEntry);
                    Guid rootGuid = pasteAsset.RootInstanceGuid;
                    using (EbxBaseWriter writer = EbxBaseWriter.CreateWriter(new MemoryStream(), EbxWriteFlags.DoNotSort | EbxWriteFlags.IncludeTransient))
                    {
                        writer.WriteAsset(copyAsset);
                        byte[] buf = writer.ToByteArray();
                        try
                        {
                            using (EbxReader reader = EbxReader.CreateReader(new MemoryStream(buf)))
                                pasteAsset = reader.ReadAsset<EbxAsset>();
                        }
                        catch
                        {
                            App.Logger.Log("Error copying " + copyEntry.Name + ". Please try again");
                        }
                    }
                    pasteAsset.SetFileGuid(pasteEntry.Guid);
                    ((dynamic)pasteAsset.RootObject).Name = pasteEntry.Name;
                    ((dynamic)pasteAsset.RootObject).SetInstanceGuid(new AssetClassGuid(rootGuid, -1) { });
                    App.AssetManager.ModifyEbx(pasteEntry.Name, pasteAsset);
                }
                App.EditorWindow.DataExplorer.RefreshAll();
            });
        }
    }
}
