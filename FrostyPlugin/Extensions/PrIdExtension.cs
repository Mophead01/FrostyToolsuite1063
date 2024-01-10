using FrostySdk.Ebx;
using FrostySdk.Managers;
using System.Windows.Media;

namespace Frosty.Core
{
    public abstract class PrIdExtension
    {
        public PrIdExtension()
        {
        }

        public virtual string GetOverrideString(dynamic assetData)
        {
            return string.Empty;
        }

        public string GetExternalName(PointerRef pr, bool getFullName = false)
        {
            if (pr.Type != FrostySdk.IO.PointerRefType.External)
                return "";
            EbxAssetEntry refEntry = App.AssetManager.GetEbxEntry(pr.External.FileGuid);
            if (refEntry == null)
                return "";
            if (getFullName)
                return refEntry.Name;
            return refEntry.DisplayName;
        }
    }
}
