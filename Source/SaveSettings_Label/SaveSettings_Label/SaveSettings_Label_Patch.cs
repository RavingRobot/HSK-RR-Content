using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace SaveStorageSettings
{
    // === Helper для работы с ImprovedWorkbenches ===
    internal static class IWHelper
    {
        public static Type StorageType;
        public static MethodInfo GetOrCreateMethod;
        public static FieldInfo NameField;
        public static bool Ready;

        static IWHelper()
        {
            bool bwmFound = false;
            foreach (var mod in LoadedModManager.RunningMods)
            {
                string pid = mod.PackageId.ToLower();
                if (pid == "falconne.bwm" || pid.Contains("improvedworkbenches"))
                {
                    foreach (var asm in mod.assemblies.loadedAssemblies)
                    {
                        var st = asm.GetType("ImprovedWorkbenches.ExtendedBillDataStorage");
                        var dt = asm.GetType("ImprovedWorkbenches.ExtendedBillData");

                        if (st != null && dt != null)
                        {
                            StorageType = st;
                            NameField = dt.GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                            GetOrCreateMethod = st.GetMethod("GetOrCreateExtendedDataFor", BindingFlags.Public | BindingFlags.Instance);

                            if (GetOrCreateMethod != null && NameField != null)
                            {
                                Ready = true;
                                Log.Message("[SaveSettings_Label] ImprovedWorkbenches detected.");
                                return;
                            }
                        }
                    }
                }
            }
            if (!bwmFound)
            {
                Log.Warning("[SaveSettings_Label] ImprovedWorkbenches NOT found. Custom labels will be saved but not applied.");
            }
        }

        public static void ApplyLabel(Bill_Production bill, string label)
        {
            if (!Ready || Current.Game?.World == null || string.IsNullOrEmpty(label)) return;
            try
            {
                var storage = Current.Game.World.GetComponent(StorageType);
                if (storage == null) return;

                var data = GetOrCreateMethod.Invoke(storage, new object[] { bill });
                if (data != null)
                {
                    NameField.SetValue(data, label);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[SaveSettings_Label] Failed to apply label: {ex}");
            }
        }
    }

    // === ПАТЧ 1: Сохранение (Postfix) ===
    // Добавляет customLabel в файл ПОСЛЕ того, как оригинал его записал.
    [HarmonyPatch]
    public static class Patch_SaveCraftingSettings
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("SaveStorageSettings.IOUtil");
            return type?.GetMethod("SaveCraftingSettings");
        }

        [HarmonyPostfix]
        public static void Postfix(BillStack bills, FileInfo fi)
        {
            if (!IWHelper.Ready || !fi.Exists) return;

            try
            {
                var lines = File.ReadAllLines(fi.FullName).ToList();
                var newLines = new List<string>();
                int billIndex = -1;

                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    newLines.Add(line);

                    // Ищем строку с названием рецепта
                    if (line.StartsWith("recipeDefName:") || line.StartsWith("recipeDefNameUft:"))
                    {
                        billIndex++;

                        // Проверяем, есть ли такой билл в стеке
                        if (billIndex >= 0 && billIndex < bills.Count)
                        {
                            if (bills[billIndex] is Bill_Production bp)
                            {
                                string visibleLabel = bp.LabelCap;
                                string defaultLabel = bp.recipe.LabelCap;

                                // Если имя отличается от стандартного, добавляем строку customLabel СРАЗУ после рецепта
                                if (visibleLabel != defaultLabel)
                                {
                                    newLines.Add($"customLabel:{visibleLabel}");
                                }
                            }
                        }
                    }
                }

                File.WriteAllLines(fi.FullName, newLines);
            }
            catch (Exception ex)
            {
                Log.Warning($"[SaveSettings_Label] Failed to patch save file: {ex}");
            }
        }
    }

    // === ПАТЧ 2: Загрузка (Postfix) ===
    // 1. Сканируем файл и создаем словарь: ИндексРецепта -> CustomLabel.
    // 2. Применяем имена к загруженным биллам по их индексу в списке.
    [HarmonyPatch]
    public static class Patch_LoadCraftingBills
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("SaveStorageSettings.IOUtil");
            return type?.GetMethod("LoadCraftingBills", new Type[] { typeof(FileInfo) });
        }

        [HarmonyPostfix]
        public static void Postfix(FileInfo fi, List<Bill> __result)
        {
            if (!IWHelper.Ready || __result == null || __result.Count == 0 || !fi.Exists) return;

            try
            {
                // 1. Читаем файл и собираем имена по ИНДЕКСАМ
                // Dictionary<int, string> где Key = порядковый номер рецепта (0, 1, 2...), Value = имя
                var labelsByIndex = new Dictionary<int, string>();

                int currentRecipeIndex = -1;
                bool waitingForLabel = false;

                using (var sr = new StreamReader(fi.FullName))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();

                        // Если встретили рецепт
                        if (line.StartsWith("recipeDefName:") || line.StartsWith("recipeDefNameUft:"))
                        {
                            currentRecipeIndex++;
                            waitingForLabel = true; // Ожидаем, что следующая значимая строка может быть лейблом
                        }
                        // Если встретили лейбл И мы только что прочитали рецепт
                        else if (waitingForLabel && line.StartsWith("customLabel:"))
                        {
                            int idx = line.IndexOf(':');
                            if (idx > 0)
                            {
                                string label = line.Substring(idx + 1);
                                labelsByIndex[currentRecipeIndex] = label;
                            }
                            waitingForLabel = false; // Лейбл обработан
                        }
                        // Если встретили что-то другое (например, skillRange), значит лейбла для этого рецепта не было
                        else if (waitingForLabel && line.Contains(":") && !line.StartsWith("---"))
                        {
                            waitingForLabel = false;
                        }
                        // Сброс флага при конце блока
                        else if (line == "---:---" || line == "---")
                        {
                            waitingForLabel = false;
                        }
                    }
                }

                // 2. Применяем имена к загруженным биллам строго по порядку
                for (int i = 0; i < __result.Count; i++)
                {
                    if (__result[i] is Bill_Production bp)
                    {
                        // Пытаемся получить имя для этого индекса
                        if (labelsByIndex.TryGetValue(i, out string label))
                        {
                            IWHelper.ApplyLabel(bp, label);
                        }
                        // Если ключа нет в словаре, значит в файле не было customLabel для этого рецепта.
                        // Оставляем ванильное имя (ничего не делаем).
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[SaveSettings_Label] Error applying labels on load: {ex}");
            }
        }
    }
}