﻿using FrostySdk.Managers;

namespace Frosty.Core.Mod
{
    public class EbxResource : BaseModResource
    {
        public override ModResourceType Type => ModResourceType.Ebx;

        public EbxResource()
        {
        }

        internal EbxResource(EbxAssetEntry entry)
            : base(entry)
        {
        }
    }
}
