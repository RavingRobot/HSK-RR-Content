using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace HackSkynetSK;

public class Recipe_HackSkynetSK : RecipeWorker
{
	public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
	{
		if (Rand.Chance(0.5f))
		{
			pawn.SetFaction(Faction.OfPlayer, billDoer);
			if (pawn.IsPrisoner)
			{
				pawn.guest.SetGuestStatus(null);
			}
			SendLetter("HackSkynetSK_SuccessMessage", pawn.LabelShort, LetterDefOf.NeutralEvent, pawn);
			return;
		}
		(GenTypes.GetTypeInAnyAssembly("Skynet.Skynet_Utility")?.GetMethod("DestroyMeWithExplosion", BindingFlags.Static | BindingFlags.Public))?.Invoke(null, new object[1] { pawn });
		SendLetter("HackSkynetSK_FailureMessage", pawn.LabelShort, LetterDefOf.ThreatSmall, pawn);
		if (Rand.Chance(0.3f) && pawn.MapHeld != null)
		{
			TryStartCounterAttack(pawn);
		}
	}

	private void TryStartCounterAttack(Pawn sourcePawn)
	{
		if (sourcePawn.MapHeld == null)
		{
			return;
		}
		Faction faction = null;
		foreach (Faction item in Find.FactionManager.AllFactionsListForReading)
		{
			if (item.def?.fixedName == "Skynet" || (item.def?.fixedName == "Скайнет" && item.HostileTo(Faction.OfPlayer)))
			{
				faction = item;
				break;
			}
		}
		if (faction != null)
		{
			IncidentParms incidentParms = new IncidentParms();
			incidentParms.target = sourcePawn.MapHeld;
			incidentParms.faction = faction;
			incidentParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
			incidentParms.forced = true;
			incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
			int num = 0;
			int num2 = 0;
			num = Rand.Range(2, 8);
			num2 = num * 2500;
			if (Rand.Chance(0.75f))
			{
				incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeDrop;
			}
			else
			{
				incidentParms.raidArrivalMode = PawnsArrivalModeDefOf.CenterDrop;
			}
			if (Find.Storyteller.incidentQueue.Add(IncidentDefOf.RaidEnemy, Find.TickManager.TicksGame + num2, incidentParms, 240000))
			{
				SendLetter("HackSkynetSK_RaidTriggered", "", LetterDefOf.ThreatSmall, sourcePawn);
			}
		}
	}

	private void SendLetter(string key, string labelShort, LetterDef letterDef, Pawn pawn)
	{
		Find.LetterStack.ReceiveLetter("HackSkynetSK_LetterTitle".Translate(), key.Translate(labelShort), letterDef, pawn);
	}
}
