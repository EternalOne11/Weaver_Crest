using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Needleforge;
using Needleforge.Attacks;
using Needleforge.Data;
using Weaver_Crest.Patches;
using TeamCherry.Localization;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using SlotInfo = ToolCrest.SlotInfo;

namespace Weaver_Crest;

/*Patches contain
Custom Hud implementation
Custom Bind mechanics

Note that moveset and Evo optout are still from queencrest.
(evooptout will disable once I implement memory lockets)
*/

[BepInAutoPlugin(id: "io.github.eternalone11.Weaver_Crest")]
[BepInDependency("org.silksong-modding.i18n")]
[BepInDependency("io.github.needleforge", "0.9.0")]
[BepInDependency("org.silksong-modding.unityhelper", "1.1.1")]
[BepInDependency("org.silksong-modding.fsmutil", "0.3.16")]
public partial class Weaver_CrestPlugin : BaseUnityPlugin
{
	private Harmony Harmony { get; } = new(Id);
	internal static ManualLogSource Log { get; private set; }

	internal static CrestData YenCrest { get; private set; }

	// Config Settings
	internal static ConfigEntry<bool> HoldToBindSetting { get; private set; } = null!;
	internal static ConfigEntry<SkillSlotChoice> SkillSlotSetting { get; private set; } = null!;
	internal static ConfigEntry<bool> KnockbackSetting { get; private set; } = null!;
	internal static ConfigEntry<bool> RefundSetting { get; private set; } = null!;
	internal static ConfigEntry<bool> WispCostSetting { get; private set; } = null!;

	internal const string
		YenId = "Weaver";
	internal static readonly LocalisedString
		YenName = new($"Mods.{Id}", "CREST_NAME"),
		YenDesc = new($"Mods.{Id}", "CREST_DESC");

	private static Color AdminGreen { get; }
		= new Color32(r: 89, g: 255, b: 152, a: 255);
	internal static Color AttackColor { get; }
		= Color.Lerp(Color.white, AdminGreen, 0.3f);

	private void Awake()
	{
		Log = Logger;
		Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

		HoldToBindSetting = Config.Bind("Bind", "Hold bind", true,
			"Bind now requires holding the button down, the entire time, letting go early cancels it.");
		SkillSlotSetting = Config.Bind("Crest", "Silk skill bind slot", SkillSlotChoice.Top,
			"When binding a silk skill, on weaver crest, it will autoequip it to the selected slot, none means middle, which in this case leaves it empty.");
		KnockbackSetting = Config.Bind("Balance", "Half knockback", false,
			"Reduces knockback in half, making it the same as witch crest.");
		RefundSetting = Config.Bind("Silk", "Essential Refunds", true,
				"Attacks that on breable walls, doors, spikes, and shellshard walls refund silk");
		WispCostSetting = Config.Bind("Silk", "Halves the cost Wisp Lantern", false,
			"The Wisp Lantern now costs half a silk instead when equiped on the Weaver crest.");

		Harmony.PatchAll(typeof(Moveset));
		Harmony.PatchAll(typeof(HalfSilk));
		Harmony.PatchAll(typeof(EvaOptOut));
		Harmony.PatchAll(typeof(SilkMechanics));
		Harmony.PatchAll(typeof(HalfSilkHeart));
		Harmony.PatchAll(typeof(ToolDamage));
		Harmony.PatchAll(typeof(UpPatch));
		Harmony.PatchAll(typeof(WallPatch));
		Harmony.PatchAll(typeof(ChargedPatch));
		Harmony.PatchAll(typeof(DashPatch));
		Harmony.PatchAll(typeof(DownPatch));
		Harmony.PatchAll(typeof(AttackGrunts));
		Harmony.PatchAll(typeof(HalfKnockback));
		Harmony.PatchAll(typeof(BarnacleHit));
		Harmony.PatchAll(typeof(BreakableRefund));
		Harmony.PatchAll(typeof(ShardWallRefund));
		Harmony.PatchAll(typeof(SkillAutoEquip));
		Harmony.PatchAll(typeof(BindSoundBlock));
		Harmony.PatchAll(typeof(SpaHalfSilk));
		Harmony.PatchAll(typeof(EnemySilkDrain));
		Harmony.PatchAll(typeof(WispHalfCost));
		Harmony.PatchAll(typeof(CountHalfSilk));
		
		RegisterCrest();
		HalfSilk.Initialize();
		LocketSpawn.Initialize();

		Moveset.ImportAnimations();
		Moveset.SetupSlashes();
		Sounds.Load();

		YenCrest.Moveset.OnInitialized += () =>
		{
			Moveset.EditHeroConfig();

			// Hornet shaders
			Moveset.ApplyHeroShader();

			// Charge damage mulitpler
			Moveset.ChargedDamage();

			Moveset.FixDownKB();

			// One terrain hit per swing... needleforge oversight.
			Moveset.TerrainHitCooldown();

			//... Stupid Barnacles
			Moveset.DetectBarnacle();

			// Longclaw
			var m = YenCrest.Moveset;
			foreach (var attack in new GameObjectProxy?[] { m.Slash, m.AltSlash, m.UpSlash, m.WallSlash, m.DownSlash, m.DashSlash })
			{
				if (attack == null || attack.GameObject == null) continue;
				foreach (var nab in attack.GameObject.GetComponentsInChildren<NailAttackBase>(true))
				{
					nab.overrideLongNeedleScale = true;
					nab.longNeedleScale = Vector3.Scale(nab.scale, new Vector3(1.2f, 1.075f, 1f));
				}
			}
		};
	}

	private void OnDestroy() => Harmony.UnpatchSelf();

	private static void RegisterCrest()
	{
		Vector2 pivot = new(0.5f, 0.44f);

		Sprite
			linework = LoadEmbeddedPngAsSprite("Crest.png", pivot),
			silhouette = LoadEmbeddedPngAsSprite("CrestSilhouette.png", pivot),
			glow = LoadEmbeddedPngAsSprite("CrestEquipGlow.png", pivot, 101f);

		float slotOffset = (0.5f - pivot.y) * (linework.rect.height / linework.pixelsPerUnit);

		YenCrest = NeedleforgePlugin.AddCrest(YenId, YenName, YenDesc, linework, silhouette, glow);
		YenCrest.HudFrame.ProfileIcon = LoadEmbeddedPngAsSprite("CrestIcon.png");// Save profile for normal save
		YenCrest.HudFrame.SteelProfileIcon = LoadEmbeddedPngAsSprite("CrestIcon_S.png"); // Save profile for Steel soul

		//Tool slot locations (x,y)
	    // X is the horizontal axis, Negative values move the image left, Positive move them right.
	    // Y is the Vertical axis, Negative values move it Down, Postive move it Up
		YenCrest.AddSkillSlot(AttackToolBinding.Up,new(0f, 1.47f + slotOffset), false);
		YenCrest.AddSkillSlot(AttackToolBinding.Down,new(0f, -2.19f + slotOffset), false);
		YenCrest.AddRedSlot(AttackToolBinding.Neutral,new(0f, -0.36f + slotOffset), false);

		// Yellow and blue slots: locked behind memory lockets
		// 4 are placed in the world, exact locations in LocketSpawn.cs
		// But they are in Mount fay, Putrified Ducts, Wispthicket/underworks coennction, and Citidel Spa.
		YenCrest.AddYellowSlot(new(-1.57f, 0.4f + slotOffset), true);
		YenCrest.AddYellowSlot(new(-1.57f, -1.1f + slotOffset), true);
		YenCrest.AddBlueSlot(new( 1.57f, 0.4f + slotOffset), true);
		YenCrest.AddBlueSlot(new( 1.57f, -1.1f + slotOffset), true);

		YenCrest.ApplyAutoSlotNavigation(slotDimensions: new(1.25f, 0.75f));

		// Hud and Bind
		Hud.Setup(YenCrest);
		YenCrest.BindEvent = Bind.TripleBind;
	}
}
