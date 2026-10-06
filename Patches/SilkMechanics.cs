using HarmonyLib;
using Needleforge.Attacks;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

// This is to make melee attacks give double silk and
// adds a press delay to avoid some issues at low silk.
[HarmonyPatch]
internal static class SilkMechanics {
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void ThreadCost() {
		if (!YenCrest.IsEquipped)
			return;

		LastWeave = Time.time;
		MaskTrade.MaskCost();
	}

	private static float LastWeave = -1f;

	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void WeaveGain(ref HitInstance hitInstance) {
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
		MaskTrade.MaskCost();
	}

	private static string? lastHeroClip;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
        private static void DashAttackCost(HeroController __instance) {
                string? clip = __instance.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name;
		if (clip != lastHeroClip && YenCrest.IsEquipped) {
			if (clip == "Dash Attack Antic 1" || clip == "Dash Attack Antic 3") {
				LastWeave = Time.time;
				MaskTrade.MaskCost();
			}
			else if (clip == "Dash Attack Antic 2")
				LastWeave = Time.time;
		}
		lastHeroClip = clip;
	}
}

	// At 0 silk, attacking will break a mask for silk
	internal static class MaskTrade {
		private const int Cost = 1; //number of masks broken
		private const int Silk = 3; //value of silk given

		private static GameObject? effectObj;
		private static tk2dSpriteAnimator? effectAnim;

	internal static void MaskCost() {
		if (PlayerData.instance.silk <= 0
			&& !HalfSilk.GetHasHalfSilk()
			&& PlayerData.instance.health > Cost) { // never on her last mask

		//Sigh this is stupid... why can't it just work out of the box. (charge and dash freeze otherwise)
			PlayerData.instance.TakeHealth(Cost, HeroController.instance.IsInLifebloodState, allowFracturedMaskBreak: false);
			EventRegister.SendEvent("HEALTH UPDATE");
			HeroController.instance.AddSilk(Silk, false, SilkSpool.SilkAddSource.Normal, false);
			HeroController.instance.SpriteFlash.flashFocusHeal();
			PlayEffect();
		}
		HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}

	//Manual reimplementation of mask break effect...
	private static void PlayEffect() {
		if (!Moveset.MaskBreakCollection || Moveset.MaskBreakClip == null)
			return;

		if (!effectObj || !effectAnim) {
			if (effectObj)
				Object.Destroy(effectObj);
			effectObj = new GameObject($"{YenId} Mask Break Effect");
			effectObj.transform.SetParent(HeroController.instance.transform, false);
			effectObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
			tk2dBaseSprite.AddComponent<tk2dSprite>(effectObj, Moveset.MaskBreakCollection, 0);
			effectAnim = effectObj.AddComponent<tk2dSpriteAnimator>();
			effectAnim.AnimationCompleted = (_, _) => effectObj!.SetActive(false);
		}

		effectObj!.SetActive(true);
		effectAnim!.Stop();
		effectAnim.Play(Moveset.MaskBreakClip);
	}
}
