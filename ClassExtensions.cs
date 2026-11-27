using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace CustomEndless
{
    public static class ClassExtensions
    {
        public static List<M_Level.LevelAssetHolder> GetLevelReferences(this M_Subregion subregion, bool includingStartAndEnd = false)
        {
            List<M_Level.LevelAssetHolder> result = subregion.levelGroups.SelectMany(group => group.levelReferences).ToList();
            if(includingStartAndEnd)
            {
                result.AddRange(subregion.startLevelReferences);
                result.AddRange(subregion.endLevelReferences);
            }
            return result;
        }
    }
}
