using HarmonyLib;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	// regular attacks and recovery lengths, these must match the "time" fields in moveset.cs.
	internal const float NormalAttackTime = 0.33f;
	internal const float SlashRecoveryTime = 0.15f;

	// Effect timing: starts on hornets 2nd frame and lasts 3 of her frames (vanilla does frames 1 to 3 of 5)
	internal const int SlashHornetFrames = 5;
	internal const int SlashEffectFirstFrame = 2;
	internal const int SlashEffectFrames = 3;
	private const float SlashHornetFrameTime = NormalAttackTime / SlashHornetFrames;

	private static readonly Vector2[] SlashHitbox = [new(0f,0.3f), new(-1.2f,0.6f), new(-2.1f,0.7f),
	new(-2.7f,0.1f), new(-2.7f,-0.6f), new(-2.2f,-1.3f), new(-1.3f,-1.5f), new(0.2f,-0.8f)];

	private static void Slash() {
		string[] slashFiles = ["slashAlt_e0000.png", "slashAlt_e0001.png", "slashAlt_e0002.png", "slashAlt_e0003.png", "slashAlt_e0004.png"]; //sigh
		Texture2D[] slashTex = LoadNamedTextures(slashFiles);
		string[] slashHornetFiles = ["slash0000.png", "slash0001.png", "slash0002.png", "slash0003.png", "slash0004.png"];
		Texture2D[] slashHornetTex = LoadNamedTextures(slashHornetFiles);
		Texture2D blank = BlankTexture("slash_blank");

		tk2dSpriteCollectionData slashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. slashTex, .. slashHornetTex, blank],
			spriteCenters: [
				.. slashTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(128f, -30f)),
				.. slashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
				new Vector2(0.5f, 0.5f),
			]
		);
		Object.DontDestroyOnLoad(slashData.gameObject);
		slashData.gameObject.name = $"{YenId}_SlashAnim";
		slashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");

		heroShaderCollections.Add(slashData);

		tk2dSpriteAnimation slashAnims = slashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		slashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver Slash Effect",
				fps = slashTex.Length / SlashHornetFrameTime,
				wrapMode = WrapMode.Once,
				frames = EffectFrames(slashData, slashTex, blank),
			},
			new tk2dSpriteAnimationClip {
				name = "Slash",
				fps = 1f / SlashHornetFrameTime,
				wrapMode = WrapMode.Once,
				frames = slashData.CreateFrames(slashHornetTex.Select(t => t.name)),
			},
		];
		slashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, slashAnims.clips[1]];

		YenCrest.Moveset.Slash = new Attack {
			Name = "WeaverSlash",
			AnimName = "Weaver Slash Effect",
			AnimLibrary = slashAnims,
			Hitbox = SlashHitbox,
		};
	}

	// Blank image to add a extra frame before the slash effect so it lines up with animations.
	private static Texture2D BlankTexture(string name) {
		Texture2D blank = new(1, 1) { name = name };
		blank.SetPixel(0, 0, Color.clear);
		blank.Apply();
		return blank;
	}

	private static tk2dSpriteAnimationFrame[] EffectFrames(tk2dSpriteCollectionData data, Texture2D[] images, Texture2D blank) {
		List<tk2dSpriteAnimationFrame> frames = [];
		for (int i = 0; i < (SlashEffectFirstFrame - 1) * images.Length; i++)
			frames.Add(data.CreateFrame(blank.name));
		for (int i = 0; i < images.Length; i++)
			for (int r = 0; r < SlashEffectFrames; r++)
				frames.Add(data.CreateFrame(images[i].name, triggerEvent: i == 0 && r == 0));
		frames[^1] = data.CreateFrame(images[^1].name, triggerEvent: true);
		return [.. frames];
	}
}

//Cancels slash and hitbox when turning
[HarmonyPatch]
internal static class SlashFlipCancel {
	[HarmonyPatch(typeof(HeroController), "Awake")]
	[HarmonyPostfix]
	private static void Subscribe(HeroController __instance) {
		__instance.FlippedSprite -= SlashTurn.OnTurn;
		__instance.FlippedSprite += SlashTurn.OnTurn;
	}

	[HarmonyPatch(typeof(NailSlash), nameof(NailSlash.StartSlash))]
	[HarmonyPrefix]
	private static void Restore(NailSlash __instance) {
		if (SlashTurn.Moved.Remove(__instance, out Vector3 position))
			__instance.transform.localPosition = position;
	}
}

internal static class SlashTurn {
	internal static readonly Dictionary<NailSlash, Vector3> Moved = [];

	internal static void OnTurn() {
		if (!YenCrest.IsEquipped || !HeroController.instance)
			return;
		foreach (GameObject? attack in new[] { YenCrest.Moveset.Slash?.GameObject, YenCrest.Moveset.AltSlash?.GameObject }) {
			if (!attack)
				continue;
			foreach (NailSlash slash in attack!.GetComponentsInChildren<NailSlash>()) {
				if (!slash.anim || !slash.anim.Playing)
					continue;

				slash.SetCollidersActive(false);
				slash.anim.AnimationEventTriggered = null;
				if (slash.EnemyDamager)
					slash.EnemyDamager.EndDamage();

				// Effect mirror to finish the slash effect
				Transform t = slash.transform;
				if (!Moved.ContainsKey(slash))
					Moved[slash] = t.localPosition;
				Vector3 position = t.position;
				position.x = 2f * HeroController.instance.transform.position.x - position.x;
				t.position = position;
				Vector3 scale = t.localScale;
				scale.x = -scale.x;
				t.localScale = scale;
			}
		}
	}
}
