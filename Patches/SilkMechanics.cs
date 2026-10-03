using HarmonyLib;
using Needleforge.Attacks;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
// This is to make melee attacks give double silk and
// adds a press delay to avoid some issues at low silk.
// At 0 silk, Hornet breaks a mask for silk.
[HarmonyPatch]
internal static class SilkMechanics {
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void ThreadCost() {
		if (!YenCrest.IsEquipped)
			return;

		LastWeave = Time.time;
		HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}

	private static float LastWeave = -1f;
	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void WeaveGain(HealthManager __instance, ref HitInstance hitInstance) {
		if (!YenCrest.IsEquipped)
			return;

		bool isNailHit =
			hitInstance.AttackType == AttackTypes.Nail
			|| (hitInstance.AttackType == AttackTypes.Heavy && hitInstance.IsNailTag);
		if (!isNailHit)
			return;

		const float WeaveWindow = 0.5f;
		if (Time.time - LastWeave >= WeaveWindow)
			return;

		HeroController.instance.AddSilk(1, true, SilkSpool.SilkAddSource.Normal, false);
	}

	[HarmonyPatch(typeof(AttackBase), nameof(AttackBase.StartAttack))]
	[HarmonyPostfix]
	private static void ChargedAttackCost(AttackBase __instance) {
		if (!YenCrest.IsEquipped)
			return;

		if (!(YenCrest.Moveset.ChargedSlash?.Steps.Contains(__instance) ?? false))
			return;

		LastWeave = Time.time;
		HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}
	
	private static string? lastHeroClip;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void DashAttackCost(HeroController __instance) {
		string? clip = __instance.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name;
		if (clip != lastHeroClip && YenCrest.IsEquipped) {
			if (clip == "Dash Attack Antic 1") {
				LastWeave = Time.time;
				HalfSilk.RemoveHalfSilk(interruptRegen: false);
			}
			else if (clip == "Dash Attack Antic 2")
				LastWeave = Time.time;
		}
		lastHeroClip = clip;
	}

	//Mask break code.
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPrefix]
	private static bool ZeroSilk() {
		if (!YenCrest.IsEquipped || !MaskBreak.ShouldStart())
			return true;

		HeroController.instance.StartCoroutine(MaskBreak.Play());
		return false;
	}
}

	internal static class MaskBreak {
	private const int Cost = 1; //number of masks broken
	private const int Silk = 3; //value of silk given
	private const int Frame = 2; //Frame of animation where mask breaks
	private static bool Breaking;
	
	internal static bool ShouldStart() =>
		!Breaking
		&& !Moveset.DownAnimLocked
		&& PlayerData.instance.silk <= 0
		&& !HalfSilk.GetHasHalfSilk() // Spend half silk, now. 
		&& PlayerData.instance.health > Cost // never on her last mask
		&& HeroController.instance.CanInput();


	internal static IEnumerator Play() {
		Breaking = true;
		HeroController.instance.RelinquishControl();

		var clip = Moveset.MaskBreakClip;
		var heroAnim = HeroController.instance.GetComponent<tk2dSpriteAnimator>();
		var animCtrl = HeroController.instance.GetComponent<HeroAnimationController>();
		animCtrl.StopControl();

		float toBreak = Mathf.Min(Frame, clip.frames.Length - 1) / clip.fps;
		float total = clip.frames.Length / clip.fps;
		HeroController.instance.StartCoroutine(Moveset.StepFrames(heroAnim, clip, 0f));
		yield return new WaitForSeconds(toBreak);
		MaskForSilk();
		yield return new WaitForSeconds(total - toBreak);

		if (heroAnim.Paused)
			heroAnim.Resume();
		animCtrl.StartControl();

		HeroController.instance.RegainControl();
		Breaking = false;
	}

	private static void MaskForSilk() {
		HeroController.instance.TakeHealth(Cost);
		HeroController.instance.AddSilk(Silk, false, SilkSpool.SilkAddSource.Normal, false);
		HeroController.instance.SpriteFlash.flashFocusHeal();
	}
}
