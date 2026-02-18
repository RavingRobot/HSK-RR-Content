using RimWorld;
using Verse;

namespace DroidEnergyComa
{
    public class Hediff_EnergyComa : HediffWithComps
    {
        private bool letterSent = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref letterSent, "letterSent", false);
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!letterSent && pawn != null)
            {
                string label = "DroidInComaLetterLabel".Translate(pawn.LabelShort);
                string text = "DroidInComaLetterText".Translate(pawn.LabelShort);
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent, pawn);
                Messages.Message("MessageDroidInComa".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.NegativeEvent);
                letterSent = true;
            }
        }
    }
}