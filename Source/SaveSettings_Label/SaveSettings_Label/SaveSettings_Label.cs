using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Verse;

namespace SaveStorageSettings
{
    public class SaveSettings_Label : Mod
    {
        public SaveSettings_Label(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("ravingrobot.savestoragesettings.label");
            harmony.PatchAll();
            Log.Message("[SaveSettings_Label] Harmony initialized.");
        }
    }
}