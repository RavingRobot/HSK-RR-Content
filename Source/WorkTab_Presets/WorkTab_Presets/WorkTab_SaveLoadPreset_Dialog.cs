using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace WorkTab
{
    public class WorkPresetDialog : Window
    {
        private readonly Pawn targetPawn;
        private string newPresetName = "";
        private List<PresetInfo> presets = new List<PresetInfo>();
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 700f);

        public WorkPresetDialog(Pawn pawn)
        {
            this.targetPawn = pawn;
            this.closeOnClickedOutside = true;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            ReloadPresets();
        }

        private void ReloadPresets()
        {
            presets.Clear();
            string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "WorkTabPresets");
            if (!Directory.Exists(folder)) return;

            foreach (string file in Directory.GetFiles(folder, "*.txt"))
            {
                try
                {
                    var fi = new FileInfo(file);
                    presets.Add(new PresetInfo
                    {
                        name = Path.GetFileNameWithoutExtension(file),
                        fullPath = file,
                        creationTime = fi.CreationTime
                    });
                }
                catch (Exception ex)
                {
                    Log.Error($"[WorkTab_SaveLoadPreset] Error reading preset file {file}: {ex}");
                }
            }
            // Сортировка: новые сверху
            presets.Sort((a, b) => b.creationTime.CompareTo(a.creationTime));
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 10f, inRect.width, 30f), "WorkTab.WorkPreset".Translate());
            Text.Font = GameFont.Small;

            float y = 50f;
            const float labelWidth = 120f;
            const float inputWidth = 350f;
            const float buttonSize = 25f;
            const float rowHeight = 28f;

            // === Поле ввода и кнопка сохранения ===
            Widgets.Label(new Rect(15f, y + 2f, labelWidth, rowHeight), "WorkTab.NewPresetName".Translate());

            Rect inputRect = new Rect(15f + labelWidth, y, inputWidth, rowHeight);
            newPresetName = Widgets.TextField(inputRect, newPresetName);

            // Кнопка "Сохранить" (иконка дискеты)
            Rect saveButton = new Rect(inRect.width - 50f, y, buttonSize, buttonSize);
            if (Widgets.ButtonImage(saveButton, ContentFinder<Texture2D>.Get("Icons/SaveLoad/save", reportFailure: false)))
            {
                if (!string.IsNullOrEmpty(newPresetName.Trim()))
                {
                    SaveNewPreset(newPresetName.Trim());
                }
                else
                {
                    Messages.Message("WorkTab.EmptyName".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
            TooltipHandler.TipRegion(saveButton, "WorkTab.Save".Translate());

            y += 40f;

            // === Список пресетов ===
            const float entryHeight = 36f;
            float viewHeight = presets.Count * entryHeight;
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeight);
            Rect outRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            for (int i = 0; i < presets.Count; i++)
            {
                var preset = presets[i];
                Rect rowRect = new Rect(0f, i * entryHeight, viewRect.width, entryHeight);
                if (i % 2 == 0) Widgets.DrawAltRect(rowRect);

                // Название пресета
                Widgets.Label(new Rect(15f, rowRect.y + 7f, 200f, entryHeight), preset.name);
                // Дата создания
                Widgets.Label(new Rect(300f, rowRect.y + 7f, 200f, entryHeight), preset.creationTime.ToString("dd-MM-yyyy HH:mm"));

                // === Кнопки справа ===
                const float buttonHeight = 25f;
                float rightEdge = rowRect.xMax - 10f;

                // 1. Кнопка "Удалить" (Красный крест)
                Rect deleteRect = new Rect(rightEdge - buttonHeight, rowRect.y + 6f, buttonHeight, buttonHeight);
                if (Widgets.ButtonImage(deleteRect, ContentFinder<Texture2D>.Get("Icons/SaveLoad/delete", reportFailure: false)))
                {
                    DeletePreset(preset);
                }
                TooltipHandler.TipRegion(deleteRect, "WorkTab.Delete".Translate());
                rightEdge -= buttonHeight + 10f;

                // 2. Кнопка "Перезаписать" (Стрелка обновления)
                Rect overwriteRect = new Rect(rightEdge - buttonHeight, rowRect.y + 6f, buttonHeight, buttonHeight);
                if (Widgets.ButtonImage(overwriteRect, ContentFinder<Texture2D>.Get("Icons/SaveLoad/overwrite", reportFailure: false)))
                {
                    OverwritePreset(preset);
                }
                TooltipHandler.TipRegion(overwriteRect, "WorkTab.Overwrite".Translate());
                rightEdge -= buttonHeight + 10f;

                // 3. Кнопка "Загрузить" (Стрелка вниз)
                Rect loadRect = new Rect(rightEdge - buttonHeight, rowRect.y + 6f, buttonHeight, buttonHeight);
                if (Widgets.ButtonImage(loadRect, ContentFinder<Texture2D>.Get("Icons/SaveLoad/load", reportFailure: false)))
                {
                    LoadPreset(preset);
                }
                TooltipHandler.TipRegion(loadRect, "WorkTab.Load".Translate());
            }
            Widgets.EndScrollView();

            // Кнопка закрытия
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - 50f, inRect.height - 40f, 100f, 30f), "Close".Translate()))
            {
                Close();
            }
        }

        private void SaveNewPreset(string name)
        {
            try
            {
                string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "WorkTabPresets");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string path = Path.Combine(folder, name + ".txt");
                if (File.Exists(path))
                {
                    Messages.Message("WorkTab.PresetExists".Translate(), MessageTypeDefOf.RejectInput);
                    return;
                }

                using (StreamWriter sw = new StreamWriter(path, false, System.Text.Encoding.UTF8))
                {
                    foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                    {
                        int[] priorities = targetPawn.GetPriorities(def);
                        if (priorities != null && priorities.Any(p => p > 0))
                        {
                            sw.WriteLine($"workgiver:{def.defName}");
                            sw.WriteLine($"priorities:{string.Join(",", priorities)}");
                        }
                    }
                }
                Messages.Message("WorkTab.PresetSaved".Translate(name), MessageTypeDefOf.PositiveEvent);
                ReloadPresets();
            }
            catch (Exception ex)
            {
                Log.Error($"[WorkTab_SaveLoadPreset] Failed to save preset: {ex}");
                Messages.Message("WorkTab.SaveFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private void OverwritePreset(PresetInfo preset)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "WorkTab.ConfirmOverwrite".Translate(preset.name),
                () =>
                {
                    try
                    {
                        File.Delete(preset.fullPath);
                        SaveNewPreset(preset.name);
                        Messages.Message("WorkTab.PresetOverwrited".Translate(preset.name), MessageTypeDefOf.PositiveEvent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[WorkTab_SaveLoadPreset] Failed to overwrite preset: {ex}");
                        Messages.Message("WorkTab.OverwriteFailed".Translate(), MessageTypeDefOf.RejectInput);
                    }
                },
                destructive: true
            ));
        }

        private void LoadPreset(PresetInfo preset)
        {
            try
            {
                // СБРОС ВСЕХ ПРИОРИТЕТОВ ПЕРЕД ЗАГРУЗКОЙ
                foreach (WorkGiverDef wg in DefDatabase<WorkGiverDef>.AllDefsListForReading)
                {
                    for (int hour = 0; hour < 24; hour++)
                    {
                        targetPawn.SetPriority(wg, 0, hour, recache: false);
                    }
                }

                string[] lines = File.ReadAllLines(preset.fullPath, System.Text.Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("workgiver:"))
                    {
                        string defName = lines[i].Substring("workgiver:".Length);
                        WorkGiverDef def = DefDatabase<WorkGiverDef>.GetNamed(defName);
                        if (def == null) continue;

                        if (i + 1 < lines.Length && lines[i + 1].StartsWith("priorities:"))
                        {
                            string priosStr = lines[i + 1].Substring("priorities:".Length);
                            string[] prioStrs = priosStr.Split(',');
                            int[] priorities = new int[24];
                            for (int h = 0; h < 24 && h < prioStrs.Length; h++)
                            {
                                if (int.TryParse(prioStrs[h], out int p))
                                    priorities[h] = p;
                            }
                            // Используем наш extension method для установки массива
                            targetPawn.SetPriority(def, priorities, TimeUtilities.WholeDay);
                            i++; // пропускаем строку с приоритетами
                        }
                    }
                }

                targetPawn.workSettings.Notify_UseWorkPrioritiesChanged();

                string pawnLabel = targetPawn.LabelShort;
                Messages.Message(
                    "WorkTab.PresetLoadedForPawn".Translate(preset.name, pawnLabel),
                    MessageTypeDefOf.PositiveEvent
                );
                Close();
            }
            catch (Exception ex)
            {
                Log.Error($"[WorkTab_SaveLoadPreset] Failed to load preset '{preset.name}': {ex}");
                Messages.Message("WorkTab.LoadFailed".Translate(), MessageTypeDefOf.RejectInput);
            }
        }

        private void DeletePreset(PresetInfo preset)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "WorkTab.ConfirmDelete".Translate(preset.name),
                () =>
                {
                    try
                    {
                        File.Delete(preset.fullPath);
                        ReloadPresets();
                        Messages.Message("WorkTab.PresetDeleted".Translate(preset.name), MessageTypeDefOf.PositiveEvent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[WorkTab_SaveLoadPreset] Failed to delete preset: {ex}");
                        Messages.Message("WorkTab.DeleteFailed".Translate(), MessageTypeDefOf.RejectInput);
                    }
                },
                destructive: true
            ));
        }

        private class PresetInfo
        {
            public string name;
            public string fullPath;
            public DateTime creationTime;
        }
    }
}