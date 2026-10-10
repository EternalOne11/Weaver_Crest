using HarmonyLib;
using HutongGames.PlayMaker;
using Needleforge.Attacks;
using System.Collections;
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

	internal static void MaskCost() {
		if (PlayerData.instance.silk <= 0
			&& !HalfSilk.GetHasHalfSilk()
			&& PlayerData.instance.health > Cost) { // never on her last mask

		//Sigh this is stupid... why can't it just work out of the box. (charge and dash freeze otherwise)
			PlayerData.instance.TakeHealth(Cost, HeroController.instance.IsInLifebloodState, allowFracturedMaskBreak: false);
			EventRegister.SendEvent("HEALTH UPDATE");
			HeroController.instance.AddSilk(Silk, false, SilkSpool.SilkAddSource.Normal, false);
			HeroController.instance.SpriteFlash.flashFocusHeal();
			PlayFlash();
		}
		HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}

	private static GameObject? flash;
	private static Vector3 flashScale;
	private static int flashRun;

	//Manual reimplementation of mask break effect...
	private static void PlayFlash() {
		if (!flash) {
			flash = HeroController.instance.GetComponents<PlayMakerFSM>()
				.Select(fsm => fsm.FsmVariables.FindFsmGameObject("Sphere Flash")?.Value)
				.FirstOrDefault(obj => obj);
			if (!flash)
				return;
		}

		if (flashRun == 0 || !flashRestore)
			flashScale = flash!.transform.localScale;
		flash!.transform.localScale = flashScale * 0.7f;
		flash.SetActive(false);
		flash.SetActive(true);

		flashRestore = true;
		HeroController.instance.StartCoroutine(RestoreFlashSize(++flashRun));
	}

	private static bool flashRestore;

	// disables the scale change unless for the interaction above
	private static IEnumerator RestoreFlashSize(int run) {
		yield return new WaitForSeconds(0.5f);
		if (run != flashRun)
			yield break;
		if (flash)
			flash!.transform.localScale = flashScale;
		flashRestore = false;
	}
}
