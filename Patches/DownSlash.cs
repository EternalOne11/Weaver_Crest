using GlobalEnums;
using HarmonyLib;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private const float DownAnticFps = 12f;
	private const float DownKickFps = 16f;
	private const float DownRecoveryFps = 17f;
	private const int DownAnticFrames = 2, DownKickFrames = 3, DownRecoveryFrames = 4;

	internal const float DownAnticTime = DownAnticFrames / DownAnticFps;
	internal const float DownKickTime = DownKickFrames / DownKickFps;
	internal const float DownRecoveryTime = DownRecoveryFrames / DownRecoveryFps;

	internal const float ThrustAngle = 30f;
	internal const float ThrustSpeed = 25f;

	internal static Vector2 DownThrustVelocity => new(
		-ThrustSpeed * Mathf.Sin(ThrustAngle * Mathf.Deg2Rad),
		-ThrustSpeed * Mathf.Cos(ThrustAngle * Mathf.Deg2Rad));

	internal const float DownCooldown = DownAnticTime + DownKickTime + DownRecoveryTime + 0.05f;

	private static readonly float[] DownEffectOffsetY = [-60f, -39f, -21f, -18f, -14f];

	private static readonly Vector2[] DownHitbox = [new(0.6f, -0.2f), new(-0.9f, -0.2f), new(-1.2f, 1.0f),
		new(-2.6f, 1.0f), new(-1.6f, -1.6f), new(0.6f, -1.6f)];

	private static tk2dSpriteCollectionData downData = null!;
	private static tk2dSpriteAnimation downAnims = null!;
	private static tk2dSpriteAnimationClip downRecoveryClip = null!;
	private static GameObject? downEffectObj;
	private static tk2dSpriteAnimator? downEffectAnim;

	private static void DownSlash() {
		string[] fxFiles = ["downSlash_e0000.png", "downSlash_e0001.png", "downSlash_e0002.png",
			"downSlash_e0003.png", "downSlash_e0004.png"];
		string[] hornetFiles = ["downSlash_0000.png", "downSlash_0001.png", "downSlash_0002.png", "downSlash_0003.png",
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
				.. hornet.Select(t => new Vector2(t.width, t.height) * 0.5f),
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
		];
		downAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, .. downAnims.clips.Skip(1)];
		downRecoveryClip = downAnims.GetClipByName("Downspike Recovery");

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

	internal static IEnumerator DownRecovery() {
		HeroController.instance.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;

		var heroAnim = HeroController.instance.GetComponent<tk2dSpriteAnimator>();
		var animCtrl = HeroController.instance.GetComponent<HeroAnimationController>();
		animCtrl.StopControl();
		try {
			float duration = downRecoveryClip.frames.Length / downRecoveryClip.fps;
			for (float t = 0f; t < duration; t += Time.deltaTime) {
				if (heroAnim.CurrentClip != downRecoveryClip) {
					int frame = Mathf.Min((int)(t * downRecoveryClip.fps), downRecoveryClip.frames.Length - 1);
					heroAnim.PlayFromFrame(downRecoveryClip, frame);
				}
				yield return null;
			}
		}
		finally {
			animCtrl.StartControl();
		}
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
			else if (clip == "Downspike Recovery")
				__instance.StartCoroutine(Moveset.DownRecovery());
		}
		lastClip = clip;
	}

	// Manual cooldown
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void DownAttackCooldown(AttackDirection __0, ref float ___attack_cooldown) {
		if (YenCrest.IsEquipped && __0 == AttackDirection.downward)
			___attack_cooldown = Moveset.DownCooldown;
	}
}
