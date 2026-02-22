using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace WorkTab;

[HarmonyPatch(typeof(DefGenerator), "GenerateImpliedDefs_PreResolve")]
public class DefGenerator_AddSaveLoadColumn_Patch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        // Получаем таблицу работы
        PawnTableDef work = PawnTableDefOf.Work;

        // Создаем столбец
        PawnColumnDef saveLoadPreset = new PawnColumnDef();
        saveLoadPreset.defName = "WorkTab_SaveLoadPreset";
        saveLoadPreset.workerClass = typeof(PawnColumnWorker_SaveLoadPreset);
        saveLoadPreset.headerIcon = "Icons/SaveLoad/blank";
        saveLoadPreset.headerIconSize = new Vector2(25f, 25f);

        // Регистрируем деф
        saveLoadPreset.PostLoad();
        DefDatabase<PawnColumnDef>.Add(saveLoadPreset);

        // Вставляем его последним элементом
        work.columns.Insert(work.columns.Count - 1, saveLoadPreset);

        // Обновляем статический список контроллера, если он используется UI
        if (Controller.allColumns != null)
        {
            Controller.allColumns = new List<PawnColumnDef>(work.columns);
        }

        // Сообщаем таблице, что структура изменилась (если таблица уже открыта)
        if (MainTabWindow_WorkTab.Instance != null)
        {
            MainTabWindow_WorkTab.Instance.Notify_ColumnsChanged();
        }
    }
}