using RimWorld;
using UnityEngine;
using Verse;

namespace WorkTab
{
    public class PawnColumnWorker_SaveLoadPreset : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn == null) return;

            float size = 21f;
            Rect iconRect = new Rect(
                rect.x + (rect.width - size) / 2f,
                rect.y + (rect.height - size) / 2f,
                size, size
            );

            Texture2D icon = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Save", reportFailure: false);

            if (Widgets.ButtonImage(iconRect, icon))
            {
                Find.WindowStack.Add(new WorkPresetDialog(pawn));
            }

            TooltipHandler.TipRegion(iconRect, "WorkTab.SaveLoadPreset".Translate());
        }
    }
}