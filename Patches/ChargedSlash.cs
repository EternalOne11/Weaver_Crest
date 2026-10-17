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

	private static readonly Vector2[] ChargeHitbox = [new(0f, 1.2f), new(-2.3f, 1.8f), new(-4.3f, 1.4f),
	new(-6.4f, 0.3f), new(-7.1f, -1.0f), new (-4.3f, -1.4f), new (-2.4f, -1.4f), new (0f, -1.2f),];

	private static readonly Vector2[] WindupHitbox = [new(1.8f,0.3f), new(-0.7f,0.3f), new(-1.5f,-0.4f),
	new(-1f,-1.2f), new(2.0f,-1.3f), new(2.4f,-0.3f)];

	// Main attack modifer
	private const float ChargedMultiplier = 2.75f;
	// Windup attack modifer
	private const float WindupMultiplier = 0.75f;

	// left and right air movement speed
	private const float ChargedMoveSpeed = 8f;

	//frame where the animation is split (into windup and slash)
	private const int ChargedSplit = 11;

	private const int WindupStartHit = 1, WindupEndHit = ChargedSplit - 4;

	internal static ChargedAttack.Step? ChargedMain => YenCrest.Moveset.ChargedSlash?.Steps[0];
	internal static ChargedAttack.Step? WindupStep => YenCrest.Moveset.ChargedSlash?.Steps[1];

	private static void ChargedSlash() {
		string[] chargedFiles = ["charged_e0000.png", "charged_e0001.png", "charged_e0002.png"];
		Texture2D[] chargedTex = LoadNamedTextures(chargedFiles);
		string[] chargedHornetFiles = ["chargeSlash_0000.png", "chargeSlash_0001.png", "chargeSlash_0002.png", "chargeSlash_0003.png",
			"chargeSlash_0004.png", "chargeSlash_0005.png", "chargeSlash_0006.png", "chargeSlash_0007.png",
			"chargeSlash_0008.png", "chargeSlash_0009.png", "chargeSlash_0010.png", "chargeSlash_0011.png",
			"chargeSlash_0012.png", "chargeSlash_0013.png", "chargeSlash_0014.png", "chargeSlash_0015.png",];
		Texture2D[] chargedHornetTex = LoadNamedTextures(chargedHornetFiles);

		Texture2D blank = new(1, 1) { name = "charged_windup_blank" };
		blank.SetPixel(0, 0, Color.clear);
		blank.Apply();

		tk2dSpriteCollectionData chargedData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. chargedTex, .. chargedHornetTex, blank],
			spriteCenters: [
				.. chargedTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(275f, -7f)),
				.. chargedHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(89f, 0f)),
				new Vector2(0.5f, 0.5f),
			]
		);
		Object.DontDestroyOnLoad(chargedData.gameObject);
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
				name = "Slash_Windup",
				fps = 20f,
				wrapMode = WrapMode.Once,
				frames = [
					.. chargedData.CreateFrames(chargedHornetTex.Take(ChargedSplit - 1).Select(t => t.name)),
					chargedData.CreateFrame(chargedHornetTex[ChargedSplit - 1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "Slash_Charged",
				fps = 16f,
				wrapMode = WrapMode.Once,
				frames = chargedData.CreateFrames(chargedHornetTex.Skip(ChargedSplit).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Weaver Windup Effect",
				fps = 20f,
				wrapMode = WrapMode.Once,
				frames = [
					.. chargedData.CreateFrames(Enumerable.Repeat(blank.name, WindupStartHit)),
					chargedData.CreateFrame(blank.name, triggerEvent: true),
					.. chargedData.CreateFrames(Enumerable.Repeat(blank.name, WindupEndHit - WindupStartHit - 1)),
					chargedData.CreateFrame(blank.name, triggerEvent: true),
				],
			},
		];
		chargedAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, chargedAnims.clips[1], chargedAnims.clips[2]];

		YenCrest.Moveset.ChargedSlash = new ChargedAttack {
			Name = "WeaverSlashCharged",
			PlayOnActivation = false,
			PlayStepsInSequence = false,
			Steps = [
				new ChargedAttack.Step {
					AnimName = "Weaver Charged Effect",
					Hitbox = ChargeHitbox,
				},
				new ChargedAttack.Step {
					AnimName = "Weaver Windup Effect",
					Hitbox = WindupHitbox,
				},
			],
		};
		YenCrest.Moveset.ChargedSlash.SetAnimLibrary(chargedAnims);
	}

	internal static void ChargedDamage() {
		if (ChargedMain?.GameObject is GameObject main)
			foreach (var de in main.GetComponentsInChildren<DamageEnemies>(true))
				de.nailDamageMultiplier = ChargedMultiplier;
		if (WindupStep?.GameObject is GameObject windup)
			foreach (var de in windup.GetComponentsInChildren<DamageEnemies>(true))
				de.nailDamageMultiplier = WindupMultiplier;
	}

	// Manual reimplementation of of recoil...
	private const int ChargedRecoilSteps = 5;
	private const float ChargedRecoilSpeed = 10f;

	internal static void ChargedKnockback() {
		HeroController.instance.Recoil(!HeroController.instance.cState.facingRight, ChargedRecoilSteps, ChargedRecoilSpeed);
	}

	private static void ChargedFsmEdit(PlayMakerFSM fsm, FsmState startState, out FsmState[] endStates) {
		FsmState attackState = fsm.AddState("Weaver Slash");
		endStates = [attackState];


		startState.AddMethod(() => {
			// Windup sound
			Sounds.PlayAttack("ChargedWindup");

			HeroController.instance.RelinquishControl();
			HeroController.instance.AffectedByGravity(true);
			Rigidbody2D rb = HeroController.instance.GetComponent<Rigidbody2D>();

			// Horizontal movement during charge attack, when in the air (ground movement disabled by CheckTouchingGround)
			HeroController.instance.StartCoroutine(ChargedMovement());
			IEnumerator ChargedMovement() {
				while (fsm.ActiveStateName == startState.Name || fsm.ActiveStateName == attackState.Name) {
					var cState = HeroController.instance.cState;
					if (!cState.recoiling && !cState.recoilingLeft && !cState.recoilingRight) {
						float input = HeroController.instance.CheckTouchingGround() ? 0f : InputHandler.Instance.inputActions.MoveVector.Vector.x;
						float dir = Mathf.Abs(input) > 0.3f ? Mathf.Sign(input) : 0f;
						rb.linearVelocity = new Vector2(dir * ChargedMoveSpeed, rb.linearVelocity.y);
					}
					yield return new WaitForFixedUpdate();
				}
			}
			HeroController.instance.SpriteFlash.flashFocusHeal();
			GameObject sphereFlash = fsm.FsmVariables.GetFsmGameObject("Sphere Flash").Value;
			if (sphereFlash) sphereFlash.SetActive(true);
			YenCrest.Moveset.ChargedSlash!.GameObject!.SetActive(true);
			foreach (var step in YenCrest.Moveset.ChargedSlash!.Steps)
				step.EndAttack();

			WindupStep?.StartAttack();
		});
		startState.AddActions(
			new Tk2dPlayAnimationWithEvents {
				gameObject = new(),
				clipName = "Slash_Windup",
				animationTriggerEvent = FsmEvent.Finished,
			}
		);
		startState.AddTransition(FsmEvent.Finished.name, attackState.name);

		attackState.AddMethod(() => {
			// Removes small the recoil
			fsm.FsmVariables.GetFsmString("Recoil Method").Value = "";

			WindupStep?.EndAttack();
			ChargedMain?.StartAttack();
		});
		attackState.AddAction(new Tk2dPlayAnimationWithEvents {
			gameObject = new(),
			clipName = "Slash_Charged",
			animationCompleteEvent = FsmEvent.Finished,
		});
	}
}

[HarmonyPatch]
internal static class ChargedPatch {
	private static float lastKnockback = -1f;

	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void KnockbackOnHit(ref HitInstance hitInstance) {
		if (!YenCrest.IsEquipped || Time.time - lastKnockback < 0.25f || !hitInstance.Source)
			return;

		GameObject? charged = YenCrest.Moveset.ChargedSlash?.GameObject;
		if (!charged || !hitInstance.Source.transform.IsChildOf(charged!.transform))
			return;

		lastKnockback = Time.time;
		Moveset.ChargedKnockback();
	}
}
