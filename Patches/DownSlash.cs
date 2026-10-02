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

	private const float DownFps = 15f;
	private const float DownEndHold = 0.05f;

	private const int DownAnticFrames = 7, DownKickFrames = 3, DownRecoverFrames = 3;

	internal const float DownAnticTime = DownAnticFrames / DownFps;//windup
	internal const float DownThrustTime = DownKickFrames / DownFps;
	internal const float DownRecoveryTime = DownRecoverFrames / DownFps + DownEndHold;

	internal const float PogoSpeed   = 18f;
	internal const float PogoTime    = 0.1f;
	internal const float PogoGravity = 45f;


	private static readonly Vector2[] DownHitbox = [new(0.6f, -0.9f), new(-0.9f, -0.9f),new(-1.9f, 1.0f), new(-2.4f, 0.8f), new(-1.3f, -1.4f), new(0.6f, -1.4f),
	];

	private static tk2dSpriteAnimationClip anticClip = null!, kickClip = null!, recoverClip = null!;

	internal static bool DownAnimLocked { get; private set; }
	internal static float DownAnimUnlockAt { get; private set; }

	private static void DownSlash() {
		string[] fxFiles = ["downSlash_e0004.png", "downSlash_e0005.png"];
		string[] anticFiles = ["downSlash_0000.png", "downSlash_0001.png", "downSlash_0002.png", "downSlash_0003.png", "downSlash_0004.png", "downSlash_0005.png", "downSlash_0006.png"];
		string[] kickFiles = ["downSlash_0007.png", "downSlash_0008.png", "downSlash_0009.png"];
		string[] recoverFiles = ["downSlash_0010.png", "downSlash_0011.png", "downSlash_0012.png"];

		Texture2D[] fx = LoadNamedTextures(fxFiles);
		Texture2D[] antic = LoadNamedTextures(anticFiles);
		Texture2D[] kick = LoadNamedTextures(kickFiles);
		Texture2D[] recover = LoadNamedTextures(recoverFiles);
		Texture2D[] all = [.. fx, .. antic, .. kick, .. recover];

		tk2dSpriteCollectionData data = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: all,
			spriteCenters: [.. all.Select(t => new Vector2(t.width, t.height) * 0.5f)]
		);
		Object.DontDestroyOnLoad(data.gameObject);
		data.gameObject.name = $"{YenId}_DownSlashAnim";
		data.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(data);

		anticClip = new tk2dSpriteAnimationClip {
			name = "DownSpike Antic",
			fps = DownFps,
			wrapMode = WrapMode.Once,
			frames = data.CreateFrames(antic.Select(t => t.name)),
		};
		kickClip = new tk2dSpriteAnimationClip {
			name = "DownSpike",
			fps = DownFps,
			wrapMode = WrapMode.Once,
			frames = data.CreateFrames(kick.Select(t => t.name)),
		};
		recoverClip = new tk2dSpriteAnimationClip {
			name = "Downspike Recovery",
			fps = DownFps,
			wrapMode = WrapMode.Once,
			frames = data.CreateFrames(recover.Select(t => t.name)),
		};

		tk2dSpriteAnimation anims = data.gameObject.AddComponent<tk2dSpriteAnimation>();
		anims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver DownSlash Effect",
				fps = DownFps,
				wrapMode = WrapMode.Once,
				frames = [
					data.CreateFrame(fx[0].name, triggerEvent: true),
					data.CreateFrame(fx[1].name),
					data.CreateFrame(fx[1].name, triggerEvent: true),
				],
			},
			anticClip,
			kickClip,
			recoverClip,
		];
		anims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, .. anims.clips.Skip(1)];

		YenCrest.Moveset.DownSlash = new DownAttack {
			Name = "WeaverDownSlash",
			AnimName = "Weaver DownSlash Effect",
			AnimLibrary = anims,
			Hitbox = DownHitbox,
		};
	}

	internal static bool DownAnticStarted(HeroController hc) =>
		!DownAnimLocked && hc.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name == anticClip.name;

	internal static IEnumerator DownSequence(HeroController hc) {
		var heroAnim = hc.GetComponent<tk2dSpriteAnimator>();
		hc.GetComponent<HeroAnimationController>().StopControl();
		DownAnimLocked = true;
		DownAnimUnlockAt = Time.time + DownAnticTime + DownThrustTime + DownRecoveryTime;

		yield return StepFrames(heroAnim, anticClip, 0f);
		yield return StepFrames(heroAnim, kickClip, 0f);
		yield return StepFrames(heroAnim, recoverClip, DownEndHold);

		ReleaseDownAnim(hc);
	}

	internal static IEnumerator StepFrames(tk2dSpriteAnimator hero, tk2dSpriteAnimationClip clip, float hold) {
		float duration = clip.frames.Length / clip.fps;
		for (float elapsed = 0f; elapsed < duration + hold; ) {
			if (hero.CurrentClip != clip) {
				hero.Play(clip);
				hero.Pause();
			}
			hero.SetFrame(Mathf.Min((int)(elapsed * clip.fps), clip.frames.Length - 1));

			yield return null;
			if (Time.timeScale > 0f)
				elapsed += Time.unscaledDeltaTime;
		}
	}

	internal static IEnumerator Pogo(HeroController hc) {
		Rigidbody2D rb = hc.GetComponent<Rigidbody2D>();
		Vector2 v = new(rb.linearVelocity.x, PogoSpeed);

		for (float t = 0f; t < 1f && (t < PogoTime || hc.cState.downSpiking || DownAnimLocked); t += Time.fixedDeltaTime) {
			if (t > PogoTime && hc.CheckTouchingGround())
				break;
			if (t >= PogoTime)
				v.y -= PogoGravity * Time.fixedDeltaTime;
			rb.linearVelocity = v;
			yield return new WaitForFixedUpdate();
		}
		hc.AffectedByGravity(true);
	}

	internal static void ReleaseDownAnim(HeroController hc) {
		if (!DownAnimLocked)
			return;
		DownAnimLocked = false;

		var heroAnim = hc.GetComponent<tk2dSpriteAnimator>();
		if (heroAnim.Paused)
			heroAnim.Resume();
		hc.GetComponent<HeroAnimationController>().StartControl();
	}
}

[HarmonyPatch]
internal static class DownPatch {
	private static bool pogoUsed;

	[HarmonyPatch(typeof(HeroController), "FixedUpdate")]
	[HarmonyPostfix]
	private static void DownAttackStart(HeroController __instance) {
		if (Moveset.DownAnimLocked && Time.time > Moveset.DownAnimUnlockAt + 0.1f)
			Moveset.ReleaseDownAnim(__instance);

		if (YenCrest.IsEquipped && Moveset.DownAnticStarted(__instance)) {
			pogoUsed = false;
			__instance.StartCoroutine(Moveset.DownSequence(__instance));
		}
	}

	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void DownAttackPogo(ref HitInstance hitInstance) {
		if (pogoUsed || !YenCrest.IsEquipped)
			return;

		GameObject? down = YenCrest.Moveset.DownSlash?.GameObject;
		if (down == null || hitInstance.Source == null || !hitInstance.Source.transform.IsChildOf(down.transform))
			return;

		pogoUsed = true;
		HeroController.instance.StartCoroutine(Moveset.Pogo(HeroController.instance));
	}
}
