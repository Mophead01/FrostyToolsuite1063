using Frosty.Core;
using Frosty.Core.Windows;
using Frosty.Hash;
using FrostySdk;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace ChunkResEditorPlugin
{
    public static class HashCache
    {
        public static bool IsLoaded { get; private set; }

        private const uint cacheVersion = 1;
        private static Dictionary<uint, CString> hash32List = new Dictionary<uint,CString>();
        private static Dictionary<ulong, CString> hash64List = new Dictionary<ulong,CString>();

        public static void LoadHashCache(FrostyTaskWindow task)
        {
            //File.OpenRead("../../FrostyEditor/strings.txt");
            using (NativeReader reader = new NativeReader(new FileStream("strings.txt", FileMode.Open, FileAccess.Read)))
            {
                while (reader.Position < reader.Length)
                {
                    string buffer = reader.ReadLine();
                    if (!hash32List.ContainsKey((uint)Fnv1.HashString(buffer.ToLower())))
                        hash32List.Add((uint)Fnv1.HashString(buffer.ToLower()), buffer);
                    if (!hash64List.ContainsKey(Fnv1.HashString64(buffer.ToLower())))
                        hash64List.Add(Fnv1.HashString64(buffer.ToLower()), buffer);
                }
            }
            //if (!ReadCache(task))
            //{
            //    uint totalCount = (uint)App.AssetManager.EnumerateEbx(type: "MeshVariationDatabase").Count();
            //    uint index = 0;
            //    foreach (EbxAssetEntry entry in App.AssetManager.EnumerateEbx(type: "MeshVariationDatabase"))
            //    {
            //        uint progress = (uint)((index / (float)totalCount) * 100);
            //        task.Update(status: "Collecting Hashes", progress: progress);
            //        dynamic root = App.AssetManager.GetEbx(entry).RootObject;
            //        foreach (dynamic e in root.Entries)
            //        {
            //            foreach( dynamic m in e.Materials)
            //            {
            //                foreach (dynamic t in m.TextureParameters)
            //                {
            //                    if (!hash32List.ContainsKey((uint)Fnv1.HashString(((string)t.ParameterName).ToLower())))
            //                        hash32List.Add((uint)Fnv1.HashString(((string)t.ParameterName).ToLower()), ((CString)t.ParameterName).Sanitize());
            //                }
            //            }
            //        }
            //    }

            //    WriteCache(task);

            //}
            IsLoaded = true;
        }
        public static CString GetHashString(uint hash)
        {
            return hash32List.ContainsKey(hash) ? hash32List[hash] : (CString)hash.ToString("X8");
        }
        public static CString GetHashString(ulong hash)
        {
            return hash64List.ContainsKey(hash) ? hash64List[hash] : (CString)hash.ToString("X16");
        }
        public static bool ReadCache(FrostyTaskWindow task)
        {
            if (!File.Exists($"{App.FileSystem.CacheName}_hash.cache"))
                return false;

            if (!File.Exists(@"D:\Frosty_Alpha_4\FrostyEditor\Caches\test.txt"))
            {
                List<string> st = new List<string>();
                foreach (KeyValuePair<uint, CString> kv in hash32List)
                {
                    st.Add(kv.Value);
                }
                File.WriteAllLines(@"D:\Frosty_Alpha_4\FrostyEditor\Caches\test.txt", st.ToArray());
            }

            task.Update($"Loading data ({App.FileSystem.CacheName}_hash.cache)");

            using (NativeReader reader = new NativeReader(new FileStream($"{App.FileSystem.CacheName}_hash.cache", FileMode.Open, FileAccess.Read)))
            {
                uint version = reader.ReadUInt();
                if (version != cacheVersion)
                    return false;

                int profileHash = reader.ReadInt();
                if(profileHash != Fnv1.HashString(ProfilesLibrary.ProfileName))
                    return false;
                
                uint count = reader.ReadUInt();
                for (uint i = 0; i < count; i++)
                {
                    uint key = reader.ReadUInt();
                    string value = reader.ReadNullTerminatedString();
                    hash32List.Add(key, value);
                }
                return true;
            }
        }

        public static void WriteCache(FrostyTaskWindow task)
        {
            FileInfo fi = new FileInfo($"{App.FileSystem.CacheName}_hash.cache");
            if (!Directory.Exists(fi.DirectoryName))
                Directory.CreateDirectory(fi.DirectoryName);

            task.Update("Caching data");

            using (NativeWriter writer = new NativeWriter(new FileStream(fi.FullName, FileMode.Create)))
            {
                writer.Write(cacheVersion);
                writer.Write(Fnv1.HashString(ProfilesLibrary.ProfileName));

                writer.Write((uint)hash32List.Count);
                foreach (KeyValuePair<uint, CString> kv in hash32List)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value);
                    writer.Write((byte)0);
                }
            }
        }

       
    }
}