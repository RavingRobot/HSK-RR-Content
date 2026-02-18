using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Verse;

namespace DroidEnergyComa
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.ravingrobot.HSKRRContent");

            // Применяем Transpiler к Need_Energy.NeedInterval
            harmony.Patch(
                original: AccessTools.Method(typeof(Androids.Need_Energy), nameof(Androids.Need_Energy.NeedInterval)),
                transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(NeedInterval_Transpiler))
            );

            // Загружаем флаг из DefModExtension (управляется через XmlExtensions.OptionalPatch)
            var ext = DefDatabase<ThingDef>.AllDefs
                .Select(d => d.GetModExtension<DroidEnergyComa_Extension>())
                .FirstOrDefault(x => x != null);
            if (ext != null)
            {
                DroidEnergyComa_Settings.ReplaceDroidDeathWithComa = ext.replaceDroidDeathWithComa;
            }
        }

        // Заменяем вызов pawn.Kill(...) на нашу логику
        public static IEnumerable<CodeInstruction> NeedInterval_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var killMethod = AccessTools.Method(typeof(Thing), nameof(Thing.Kill));
            var replacement = AccessTools.Method(typeof(DroidEnergyComaHelper), nameof(DroidEnergyComaHelper.ReplaceKillWithComa));

            bool patched = false;
            foreach (var instr in instructions)
            {
                if (!patched && instr.Calls(killMethod))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                    patched = true;
                }
                else
                {
                    yield return instr;
                }
            }

            if (!patched)
            {
                Log.Warning("[DroidEnergyComa] Transpiler did not find Thing.Kill call in Need_Energy.NeedInterval.");
            }
        }
    }

    // Вспомогательный класс для безопасной замены смерти
    public static class DroidEnergyComaHelper
    {
        public static void ReplaceKillWithComa(Thing thing, DamageInfo? dinfo, Hediff culprit)
        {
            // Если патч выключен — убиваем как обычно
            if (!DroidEnergyComa_Settings.ReplaceDroidDeathWithComa)
            {
                thing.Kill(dinfo, culprit);
                return;
            }

            Pawn pawn = thing as Pawn;
            if (pawn == null)
            {
                thing.Kill(dinfo, culprit);
                return;
            }

            // Применяем только к дроидам-колонистам
            if (!pawn.IsColonist || pawn.def.GetModExtension<Androids.MechanicalPawnProperties>() == null)
            {
                thing.Kill(dinfo, culprit);
                return;
            }

            // Удаляем ChjPowerFailure, чтобы избежать двойной смерти
            var powerFailure = pawn.health.hediffSet.GetFirstHediffOfDef(Androids.HediffDefOf.ChjPowerFailure);
            if (powerFailure != null)
            {
                pawn.health.RemoveHediff(powerFailure);
            }

            // Добавляем "энергетическую кому"
            if (!pawn.health.hediffSet.HasHediff(DroidEnergyComaHediffDefOf.EnergyComa))
            {
                var coma = HediffMaker.MakeHediff(DroidEnergyComaHediffDefOf.EnergyComa, pawn);
                pawn.health.AddHediff(coma);
            }
        }
    }

    // Рецепт замены батареи
    public class Recipe_ReplaceDroidBattery : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null) return false;
            if (!pawn.IsColonist) return false;
            if (!pawn.health.hediffSet.HasHediff(DroidEnergyComaHediffDefOf.EnergyComa)) return false;
            return base.AvailableOnNow(thing, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // Убираем кому
            var coma = pawn.health.hediffSet.GetFirstHediffOfDef(DroidEnergyComaHediffDefOf.EnergyComa);
            if (coma != null)
                pawn.health.RemoveHediff(coma);

            // Восстанавливаем энергию (50%)
            var energyNeed = pawn.needs.TryGetNeed<Androids.Need_Energy>();
            if (energyNeed != null)
                energyNeed.CurLevel = energyNeed.MaxLevel * 0.5f;

            // Уведомление
            Messages.Message("MessageDroidBatteryReplaced".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}