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
	// Tick of the FIRST raid on each map
	private Dictionary<Map, int> mapRaidTimers = new Dictionary<Map, int>();
	// Number of raids already fired on each map (per map, and saved)
	private Dictionary<Map, int> mapRaidCounts = new Dictionary<Map, int>();
	private bool questCompleted;
	private const int InitialWarningDelay = 2700;
	private const int InitialRaidDelay = 30000;
	private const int TickInterval = 2500;
	private const int RaidSpacing = 30000;
	private const int VanillaRaidCount = 2;
	private Map cachedAlertMap;
	private Alert_MechThreat alertCached;
	private List<Map> tmpWarningKeys;
	private List<int> tmpWarningValues;
	private List<Map> tmpRaidKeys;
	private List<int> tmpRaidValues;
	private List<Map> tmpCountKeys;
	private List<int> tmpCountValues;
	private List<Map> tmpMaps = new List<Map>();

	private bool Disabled => this.questCompleted;

	private int RaidCount(Map map) => this.mapRaidCounts.TryGetValue(map, out int c) ? c : 0;

	// True while more raids are still going to come on this map
	private bool RaidsRemaining(Map map) => Settings_PMechs_Custom.RaidDoInfinite || this.RaidCount(map) < VanillaRaidCount;

	// Tick at which the next raid is due on this map
	private int NextRaidTick(Map map) => this.mapRaidTimers[map] + this.RaidCount(map) * RaidSpacing;

	private Alert_MechThreat AlertCached
	{
		get
		{
			if (this.Disabled)
				return null;
			Map map = Find.CurrentMap;
			if (map == null)
				return null;
			if (this.cachedAlertMap != map)
				this.alertCached = null;

			if (!this.mapWarningTimers.TryGetValue(map, out int warningTick) ||
			    !this.mapRaidTimers.ContainsKey(map) ||
			    Find.TickManager.TicksGame <= this.TimerIntervalTick(warningTick) ||
			    !this.RaidsRemaining(map))
			{
				this.alertCached = null;
				return null;
			}

			if (this.alertCached == null)
			{
				this.alertCached = new Alert_MechThreat();
				this.cachedAlertMap = map;
			}
			// Keep the alert pointing at the upcoming raid
			this.alertCached.raidTick = this.NextRaidTick(map);
			return this.alertCached;
		}
	}

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
					this.mapRaidCounts.Remove(key);
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
		Scribe_Collections.Look<Map, int>(ref this.mapRaidCounts, "mapRaidCounts",
						  LookMode.Reference, LookMode.Value,
						  ref this.tmpCountKeys, ref this.tmpCountValues);
		Scribe_Values.Look<bool>(ref this.questCompleted, "questCompleted");
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
			return;
		if (this.mapWarningTimers == null)
			this.mapWarningTimers = new Dictionary<Map, int>();
		if (this.mapRaidTimers == null)
			this.mapRaidTimers = new Dictionary<Map, int>();
		if (this.mapRaidCounts == null)
			this.mapRaidCounts = new Dictionary<Map, int>();

		// Migrate saves made before raid counts were stored: derive how many raids have already happened
		int now = Find.TickManager.TicksGame;
		foreach (KeyValuePair<Map, int> kv in this.mapRaidTimers.ToList())
		{
			if (kv.Key == null || this.mapRaidCounts.ContainsKey(kv.Key))
				continue;
			this.mapRaidCounts[kv.Key] = now >= kv.Value ? (now - kv.Value) / RaidSpacing + 1 : 0;
		}
	}

	public override void PostWorldGenerate()
	{
		this.onStartMap = true;
		this.mapWarningTimers.Clear();
		this.mapRaidTimers.Clear();
		this.mapRaidCounts.Clear();
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
		this.mapRaidCounts.Remove(map);
		this.onStartMap = false;
	}

	private void CheckMechhiveQuestCompletion()
	{
		List<Quest> quests = Find.QuestManager.QuestsListForReading;
		for (int i = 0; i < quests.Count; i++)
		{
			Quest quest = quests[i];
			if (quest.State == QuestState.EndedSuccess &&
			    quest.root != null &&
			    quest.root.defName.IndexOf("Gravcore_Mechhive") >= 0)
			{
				this.Notify_QuestCompleted();
				return;
			}
		}
	}

	private int TimerIntervalTick(int timer) => (timer + TickInterval - 1) / TickInterval * TickInterval;

	public override void Tick()
	{
		if (Find.TickManager.TicksGame % TickInterval != 0)
			return;

		this.tmpMaps.Clear();
		this.tmpMaps.AddRange((IEnumerable<Map>)this.mapWarningTimers.Keys);

		if (!this.questCompleted)
			this.CheckMechhiveQuestCompletion();

		int now = Find.TickManager.TicksGame;
		foreach (Map map in this.tmpMaps)
		{
			if (this.Disabled || GravshipUtility.GetPlayerGravEngine_NewTemp(map) == null)
			{
				this.mapWarningTimers.Remove(map);
				this.mapRaidTimers.Remove(map);
				this.mapRaidCounts.Remove(map);
				continue;
			}

			if (!this.mapRaidTimers.ContainsKey(map))
				continue;

			if (now == this.TimerIntervalTick(this.mapWarningTimers[map]))
			{
				Thing thing = map.listerThings.ThingsOfDef(ThingDefOf.PilotConsole).FirstOrDefault<Thing>();
				Find.LetterStack.ReceiveLetter("LetterLabelMechanoidThreat".Translate(),
							       "LetterTextMechanoidThreat".Translate(),
							       LetterDefOf.ThreatSmall, (LookTargets)thing);
			}

			if (!this.RaidsRemaining(map))
				continue;

			int count = this.RaidCount(map);
			if (now < this.TimerIntervalTick(this.NextRaidTick(map)))
				continue;

			this.FireRaid_NewTemp(map, count == 0 ? 1.5f : 2f, (float)Settings_PMechs_Custom.RaidStrengthMin);

			int caughtUp = (now - this.mapRaidTimers[map]) / RaidSpacing + 1;
			this.mapRaidCounts[map] = Math.Max(count + 1, caughtUp);
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
		 * Set maximum value to 0.95 to avoid no time between warning and raid
		 */
		IntRange warningDelayRange = new IntRange(Settings_PMechs_Custom.RaidDelayRangeMin / 2,
							  (int)(Settings_PMechs_Custom.RaidDelayRangeMin * 0.95));

		if (this.onStartMap)
		{
			this.mapWarningTimers[map] = Find.TickManager.TicksGame + InitialWarningDelay;
			this.mapRaidTimers[map] = Find.TickManager.TicksGame + InitialRaidDelay;
		}
		else
		{
			this.mapWarningTimers[map] = Find.TickManager.TicksGame + warningDelayRange.RandomInRange;
			this.mapRaidTimers[map] = Find.TickManager.TicksGame + raidDelayRange.RandomInRange;
		}
		this.mapRaidCounts[map] = 0;
	}

	public void Notify_QuestCompleted() => this.questCompleted = true;

	private void FireRaid(Map map) => this.FireRaid_NewTemp(map, 1.5f, (float)Settings_PMechs_Custom.RaidStrengthMin);

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
