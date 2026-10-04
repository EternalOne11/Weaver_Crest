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

//Split into short and extended variants
internal static partial class Moveset {

	private static readonly Vector2[] Hitbox1 = [new(-0.3f, 0.5f), new(-1.8f, 1.6f), new(-3.2f, 1.2f), new(-4.0f, 0.2f),
		new(-3.4f, -0.5f), new(-2.0f, -0.4f), new(-0.6f, -0.1f)];
	private static readonly Vector2[] Hitbox2 = [new(-0.3f, -0.5f), new(-1.8f, -1.6f), new(-3.2f, -1.2f), new(-4.0f, -0.2f),
		new(-3.4f, 0.5f), new(-2.0f, 0.4f), new(-0.6f, 0.1f)];
	private static readonly Vector2[] Hitbox3 = [new(-0.3f, 0.5f), new(-1.8f, 1.6f), new(-3.2f, 1.2f), new(-4.0f, 0.2f),
		new(-3.4f, -0.5f), new(-2.0f, -0.4f), new(-0.6f, -0.1f)];
	private static readonly Vector2[] Hitbox4 = [new(-0.3f, -0.5f), new(-1.8f, -1.6f), new(-3.2f, -1.2f), new(-4.0f, -0.2f),
		new(-3.4f, 0.5f), new(-2.0f, 0.4f), new(-0.6f, 0.1f)];

	private static tk2dSpriteAnimationClip dashRecoverClip = null!;
	private static tk2dSpriteCollectionData dashData = null!;
	private static tk2dSpriteAnimation dashAnims = null!;

	private static void DashSlash() {
		string[] Effect1 = ["dashSlash_e0003.png", "dashSlash_e0004.png"];
		string[] Effect2 = ["dashSlash_e0005.png", "dashSlash_e0006.png"];
		string[] Effect3 = ["dashSlash_e0007.png", "dashSlash_e0008.png"];
		string[] Effect4 = ["dashSlash_e0009.png", "dashSlash_e0010.png"];
		string[] anticFrames = ["dashSlash_0000.png", "dashSlash_0001.png", "dashSlash_0002.png"];
		string[] slash1  = ["dashSlash_0003.png", "dashSlash_0004.png"];
		string[] slash2  = ["dashSlash_0005.png", "dashSlash_0006.png"];
		string[] slash3  = ["dashSlash_0007.png", "dashSlash_0008.png"];
		string[] slash4  = ["dashSlash_0009.png", "dashSlash_0010.png"];
		string[] recoverFrames = ["dashSlash_0011.png", "dashSlash_0012.png", "dashSlash_0013.png"];

		Texture2D[][] ef = [LoadNamedTextures(Effect1), LoadNamedTextures(Effect2),
			LoadNamedTextures(Effect3), LoadNamedTextures(Effect4)];
		Texture2D[] antic = LoadNamedTextures(anticFrames);
		Texture2D[][] slashes = [LoadNamedTextures(slash1), LoadNamedTextures(slash2),
			LoadNamedTextures(slash3), LoadNamedTextures(slash4)];
		Texture2D[] recover = LoadNamedTextures(recoverFrames);
		Texture2D[] all = [.. ef.SelectMany(f => f), .. antic, .. slashes.SelectMany(s => s), .. recover];

		tk2dSpriteCollectionData dashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: all,
			spriteCenters: [
				.. ef.SelectMany(f => f).Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(20f, 10f)), //slash offset shared by all 4... (maybe change?)
				.. antic.Concat(slashes.SelectMany(s => s)).Concat(recover).Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(dashData.gameObject);
		dashData.gameObject.name = $"{YenId}_DashAnim";
		dashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(dashData);

		dashAnims = dashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		dashAnims.clips = [.. ef.Select((frames, i) => new tk2dSpriteAnimationClip {
			name = $"Weaver Dash Effect {i + 1}",
			fps = 10,
			wrapMode = WrapMode.Once,
			frames = dashData.CreateFrames(frames.Select(t => t.name)),
		})];

		var hornetClips = new System.Collections.Generic.List<tk2dSpriteAnimationClip>();
		for (int i = 0; i < 4; i++) {
			hornetClips.Add(new tk2dSpriteAnimationClip {
				name = $"Dash Attack Antic {i + 1}",
				fps = 15f,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames((i == 0 ? antic : slashes[i].Take(1)).Select(t => t.name)),
			});
			hornetClips.Add(new tk2dSpriteAnimationClip {
				name = $"Dash Attack {i + 1}",
				fps = 10f,
				wrapMode = WrapMode.Once,
				frames = dashData.CreateFrames(slashes[i].Select(t => t.name)),
			});
		}
		dashRecoverClip = new tk2dSpriteAnimationClip {
			name = "Weaver Dash Recover",
			fps = 15f,
			wrapMode = WrapMode.Once,
			frames = dashData.CreateFrames(recover.Select(t => t.name)),
		};

		dashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, .. hornetClips];

		DashAttack.Step Step(string effect, Vector2[] hitbox) => new DashAttack.Step {
			AnimName = effect,
			AnimLibrary = dashAnims,
			Hitbox = hitbox,
		};

		YenCrest.Moveset.DashSlash = new DashAttack {
			Name = "WeaverSlashDash",
			Steps = [
				Step("Weaver Dash Effect 1", Hitbox1),
				Step("Weaver Dash Effect 2", Hitbox2),
				Step("Weaver Dash Effect 3", Hitbox3),
				Step("Weaver Dash Effect 4", Hitbox4),
			],
		};
	}

	internal static IEnumerator DashRecovery() {
		var heroAnim = HeroController.instance.GetComponent<tk2dSpriteAnimator>();
		var animCtrl = HeroController.instance.GetComponent<HeroAnimationController>();
		animCtrl.StopControl();
		yield return StepFrames(heroAnim, dashRecoverClip, 0f);
		if (heroAnim.Paused)
			heroAnim.Resume();
		animCtrl.StartControl();
	}

	internal static void HideSprint(bool hide) {
		Transform? burst = HeroController.instance.transform.Find("Effects/Hunter SprintAttackBurst");
		if (burst == null)
			return;
		foreach (var r in burst.GetComponentsInChildren<Renderer>(true))
			r.enabled = !hide;
	}

	private static GameObject? effectObj;
	private static tk2dSpriteAnimator? effectAnim;

	internal static void HideBurst(int slash) {
		if (!effectObj) {
			effectObj = new GameObject($"{YenId} Dash Effect");
			effectObj.transform.SetParent(HeroController.instance.transform, false);
			effectObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
			tk2dBaseSprite.AddComponent<tk2dSprite>(effectObj, dashData, 0);
			effectAnim = effectObj.AddComponent<tk2dSpriteAnimator>();
			effectAnim.Library = dashAnims;
		}

		effectObj!.SetActive(true);
		effectAnim!.Stop();
		effectAnim.Play(dashAnims.GetClipByName($"Weaver Dash Effect {slash}"));
	}

	internal static void PlayBurst() {
		if (effectObj)
			effectObj!.SetActive(false);
	}

	internal static void HideAttackSprites() {
		var dash = YenCrest.Moveset.DashSlash;
		if (dash == null)
			return;
		foreach (var step in dash.Steps)
			if (step.GameObject)
				foreach (var r in step.GameObject!.GetComponentsInChildren<Renderer>(true))
					r.enabled = false;
	}
}

//Short or Extended variant code
[HarmonyPatch]
internal static class DashPatch {
	private static string? lastState;
	private static bool inDashAttack;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void DashAttack() {
		if (!YenCrest.IsEquipped)
			return;

		PlayMakerFSM fsm = HeroController.instance.sprintFSM;
		string state = fsm.Fsm.ActiveStateName;
		FsmInt totalSlashes = fsm.FsmVariables.GetFsmInt("Attack Steps");
		FsmInt currentSlash = fsm.FsmVariables.GetFsmInt("Attack Step");

		if (inDashAttack && state == "Attack Dash" && currentSlash.Value == 2)
			totalSlashes.Value = HeroController.instance.inputHandler.inputActions.Attack.IsPressed ? 4 : 2;

		Moveset.HideSprint(inDashAttack);
		if (inDashAttack)
			Moveset.HideAttackSprites();

		if (state == lastState)
			return;
		lastState = state;

		if (state == "Attack Antic" && !inDashAttack) {
			inDashAttack = true;
			totalSlashes.Value = 4;
		}
		else if (state == "Attack Dash" && inDashAttack) {
			Moveset.HideBurst(currentSlash.Value);
		}
		else if (state == "Skid Anims?" && inDashAttack) {
			inDashAttack = false;
			Moveset.PlayBurst();
			HeroController.instance.StartCoroutine(Moveset.DashRecovery());
		}
		else if (state != "Attack Antic" && state != "Loop?") {
			inDashAttack = false;
			Moveset.PlayBurst();
		}
	}

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.Start))]
	[HarmonyPostfix]
	private static void FixDashGravity() {
		PlayMakerFSM fsm = HeroController.instance.sprintFSM;
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
