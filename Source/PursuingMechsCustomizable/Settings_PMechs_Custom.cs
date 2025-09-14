using System;
using Verse;
using UnityEngine;
using RimWorld;

namespace PursuingMechanoidsCustomizable;

public class Settings_PMechs_Custom : ModSettings
{
	public const int DefaultRaidDelayMin = 1080000;
	public const int DefaultRaidDelayMax = 2100000;
	public const int DefaultRaidStrengthMin = 5000;

	public static int RaidStrengthMin = DefaultRaidStrengthMin;
	public static int RaidDelayRangeMin = DefaultRaidDelayMin;
	public static int RaidDelayRangeMax = DefaultRaidDelayMax;

	public IntRange test;

	public override void ExposeData()
	{
		base.ExposeData();

		if (Settings_PMechs_Custom.RaidDelayRangeMax < Settings_PMechs_Custom.RaidDelayRangeMin)
			Settings_PMechs_Custom.RaidDelayRangeMax = Settings_PMechs_Custom.RaidDelayRangeMin;

		Scribe_Values.Look(ref Settings_PMechs_Custom.RaidDelayRangeMin, "RaidDelayRangeMin", DefaultRaidDelayMin);
		Scribe_Values.Look(ref Settings_PMechs_Custom.RaidDelayRangeMax, "RaidDelayRangeMax", DefaultRaidDelayMax);
		Scribe_Values.Look(ref Settings_PMechs_Custom.RaidStrengthMin, "RaidStrengthMin", DefaultRaidStrengthMin);
	}
}

public class SettingsMenu_PMechs_Custom : Mod
{
	// I don't care that this can be readonly
	private Settings_PMechs_Custom settings;

	public SettingsMenu_PMechs_Custom(ModContentPack content) : base(content)
	{
		this.settings = GetSettings<Settings_PMechs_Custom>();
	}

	public override void DoSettingsWindowContents(Rect inRect)
	{
		string buffer1, buffer2, buffer3;
		bool button_pressed;

		buffer1 = Settings_PMechs_Custom.RaidDelayRangeMin.ToString();
		buffer2 = Settings_PMechs_Custom.RaidDelayRangeMax.ToString();
		buffer3 = Settings_PMechs_Custom.RaidStrengthMin.ToString();

		Listing_Standard listingStandard = new Listing_Standard();
		listingStandard.Begin(inRect);
		listingStandard.Label("Raid minimum delay (1 day = 60000)");
		listingStandard.TextFieldNumeric(ref Settings_PMechs_Custom.RaidDelayRangeMin, ref buffer1);
		listingStandard.Label("Raid maximum delay (1 day = 60000)");
		listingStandard.TextFieldNumeric(ref Settings_PMechs_Custom.RaidDelayRangeMax, ref buffer2);
		listingStandard.Label("Raid minumum strength");
		listingStandard.TextFieldNumeric(ref Settings_PMechs_Custom.RaidStrengthMin, ref buffer3);
		listingStandard.Label("Settings presets");
		button_pressed = listingStandard.ButtonText("Default");
		listingStandard.End();

		base.DoSettingsWindowContents(inRect);

		if (button_pressed)
		{
			Settings_PMechs_Custom.RaidStrengthMin = Settings_PMechs_Custom.DefaultRaidStrengthMin;
			Settings_PMechs_Custom.RaidDelayRangeMin = Settings_PMechs_Custom.DefaultRaidDelayMin;
			Settings_PMechs_Custom.RaidDelayRangeMax = Settings_PMechs_Custom.DefaultRaidDelayMax;
		}
	}

	public override string SettingsCategory()
	{
		return "PursuingMechsCustom";
	}
}