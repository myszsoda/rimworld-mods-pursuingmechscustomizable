using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

#nullable disable
namespace PursuingMechanoidsCustomizable;

public class ScenPart_PMechs_Custom : ScenPart
{
	private bool onStartMap = true;
	private Dictionary<Map, int> mapWarningTimers = new Dictionary<Map, int>();
	private Dictionary<Map, int> mapRaidTimers = new Dictionary<Map, int>();
	private bool questCompleted;
	private const int InitialWarningDelay = 2700;
	private const int InitialRaidDelay = 30000;
	private const int TickInterval = 2500;
	private const int SecondRaidDelay = 30000;
	private Map cachedAlertMap;
	private Alert_MechThreat alertCached;
	private List<Map> tmpWarningKeys;
	private List<int> tmpWarningValues;
	private List<Map> tmpRaidKeys;
	private List<int> tmpRaidValues;
	private List<Map> tmpMaps = new List<Map>();

	private Alert_MechThreat AlertCached
	{
		get
		{
			if (this.Disabled)
				return (Alert_MechThreat)null;
			if (this.cachedAlertMap != Find.CurrentMap)
				this.alertCached = (Alert_MechThreat)null;
			if (this.alertCached != null && Find.TickManager.TicksGame > this.TimerIntervalTick(this.alertCached.raidTick + 30000))
				this.alertCached = (Alert_MechThreat)null;

			int timer;
			int num;
			if (this.alertCached != null ||
			    !this.mapWarningTimers.TryGetValue(Find.CurrentMap, out timer) ||
			    Find.TickManager.TicksGame <= this.TimerIntervalTick(timer) ||
			    !this.mapRaidTimers.TryGetValue(Find.CurrentMap, out num) ||
			    Find.TickManager.TicksGame >= this.TimerIntervalTick(num + 30000))
				return this.alertCached;

			this.alertCached = new Alert_MechThreat()
			{
				raidTick = this.mapRaidTimers[Find.CurrentMap]
			};

			this.cachedAlertMap = Find.CurrentMap;
			return this.alertCached;
		}
	}

	private bool Disabled => this.questCompleted;

	public override bool OverrideDangerMusic => this.onStartMap;

	public override void ExposeData()
	{
		base.ExposeData();
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			foreach (Map key in this.mapWarningTimers.Keys.ToList<Map>())
			{
				if (key?.Parent == null || key.Parent.Destroyed)
				{
					this.mapWarningTimers.Remove(key);
					this.mapRaidTimers.Remove(key);
				}
			}
		}
		Scribe_Values.Look<bool>(ref this.onStartMap, "initialMap");
		Scribe_Collections.Look<Map, int>(ref this.mapWarningTimers, "mapWarningTimers",
						  LookMode.Reference, LookMode.Value,
						  ref this.tmpWarningKeys,
						  ref this.tmpWarningValues);
		Scribe_Collections.Look<Map, int>(ref this.mapRaidTimers, "mapRaidTimers",
						  LookMode.Reference, LookMode.Value,
						  ref this.tmpRaidKeys, ref this.tmpRaidValues);
		Scribe_Values.Look<bool>(ref this.questCompleted, "questCompleted");
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
			return;
		if (this.mapWarningTimers == null)
			this.mapWarningTimers = new Dictionary<Map, int>();
		if (this.mapRaidTimers != null)
			return;
		this.mapRaidTimers = new Dictionary<Map, int>();
	}

	public override void PostWorldGenerate()
	{
		this.onStartMap = true;
		this.mapWarningTimers.Clear();
		this.mapRaidTimers.Clear();
	}

	public override void PostMapGenerate(Map map)
	{
		if (!this.onStartMap)
			return;
		this.StartTimers(map);
		this.onStartMap = false;
	}

	public override void PostGravshipLanded(Map map)
	{
		this.onStartMap = false;
		this.StartTimers(map);
	}

	public override void MapRemoved(Map map)
	{
		if (!this.mapWarningTimers.Remove(map))
			return;
		this.mapRaidTimers.Remove(map);
		this.onStartMap = false;
	}

	private int TimerIntervalTick(int timer) => (timer + 2500 - 1) / 2500 * 2500;

	public override void Tick()
	{
		if (Find.TickManager.TicksGame % 2500 != 0)
			return;
		this.tmpMaps.Clear();
		this.tmpMaps.AddRange((IEnumerable<Map>)this.mapWarningTimers.Keys);
		foreach (Map tmpMap in this.tmpMaps)
		{
			if ((this.Disabled ? 1 : (GravshipUtility.GetPlayerGravEngine_NewTemp(tmpMap) == null ? 1 : 0)) != 0)
			{
				this.mapWarningTimers.Remove(tmpMap);
				this.mapRaidTimers.Remove(tmpMap);
			}
			else
			{
				if (Find.TickManager.TicksGame == this.TimerIntervalTick(this.mapWarningTimers[tmpMap]))
				{
					Thing thing = tmpMap.listerThings.ThingsOfDef(ThingDefOf.PilotConsole).FirstOrDefault<Thing>();
					Find.LetterStack.ReceiveLetter("LetterLabelMechanoidThreat".Translate(),
								       "LetterTextMechanoidThreat".Translate(),
								       LetterDefOf.ThreatSmall, (LookTargets)thing);
				}
				if (Find.TickManager.TicksGame == this.TimerIntervalTick(this.mapRaidTimers[tmpMap]))
					this.FireRaid_NewTemp(tmpMap, 1.5f, 2000f);
				if (Find.TickManager.TicksGame == this.TimerIntervalTick(this.mapRaidTimers[tmpMap] + 30000))
					this.FireRaid_NewTemp(tmpMap, 2f, 8000f);
			}
		}
	}

	private void StartTimers(Map map)
	{
		if (map.generatorDef == MapGeneratorDefOf.Mechhive)
			return;

		IntRange raidDelayRange = new IntRange(Settings_PMechs_Custom.RaidDelayRangeMin,
						       Settings_PMechs_Custom.RaidDelayRangeMax);
		/* I don't like this logic, but don't want it to be configurable.
		 * XXX Maybe it should?
		 * Set maximum value to 0.9 to avoid no time between warning and raid
		 */
		IntRange warningDelayRange = new IntRange(Settings_PMechs_Custom.RaidDelayRangeMin / 2,
							  (int)(Settings_PMechs_Custom.RaidDelayRangeMin * 0.9));

		if (this.onStartMap)
		{
			this.mapWarningTimers[map] = Find.TickManager.TicksGame + 2700;
			this.mapRaidTimers[map] = Find.TickManager.TicksGame + 30000;
		}
		else
		{
			this.mapWarningTimers[map] = Find.TickManager.TicksGame + warningDelayRange.RandomInRange;
			this.mapRaidTimers[map] = Find.TickManager.TicksGame + raidDelayRange.RandomInRange;
		}

	}

	public void Notify_QuestCompleted() => this.questCompleted = true;

	private void FireRaid(Map map) => this.FireRaid_NewTemp(map, 1.5f, 5000f);

	private void FireRaid_NewTemp(Map map, float pointsMultiplier, float minPoints)
	{
		IncidentDefOf.RaidEnemy.Worker.TryExecute(new IncidentParms()
		{
			forced = true,
			target = (IIncidentTarget)map,
#if USE_MATHF
			points = Mathf.Max(minPoints, StorytellerUtility.DefaultThreatPointsNow((IIncidentTarget)map) * pointsMultiplier),
#else
			points = Math.Max(minPoints, StorytellerUtility.DefaultThreatPointsNow((IIncidentTarget)map) * pointsMultiplier),
#endif
			faction = Faction.OfMechanoids,
			raidArrivalMode = PawnsArrivalModeDefOf.RandomDrop,
			raidStrategy = RaidStrategyDefOf.ImmediateAttack
		});
	}

	public override IEnumerable<Alert> GetAlerts()
	{
		Map currentMap = Find.CurrentMap;
		if (currentMap != null && currentMap.IsPlayerHome && this.AlertCached != null)
			yield return (Alert)this.AlertCached;
	}
}
