using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorkTab;

public static class Pawn_Extensions_SaveLoad
{
    // Добавляем новый метод для загрузки массива приоритетов
    public static void SetPriority(this Pawn pawn, WorkGiverDef workgiver, int[] priorities, List<int> hours)
    {
        if (hours == null) hours = TimeUtilities.WholeDay;

        for (int i = 0; i < hours.Count && i < priorities.Length; i++)
        {
            int hour = hours[i];
            if (hour >= 0 && hour < 24)
            {
                pawn.SetPriority(workgiver, priorities[hour], hour, recache: false);
            }
        }

        PriorityManager.Get[pawn].InvalidateCache(workgiver);
    }
}