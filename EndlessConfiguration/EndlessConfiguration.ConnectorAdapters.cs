using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;


//CUBE
namespace Celestial.CustomEndless
{
    public static partial class EndlessConfiguration
    {
        private static void RegisterConnectorAdaptors()
        {
            foreach (string id in new[] { "Adaptor_SquareToDoor", "Adaptor_DoorToSquare" })
            {
                M_Level.LevelAssetHolder holder = CustomLevelLoader.LoadRoom("anywhere", id, id);
                if (holder == null || holder.level == null) continue;
                holder.level.loadAdjacent = true;
                CustomLevelRegistry.Register(id, holder.level);
            }
        }

        private static bool HasConnectorDoor(M_Level level, bool entrance)
        {
            if (level == null) return false;
            Transform marker = entrance ? level.GetLevelEntrance() : level.GetLevelExit();
            if (marker == null) return false;
            foreach (Renderer renderer in level.GetComponentsInChildren<Renderer>(true))
            {
                string name = Regex.Replace(renderer.name, @"\.\d+$", "");
                bool panel = Regex.IsMatch(name, "^(ENV_HabBlueDoor_02_[LR]|Gondola_Door_[LR]|(entrance|exit)_(left|right)_(inner|outer))$", RegexOptions.IgnoreCase);
                if (panel && renderer.bounds.SqrDistance(marker.position) <= 0.36f) return true;
            }
            return false;
        }

        private static void InsertConnectorAdaptors(List<(M_Level.LevelAssetHolder holder, string targetRegion, bool universalBreakroom)> levels)
        {
            for (int index = levels.Count - 1; index > 0; index--)
            {
                M_Level previous = levels[index - 1].holder.level;
                M_Level next = levels[index].holder.level;
                if (previous == null || next == null || previous.levelName.StartsWith("Adaptor_", StringComparison.OrdinalIgnoreCase) || next.levelName.StartsWith("Adaptor_", StringComparison.OrdinalIgnoreCase)) continue;
                bool previousDoor = HasConnectorDoor(previous, false);
                bool nextDoor = HasConnectorDoor(next, true);
                if (previousDoor == nextDoor) continue;
                M_Level Adaptor = GetTransitionLevel(previousDoor ? "Adaptor_DoorToSquare" : "Adaptor_SquareToDoor");
                if (Adaptor == null) continue;
                previous.loadAdjacent = true;
                Adaptor.loadAdjacent = true;
                levels.Insert(index, (M_Level.LevelAssetHolder.GetNewHolderFromLevel(Adaptor), levels[index].targetRegion, true));
            }
        }
    }
}
