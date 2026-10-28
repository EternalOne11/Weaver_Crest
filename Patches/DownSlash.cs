using GlobalEnums;
using HarmonyLib;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private const float DownAnticFps = 12f;
	private const float DownKickFps = 16f;
	private const float DownRecoveryFps = 13f;
	private const int DownAnticFrames = 2, DownKickFrames = 3, DownRecoveryFrames = 3;

	internal const float DownAnticTime = DownAnticFrames / DownAnticFps;
	internal const float DownKickTime = DownKickFrames / DownKickFps;
	internal const float DownRecoveryTime = DownRecoveryFrames / DownRecoveryFps;

	internal const float ThrustAngle = 30f;
	internal const float ThrustSpeed = 22f;

	internal static Vector2 DownThrustVelocity => new(
		-ThrustSpeed * Mathf.Sin(ThrustAngle * Mathf.Deg2Rad),
		-ThrustSpeed * Mathf.Cos(ThrustAngle * Mathf.Deg2Rad));

	internal const float DownCooldown = DownAnticTime + DownKickTime + DownRecoveryTime + 0.01f;

	private static readonly float[] DownEffectOffsetY = [-60f, -39f, -21f, -18f, -14f];

	private static readonly Vector2[] DownHitbox = [new(0.5f,-0.7f), new(-0.2f,0.3f), new(-0.9f,0.9f),
	new(-1.6f,0.4f), new(-1.9f,-0.7f), new(-1.4f,-1.9f), new(-0.2f,-2.5f), new(0.9f,-2.1f) ];

	private static tk2dSpriteCollectionData downData = null!;
	private static tk2dSpriteAnimation downAnims = null!;
	private static GameObject? downEffectObj;
	private static tk2dSpriteAnimator? downEffectAnim;

	private static void DownSlash() {
		string[] fxFiles = ["downSlash_e0000.png", "downSlash_e0001.png", "downSlash_e0002.png",
			"downSlash_e0003.png", "downSlash_e0004.png"];
		string[] hornetFiles = ["downSlash_0000.png", "downSlash_0001.png", "downSlash_0002.png",
		"downSlash_0004.png", "downSlash_0005.png", "downSlash_0006.png", "downSlash_0007.png", "downSlash_0008.png"];

		Texture2D[] fx = LoadNamedTextures(fxFiles);
		Texture2D[] hornet = LoadNamedTextures(hornetFiles);
		Texture2D[] antic = [.. hornet.Take(DownAnticFrames)];
		Texture2D[] kick = [.. hornet.Skip(DownAnticFrames).Take(DownKickFrames)];
		Texture2D[] followUp = [.. hornet.Skip(DownAnticFrames + DownKickFrames).Take(DownRecoveryFrames)];
		Texture2D[] all = [.. fx, .. hornet];

		downData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: all,
			spriteCenters: [
				.. fx.Select((t, i) => new Vector2(t.width, t.height) * 0.5f
					+ new Vector2(30f, DownEffectOffsetY[i])),
				.. hornet.Select((t, i) => new Vector2(t.width, t.height) * 0.5f
					+ (i >= DownAnticFrames + DownKickFrames ? new Vector2(38f, 0f) : Vector2.zero)), //Offsets recovery animation to line up with hornets move hitbox.
			]
		);
		Object.DontDestroyOnLoad(downData.gameObject);
		downData.gameObject.name = $"{YenId}_DownSlashAnim";
		downData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(downData);

		downAnims = downData.gameObject.AddComponent<tk2dSpriteAnimation>();
		downAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver DownSlash Effect",
				fps = fx.Length / DownKickTime,
				wrapMode = WrapMode.Once,
				frames = downData.CreateFrames(fx.Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike Antic",
				fps = DownAnticFps,
				wrapMode = WrapMode.Once,
				frames = downData.CreateFrames(antic.Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike",
				fps = DownKickFps,
				wrapMode = WrapMode.Once,
				frames = downData.CreateFrames(kick.Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Downspike Recovery",
				fps = DownRecoveryFps,
				wrapMode = WrapMode.Once,
				frames = downData.CreateFrames(followUp.Select(t => t.name)),
			},
			// Vanilla behavior to fix some visual issues...
			new tk2dSpriteAnimationClip {
				name = "Downspike Recovery Land",
				fps = DownRecoveryFps,
				wrapMode = WrapMode.Once,
				frames = downData.CreateFrames(followUp.Select(t => t.name)),
			},
		];
		downAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, .. downAnims.clips.Skip(1)];

		YenCrest.Moveset.DownSlash = new DownAttack {
			Name = "WeaverDownSlash",
			AnimName = "Weaver DownSlash Effect",
			AnimLibrary = downAnims,
			Hitbox = DownHitbox,
		};
	}

	// Attaches hitbox to account for velocity
	internal static void PlayDownEffect() {
		if (!downEffectObj) {
			downEffectObj = new GameObject($"{YenId} DownSlash Effect");
			downEffectObj.transform.SetParent(HeroController.instance.transform, false);
			downEffectObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
			tk2dBaseSprite.AddComponent<tk2dSprite>(downEffectObj, downData, 0);
			downEffectAnim = downEffectObj.AddComponent<tk2dSpriteAnimator>();
			downEffectAnim.Library = downAnims;
			downEffectAnim.AnimationCompleted = (_, _) => downEffectObj!.SetActive(false);
		}

		downEffectObj!.SetActive(true);
		downEffectAnim!.Stop();
		downEffectAnim.Play(downAnims.GetClipByName("Weaver DownSlash Effect"));
	}

        // Moves hornet hitbox backwards to line up with the animation
	internal static void ShiftForRecovery(HeroController hc) {
		float facing = hc.cState.facingRight ? 1f : -1f;
		float shift = 38f / 64f; //64 is PerUnit
		RaycastHit2D wall = Physics2D.Raycast(hc.transform.position, new Vector2(-facing, 0f), shift + 0.1f,
			LayerMask.GetMask("Terrain"));
		if (wall)
			shift = Mathf.Max(0f, wall.distance - 0.1f);
		hc.transform.position += new Vector3(-facing * shift, 0f, 0f);
	}

	internal static void HideSprite() {
		GameObject? down = YenCrest.Moveset.DownSlash?.GameObject;
		if (!down)
			return;
		foreach (var r in down!.GetComponentsInChildren<Renderer>(true))
			r.enabled = false;
	}

	private static readonly AccessTools.FieldRef<NailSlashTravel, HeroController> TravelHero =
		AccessTools.FieldRefAccess<NailSlashTravel, HeroController>("hc");

	//recoil fix
	internal static void FixDownKB() {
		GameObject? down = YenCrest.Moveset.DownSlash?.GameObject;
		if (!down)
			return;
		foreach (var travel in down!.GetComponentsInChildren<NailSlashTravel>(true))
			TravelHero(travel) = HeroController.instance;
	}
}

[HarmonyPatch]
internal static class DownPatch {
	private static string? lastClip;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void EffectOnThrust(HeroController __instance) {
		if (HeroController.instance != __instance || !YenCrest.IsEquipped)
			return;
		var heroAnim = __instance.GetComponent<tk2dSpriteAnimator>();
		if (!heroAnim)
			return;

		Moveset.HideSprite();

		string? clip = heroAnim.CurrentClip?.name;
		if (clip != lastClip) {
			if (clip == "DownSpike")
				Moveset.PlayDownEffect();
			// Vanilla downspike recovery
			else if (clip is "Downspike Recovery" or "Downspike Recovery Land"
				&& lastClip is not ("Downspike Recovery" or "Downspike Recovery Land"))
				Moveset.ShiftForRecovery(__instance);
		}
		lastClip = clip;
	}

	// Interrupted vs normal cooldown for down attack
	private static float downAttackEnd = -1f;

	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void DownAttackCooldown(AttackDirection __0, ref float ___attack_cooldown) {
		if (!YenCrest.IsEquipped)
			return;
		if (__0 == AttackDirection.downward) {
			___attack_cooldown = Moveset.DownCooldown;
			downAttackEnd = Time.time + Moveset.DownCooldown;
		}
		else
			downAttackEnd = -1f;
	}

	// prevents dashing until down attack recovery frames... (for visual issue)
	[HarmonyPatch(typeof(HeroController), nameof(HeroController.CanDash))]
	[HarmonyPostfix]
	private static void NoDashDuringDownAttack(HeroController __instance, ref bool __result) {
		if (!__result || !YenCrest.IsEquipped || Time.time >= downAttackEnd)
			return;
		var state = __instance.cState;
		string? clip = __instance.GetComponent<tk2dSpriteAnimator>()?.CurrentClip?.name;
		if (state.downSpikeAntic || state.downSpiking || clip is "DownSpike Antic" or "DownSpike")
			__result = false;
	}
}
