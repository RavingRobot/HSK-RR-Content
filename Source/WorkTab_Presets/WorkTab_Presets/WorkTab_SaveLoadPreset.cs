using HarmonyLib;
using Verse;
using System.Reflection;

namespace WorkTab
{
    // Наследуемся от Verse.Mod
    public class WorkTab_SaveLoadPreset : Mod
    {
        public WorkTab_SaveLoadPreset(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("ravingrobot.worktab.saveloadpreset");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[WorkTab_SaveLoadPreset] Harmony initialized.");
        }
    }
}