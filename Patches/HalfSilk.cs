using HarmonyLib;
using Needleforge.Attacks;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
// This is to make melee attacks give double silk and
// adds a press delay to avoid some issues at low silk.
// At 0 silk, attacking breaks a mask for silk (a small effect plays during the attack's wind-up).
[HarmonyPatch]
internal static class SilkMechanics {
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void ThreadCost() {
		if (!YenCrest.IsEquipped)
			return;

		LastWeave = Time.time;
		MaskBreak.PayCost();
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
		MaskBreak.PayCost();
	}

	private static string? lastHeroClip;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void DashAttackCost(HeroController __instance) {
		string? clip = __instance.GetComponent<tk2dSpriteAnimator>().CurrentClip?.name;
		if (clip != lastHeroClip && YenCrest.IsEquipped) {
			if (clip == "Dash Attack Antic 1" || clip == "Dash Attack Antic 3") {
				LastWeave = Time.time;
				MaskBreak.PayCost();
			}
			else if (clip == "Dash Attack Antic 2")
				LastWeave = Time.time;
		}
		lastHeroClip = clip;
	}
}

internal static class MaskBreak {
	private const int Cost = 1; //number of masks broken
	private const int Silk = 3; //value of silk given

	private static GameObject? effectObj;
	private static tk2dSpriteAnimator? effectAnim;

	private static bool tradePending;
	private static int pendingCosts; // attack costs waiting for the silk

	// Used by every attack instead of paying its silk cost directly
	internal static void PayCost() {
		if (tradePending) { // silk is already on its way: this cost waits with the others
			pendingCosts++;
			return;
		}

		bool needed = PlayerData.instance.silk <= 0
			&& !HalfSilk.GetHasHalfSilk() // spend the half silk first
			&& PlayerData.instance.health > Cost; // never on her last mask
		if (!needed) {
			HalfSilk.RemoveHalfSilk(interruptRegen: false);
			return;
		}

		tradePending = true;
		pendingCosts = 1;

		// The mask breaks now
		HeroController hc = HeroController.instance;
		PlayerData.instance.TakeHealth(Cost, hc.IsInLifebloodState, allowFracturedMaskBreak: false);
		RefreshHealthHud();
		PlayEffect();

		// The silk arrives once the attack is over
		hc.StartCoroutine(GiveSilkAfterAttack());
	}

	private static IEnumerator GiveSilkAfterAttack() {
		yield return null;
		// Wait until no attack is running (max 3 seconds, in case one never reports finishing)
		for (float t = 0f; t < 3f && Attacking(); t += Time.deltaTime)
			yield return null;

		HeroController.instance.AddSilk(Silk, false, SilkSpool.SilkAddSource.Normal, false);
		HeroController.instance.SpriteFlash.flashFocusHeal();
		for (int i = 0; i < pendingCosts; i++)
			HalfSilk.RemoveHalfSilk(interruptRegen: false);

		tradePending = false;
		pendingCosts = 0;
	}

	private static bool Attacking() {
		HeroController hc = HeroController.instance;
		string sprintState = hc.sprintFSM.Fsm.ActiveStateName;
		return hc.cState.attacking
			|| Moveset.DownAnimLocked
			|| sprintState == "Attack Antic" || sprintState == "Attack Dash" || sprintState == "Loop?";
	}

	// Tells the HUD that health changed: the same event the game's own damage sends for the masks,
	// without the "Hornet was hit" events that reset the dash and charged attacks.
	private static void RefreshHealthHud() {
		EventRegister.SendEvent("HEALTH UPDATE");
	}

	// The mask break effect, attached to Hornet and drawn just in front of her (like the bind effect).
	// If anything it needs is missing, it skips the effect rather than breaking the attack.
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
