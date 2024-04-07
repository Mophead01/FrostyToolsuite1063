using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Frosty.Core.Mod
{
    public class ExportActionOverride
    {
        public virtual void PreExport(AssetManager AM, string fbmodName, List<string> loadOrder)
        {
        }
        public virtual void PostExport(AssetManager AM, string fbmodName, List<string> loadOrder)
        {
        }
    }
}
