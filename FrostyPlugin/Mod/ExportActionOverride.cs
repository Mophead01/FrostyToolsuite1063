using Frosty.Core.Attributes;
using Frosty.Core.Windows;
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
        public virtual void PreExport(FrostyTaskWindow task, ExportType exportType, string fbmodName, List<string> loadOrder)
        {
        }
        public virtual void PostExport(FrostyTaskWindow task, ExportType exportType, string fbmodName, List<string> loadOrder)
        {
        }
    }
}
