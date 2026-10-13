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

	private const float DownFps = 20f;
	private const int DownAnticFrames = 3, DownKickFrames = 6, DownFollowUpFrames = 4;

	internal const float DownAnticTime = DownAnticFrames / DownFps;
	internal const float DownKickTime = DownKickFrames / DownFps;
	internal const float DownFollowUpTime = DownFollowUpFrames / DownFps;

	private const float DownFlipTime = (DownKickFrames + DownFollowUpFrames) / DownFps;

	private static tk2dSpriteAnimationClip downFollowUpClip = null!;

	private static readonly Vector2[] DownHitbox = [new(0.6f, -0.9f), new(-0.9f, -0.9f), new(-1.9f, 1.0f),
		new(-2.4f, 0.8f), new(-1.3f, -1.4f), new(0.6f, -1.4f)];

	private static void DownSlash() {
		string[] fxFiles = ["downSlash_e0000.png", "downSlash_e0001.png", "downSlash_e0002.png",
			"downSlash_e0003.png", "downSlash_e0004.png", "downSlash_e0005.png"];
		string[] anticFiles = ["downSlash_0000.png", "downSlash_0001.png", "downSlash_0002.png"];
		string[] kickFiles = ["downSlash_0003.png", "downSlash_0004.png", "downSlash_0005.png",
			"downSlash_0006.png", "downSlash_0007.png", "downSlash_0008.png"];
		string[] followUpFiles = ["downSlash_0009.png", "downSlash_0010.png", "downSlash_0011.png", "downSlash_0012.png"];

		Texture2D[] fx = LoadNamedTextures(fxFiles);
		Texture2D[] antic = LoadNamedTextures(anticFiles);
		Texture2D[] kick = LoadNamedTextures(kickFiles);
		Texture2D[] followUp = LoadNamedTextures(followUpFiles);
		Texture2D[] all = [.. fx, .. antic, .. kick, .. followUp];

		tk2dSpriteCollectionData data = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: all,
			spriteCenters: [.. all.Select(t => new Vector2(t.width, t.height) * 0.5f)]
		);
		Object.DontDestroyOnLoad(data.gameObject);
		data.gameObject.name = $"{YenId}_DownSlashAnim";
		data.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(data);

		tk2dSpriteAnimation anims = data.gameObject.AddComponent<tk2dSpriteAnimation>();
		anims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver DownSlash Effect",
				fps = DownFps,
				wrapMode = WrapMode.Once,
				frames = [
					data.CreateFrame(fx[0].name, triggerEvent: true),
					.. data.CreateFrames(fx.Skip(1).Take(fx.Length - 2).Select(t => t.name)),
					data.CreateFrame(fx[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike Antic",
				fps = DownFps,
				wrapMode = WrapMode.Once,
				frames = data.CreateFrames(antic.Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike",
				fps = DownFps,
				wrapMode = WrapMode.Once,
				frames = data.CreateFrames(kick.Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Downspike Recovery",
				fps = DownFps,
				wrapMode = WrapMode.Once,
				frames = data.CreateFrames(followUp.Select(t => t.name)),
			},
		];
		anims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, .. anims.clips.Skip(1)];
		downFollowUpClip = anims.clips[3];

		YenCrest.Moveset.DownSlash = new DownAttack {
			Name = "WeaverDownSlash",
			AnimName = "Weaver DownSlash Effect",
			AnimLibrary = anims,
			Hitbox = DownHitbox,
		};
	}

	internal static bool Flipping { get; private set; }

	internal static string? PreAttackClip;

	internal static IEnumerator DownKickFlip() {
		Flipping = true;
		Rigidbody2D rb = HeroController.instance.GetComponent<Rigidbody2D>();
		var heroAnim = HeroController.instance.GetComponent<tk2dSpriteAnimator>();
		var animCtrl = HeroController.instance.GetComponent<HeroAnimationController>();
		float forward = HeroController.instance.cState.facingRight ? 1f : -1f;
		Vector2 drift = new(forward * 5f, 0f);
		bool followUpStarted = false;

		DownPatch.KeepDownAttack = true;
		HeroController.instance.RelinquishControlNotVelocity();
		DownPatch.KeepDownAttack = false;
		try {
			for (float t = 0f; t < DownFlipTime; t += Time.deltaTime) {
				if (HeroController.instance.cState.onGround)
					yield break;

				if (!followUpStarted && t >= DownKickTime) {
					followUpStarted = true;
					animCtrl.StopControl();
					heroAnim.Stop();
					heroAnim.Play(downFollowUpClip);
				}

				float angle = Mathf.Lerp(30f, 270f, t / DownFlipTime) * Mathf.Deg2Rad;
				float up = Mathf.Sin(angle);
				if (up > 0f)
					up *= 1.5f;
				Vector2 loop = new Vector2(Mathf.Cos(angle) * forward, up) * 7f;
				rb.linearVelocity = drift + loop + Vector2.down * (25f * t);
				yield return null;
			}
		}
		finally {
			HeroController.instance.RegainControl();
			HeroController.instance.RelinquishControlNotVelocity();
			if (followUpStarted)
				animCtrl.StartControl();
			HeroController.instance.RegainControl();
			if (!HeroController.instance.cState.onGround && PreAttackClip != null)
				heroAnim.Play(PreAttackClip);
			Flipping = false;
		}
	}
}

[HarmonyPatch]
internal static class DownPatch {
	private static string? lastClip;
	private static bool anticSeen;

	internal static bool KeepDownAttack;

	[HarmonyPatch(typeof(HeroController), "CancelDownSpike")]
	[HarmonyPrefix]
	private static bool SkipCancelDownSpike() => !KeepDownAttack;

	[HarmonyPatch(typeof(HeroController), "ResetAttacks")]
	[HarmonyPrefix]
	private static bool SkipResetAttacks() => !KeepDownAttack;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void PushOnKick(HeroController __instance) {
		string? clip = __instance.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name;
		if (clip != lastClip && YenCrest.IsEquipped) {
			if (clip == "DownSpike Antic") {
				Moveset.PreAttackClip = lastClip;
				anticSeen = true;
			}
			else if (clip == "DownSpike" && anticSeen && !Moveset.Flipping) {
				anticSeen = false;
				__instance.StartCoroutine(Moveset.DownKickFlip());
			}
		}
		lastClip = clip;
	}
}
