using HarmonyLib;
using Needleforge.Data;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {
	private static GameObject? animLibObj;

	private static tk2dSpriteAnimation sharedLib = null!;

	// For Sprite collections that need Hero shader
	private static readonly List<tk2dSpriteCollectionData> heroShaderCollections = [];

	#region Called from Awake

	internal static void ImportAnimations() {
		sharedLib = GetOrCreateAnimationLibrary();
	}

	internal static void SetupSlashes() {
		// Individual attack .cs files
		// Add silkless versions later
		Slash();
		SlashAlt();
		WallSlash();
		UpSlash();
		DownSlash();
		DashSlash();
		ChargedSlash();
		BindAnims(); //in Bind.cs

		sharedLib.isValid = false;
		sharedLib.ValidateLookup();
	}

	#endregion

	// Applying hero shader to attack sprites
	internal static void ApplyHeroShader() {
		HeroController hero = HeroController.instance;

		tk2dSprite? heroSprite = hero.GetComponentInChildren<tk2dSprite>();
		if (heroSprite == null) return;

		Shader heroShader = heroSprite.GetCurrentSpriteDef().material.shader;
		foreach (var data in heroShaderCollections)
			foreach (var def in data.spriteDefinitions)
				if (def != null)
					def.material.shader = heroShader;
	}

	// OnInitialized not from Awake
	internal static void EditHeroConfig() {
		HeroConfigNeedleforge yenConfig = YenCrest.Moveset.HeroConfig!;

		yenConfig.heroAnimOverrideLib = GetOrCreateAnimationLibrary();

		yenConfig.downSlashType = HeroControllerConfig.DownSlashTypes.DownSpike;
		yenConfig.SetDownspikeFields(
			anticTime: DownAnticTime, time: DownKickTime, recoveryTime: DownRecoveryTime,
			doesThrust: true, velocity: DownThrustVelocity, doesBurstEffect: false
		);

		yenConfig.canBind = true;
		yenConfig.SetCanUseAbilities(true);
		yenConfig.SetAttackFields(time: 0.33f, recovery: 0.15f, cooldown: 0.39f, // Regular attack speeds
			quickSpeedMult: 1.5f, quickCooldown: 0.205f); // Flea Brew

		yenConfig.SetDashStabFields(time: 0.15f, speed: -26f, bounceJumpSpeed: 20f);

		yenConfig.ChargedSlashFsmEdit = ChargedFsmEdit;
	}

	private static GameObject?[] AttackObjects() {
		var m = YenCrest.Moveset;
		return [m.Slash?.GameObject, m.AltSlash?.GameObject, m.UpSlash?.GameObject,
			m.WallSlash?.GameObject, m.DownSlash?.GameObject, m.DashSlash?.GameObject, m.ChargedSlash?.GameObject];
	}

	// Needleforge oversight... (you need to manually readd the NailSlashTerrainThunk, to not double hit the wall.
	private static readonly AccessTools.FieldRef<NailSlashTerrainThunk, float> ThunkCooldown =
		AccessTools.FieldRefAccess<NailSlashTerrainThunk, float>("cooldown");

	internal static void TerrainHitCooldown() {
		foreach (GameObject? attack in AttackObjects())
			if (attack)
				foreach (var thunk in attack!.GetComponentsInChildren<NailSlashTerrainThunk>(true))
					ThunkCooldown(thunk) = 0.3f;
	}

	//Stupid Barnacles
	internal static readonly HashSet<DamageEnemies> OurDamagers = [];

        internal static bool IsOurAttack(GameObject? obj) {
                if (!obj)
                        return false;
                foreach (GameObject? attack in AttackObjects())
                        if (attack && obj!.transform.IsChildOf(attack!.transform))
                                return true;
                return false;
        }
	internal static void DetectBarnacle() {
		int attackLayer = LayerMask.NameToLayer("Attack");
		OurDamagers.Clear();
		foreach (GameObject? attack in AttackObjects()) {
			if (!attack)
				continue;
			foreach (var damager in attack!.GetComponentsInChildren<DamageEnemies>(true)) {
				OurDamagers.Add(damager);
				GameObject hitbox = damager.gameObject;
				if (attackLayer >= 0)
					hitbox.layer = attackLayer;
				if (!hitbox.GetComponent<Rigidbody2D>()) {
					var body = hitbox.AddComponent<Rigidbody2D>();
					body.bodyType = RigidbodyType2D.Kinematic;
					body.simulated = true;
				}
			}
		}
	}

	private static Texture2D[] LoadNamedTextures(string[] files) =>
		files.Select(f => {
			Texture2D t = LoadEmbeddedPngAsTexture2D(f);
			t.name = f;
			return t;
		}).ToArray();
	private static tk2dSpriteAnimation GetOrCreateAnimationLibrary() {
		if (animLibObj)
			return animLibObj.GetComponent<tk2dSpriteAnimation>();

		animLibObj = new GameObject($"{YenId}_AnimLib");
		Object.DontDestroyOnLoad(animLibObj);
		var animLib = animLibObj.AddComponent<tk2dSpriteAnimation>();
		animLib.clips = [];
		return animLib;
	}
}

[HarmonyPatch]
internal static class BarnacleHit {
	[HarmonyPatch(typeof(DamageEnemies), nameof(DamageEnemies.GetDamageDealt))]
	[HarmonyPostfix]
	private static void ReportNeedleDamage(DamageEnemies __instance, ref int __result) {
		if (__result <= 0 && Moveset.OurDamagers.Contains(__instance))
			__result = Mathf.Max(1, PlayerData.instance.nailDamage);
	}
}

[HarmonyPatch]
internal static class HalfKnockback {
	private const float KnockbackMult = 0.5f;

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.Recoil), [typeof(bool), typeof(int), typeof(float)])]
	[HarmonyPrefix]
	private static void HalveRecoil(ref float __2) {
		if (!YenCrest.IsEquipped || !KnockbackSetting.Value)
			return;

		ToolItem belt = GlobalSettings.Gameplay.WeightedAnkletTool;
		__2 *= belt && belt.IsEquipped ? 0f : KnockbackMult;
	}
}
