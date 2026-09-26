using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Needleforge.Attacks;
using Silksong.FsmUtil;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {
	private static void DashSlash() {
		string[] dashFiles = ["dashSlash_e0000.png", "dashSlash_e0001.png", "dashSlash_e0002.png", "dashSlash_e0003.png",
			"dashSlash_e0004.png", "dashSlash_e0005.png", "dashSlash_e0006.png", "dashSlash_e0007.png",
			"dashSlash_e0008.png", "dashSlash_e0009.png", "dashSlash_e0010.png", "dashSlash_e0011.png",
			"dashSlash_e0012.png", "dashSlash_e0013.png",];
		Texture2D[] dashTex = LoadNamedTextures(dashFiles);
		string[] dashHornetFiles = ["dashSlash_0000.png", "dashSlash_0001.png", "dashSlash_0002.png", "dashSlash_0003.png",
			"dashSlash_0004.png", "dashSlash_0005.png", "dashSlash_0006.png", "dashSlash_0007.png",
			"dashSlash_0008.png", "dashSlash_0009.png", "dashSlash_0010.png", "dashSlash_0011.png",
			"dashSlash_0012.png", "dashSlash_0013.png",];
		Texture2D[] dashHornetTex = LoadNamedTextures(dashHornetFiles);

		tk2dSpriteCollectionData dashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. dashTex, .. dashHornetTex],
			spriteCenters: [
				.. dashTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
				.. dashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(dashData.gameObject);
		dashData.gameObject.name = $"{YenId}_DashAnim";
		dashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		
		heroShaderCollections.Add(dashData);

		tk2dSpriteAnimation dashAnims = dashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		dashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver Dash Effect 1",
				fps = 15,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashTex.Take(6).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Weaver Dash Effect 2",
				fps = 15,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashTex.Skip(6).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Dash Attack Antic 1",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashHornetTex.Take(1).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Dash Attack 1",
				fps = 15,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashHornetTex.Take(6).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Dash Attack Antic 2",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashHornetTex.Skip(6).Take(1).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Dash Attack 2",
				fps = 15,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(dashHornetTex.Skip(6).Select(t => t.name)),
			},
		];
		dashAnims.ValidateLookup();
		sharedLib.clips = [
			.. sharedLib.clips,
			dashAnims.clips[2], dashAnims.clips[3], dashAnims.clips[4], dashAnims.clips[5],
		];

		YenCrest.Moveset.DashSlash = new DashAttack {
			Name = "WeaverSlashDash",

			Steps = [
				new DashAttack.Step {
					AnimName = "Weaver Dash Effect 1",
					AnimLibrary = dashAnims,
					Hitbox = [new Vector2(-0.3f, 0.5f),new Vector2(-1.8f, 1.6f),new Vector2(-3.2f, 1.2f),new Vector2(-4.0f, 0.2f),
					new Vector2(-3.4f, -0.5f),new Vector2(-2.0f, -0.4f),new Vector2(-0.6f, -0.1f),],
				},
				new DashAttack.Step {
					AnimName = "Weaver Dash Effect 2",
					AnimLibrary = dashAnims,
					Hitbox = [new Vector2(-0.3f, -0.5f),new Vector2(-1.8f, -1.6f),new Vector2(-3.2f, -1.2f),new Vector2(-4.0f, -0.2f),
					new Vector2(-3.4f, 0.5f),new Vector2(-2.0f, 0.4f),new Vector2(-0.6f, 0.1f),],
				},
			],
		};
	}

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.Start))]
	[HarmonyPostfix]
	private static void FixDashGravity(HeroController __instance) {
		PlayMakerFSM fsm = __instance.sprintFSM;
		if (!fsm.Fsm.preprocessed)
			fsm.Preprocess();

		FsmState? setGravity = fsm.GetState("Set Gravity");
		if (setGravity == null)
			return;

		var equipCheck = new CheckIfCrestEquipped {
			Crest = new FsmObject() { Value = YenCrest.ToolCrest },
			trueEvent = FsmEvent.GetFsmEvent("FINISHED"),
			falseEvent = FsmEvent.GetFsmEvent(""),
			storeValue = false,
		};
		setGravity.InsertAction(0, equipCheck);
	}
}
