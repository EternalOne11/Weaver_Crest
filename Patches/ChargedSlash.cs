using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Needleforge.Attacks;
using Silksong.FsmUtil;
using Silksong.UnityHelper.Util;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	// Charge attack Damage multipler
	private const float ChargedMultiplier = 3f;

	private static void ChargedSlash() {
		string[] chargedFiles = ["charged_e0000.png", "charged_e0001.png", "charged_e0002.png"];
		Texture2D[] chargedTex = LoadNamedTextures(chargedFiles);
		string[] chargedHornetFiles = ["chargeSlash_0000.png", "chargeSlash_0001.png", "chargeSlash_0002.png", "chargeSlash_0003.png",
			"chargeSlash_0004.png", "chargeSlash_0005.png", "chargeSlash_0006.png", "chargeSlash_0007.png",
			"chargeSlash_0008.png", "chargeSlash_0009.png", "chargeSlash_0010.png", "chargeSlash_0011.png",
			"chargeSlash_0012.png", "chargeSlash_0013.png", "chargeSlash_0014.png",];
		Texture2D[] chargedHornetTex = LoadNamedTextures(chargedHornetFiles);

		tk2dSpriteCollectionData chargedData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. chargedTex, .. chargedHornetTex],
			spriteCenters: [
				.. chargedTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(270f, 0f)),
				.. chargedHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(chargedData.gameObject); //check if line is redundent
		chargedData.gameObject.name = $"{YenId}_ChargedAnim";
		chargedData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");

		heroShaderCollections.Add(chargedData);

		tk2dSpriteAnimation chargedAnims = chargedData.gameObject.AddComponent<tk2dSpriteAnimation>();
		chargedAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver Charged Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					chargedData.CreateFrame(chargedTex[0].name, triggerEvent: true),
					.. chargedData.CreateFrames(chargedTex.Skip(1).Take(chargedTex.Length - 2).Select(t => t.name)),
					chargedData.CreateFrame(chargedTex[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "Slash_Charged",
				fps = 16,
				wrapMode = WrapMode.Once,
				frames = [
					.. chargedData.CreateFrames(chargedHornetTex.Take(11).Select(t => t.name)),
					chargedData.CreateFrame(chargedHornetTex[11].name, triggerEvent: true),
					.. chargedData.CreateFrames(chargedHornetTex.Skip(12).Select(t => t.name)),
				],
			},
		];
		chargedAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, chargedAnims.clips[1]];

		YenCrest.Moveset.ChargedSlash = new ChargedAttack {
			Name = "WeaverSlashCharged",
			PlayOnActivation = false,
			PlayStepsInSequence = false,
			Steps = [
				new ChargedAttack.Step {
					AnimName = "Weaver Charged Effect",
					Hitbox = [new Vector2(-0.4f, 1.2f),new Vector2(-2.0f, 1.9f),new Vector2(-3.8f, 1.5f),new Vector2(-5.2f, 0.4f),
					new Vector2(-5.2f, -0.4f),new Vector2(-3.8f, -1.5f),new Vector2(-2.0f, -1.9f),new Vector2(-0.4f, -1.2f),],
				},
			],
		};
		YenCrest.Moveset.ChargedSlash.SetAnimLibrary(chargedAnims);
	}

	internal static void ChargedDamage() {
		GameObject? chargedObj = YenCrest.Moveset.ChargedSlash?.GameObject;
		if (chargedObj == null) return;

		foreach (var de in chargedObj.GetComponentsInChildren<DamageEnemies>(true))
			de.nailDamageMultiplier = ChargedMultiplier;
	}

	private static void ChargedFsmEdit(PlayMakerFSM fsm, FsmState startState, out FsmState[] endStates) {
		FsmState attackState = fsm.AddState("Weaver Slash");
		endStates = [attackState];

		startState.AddMethod(() => {
			HeroController.instance.RelinquishControlNotVelocity();
			HeroController.instance.SetStartWithDownSpikeEnd();
			HeroController.instance.SpriteFlash.flashFocusHeal();
			YenCrest.Moveset.ChargedSlash!.GameObject!.SetActive(true);
			foreach (var step in YenCrest.Moveset.ChargedSlash!.Steps)
				step.EndAttack();
		});
		startState.AddActions(
			new Tk2dPlayAnimationWithEvents {
				gameObject = new(),
				clipName = "Slash_Charged",
				animationTriggerEvent = FsmEvent.Finished,
			},
			new DecelerateV2 {
				gameObject = new(),
				deceleration = 0.6f,
				brakeOnExit = true,
			}
		);
		startState.AddTransition(FsmEvent.Finished.name, attackState.name);

		attackState.AddMethod(() => {
			HeroController.instance.StartCoroutine(PlayStepsFaster());
			IEnumerator PlayStepsFaster()
			{
				foreach (var step in YenCrest.Moveset.ChargedSlash!.Steps)
				{
					step.StartAttack();
					yield return new WaitForSeconds(0.15f);
				}
			}
		});
		attackState.AddAction(new Tk2dWatchAnimationEvents {
			gameObject = new(),
			animationCompleteEvent = FsmEvent.Finished,
		});
	}
}
