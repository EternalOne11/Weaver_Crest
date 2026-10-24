using GlobalEnums;
using HarmonyLib;
using HutongGames.PlayMaker;
using Needleforge.Attacks;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE004

namespace Weaver_Crest.Patches;

// This is to make melee attacks give double silk and
// adds a press delay to avoid some issues at low silk.
[HarmonyPatch]
internal static class SilkMechanics {
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPrefix]
	private static void MuteGruntForTrade() {
		if (YenCrest.IsEquipped && MaskTrade.DuringTrade())
			Sounds.MuteForTrade();
	}

	// Mask break sound code
	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void ThreadCost(AttackDirection __0, bool ___wallSlashing) {
		if (!YenCrest.IsEquipped)
			return;

		LastWeave = Time.time;
		BreakableRefund.StartSwing(deferredCost: MaskTrade.MaskCost(normalAttack: true));

		Sounds.PlayAttack(__0 switch {
			AttackDirection.upward => "UpSlash",
			AttackDirection.downward => "DownSlash",
			_ when ___wallSlashing => "WallSlash",
			_ => HeroController.instance.cState.altAttack ? "Slash" : "AltSlash",
		});
	}

	private static float LastWeave = -1f;

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.SilkGain), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void WeaveGain() {
		if (!YenCrest.IsEquipped)
			return;

		if (Time.time - LastWeave >= MaskTrade.WeaveWindow)
			return;

		BreakableRefund.HitEnemy();

		if (MaskTrade.CancelCost()) {
			HalfSilk.AddHalfSilk(heroEffect: false);
			return;
		}

		HeroController.instance.AddSilk(1, false, SilkSpool.SilkAddSource.Normal, false);
	}

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.AddSilk), [typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool)])]
	[HarmonyPostfix]
	private static void GainSettlesHeldCost(int __0) {
		if (__0 <= 0 || !YenCrest.IsEquipped || !MaskTrade.CancelCost())
			return; // nothing gained, or no cost on hold
		BreakableRefund.HitEnemy();
		if (PlayerData.instance.silk > SilkSpool.BindCost)
			HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void PayMissed(HeroController __instance) {
		if (HeroController.instance != __instance)
			return;
		BreakableRefund.EndSwingIfOver();
		MaskTrade.PayCosts();
	}

	[HarmonyPatch(typeof(AttackBase), nameof(AttackBase.StartAttack))]
	[HarmonyPostfix]
	private static void ChargedAttackCost(AttackBase __instance) {
		if (!YenCrest.IsEquipped)
			return;

		if (__instance != Moveset.ChargedMain)
			return;

		LastWeave = Time.time;
		MaskTrade.MaskCost();
		Sounds.PlayAttack("ChargedSlash");
	}

	private static string? lastHeroClip;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void DashAttackCost(HeroController __instance) {
		if (HeroController.instance != __instance || !YenCrest.IsEquipped)
			return;
		var heroAnim = __instance.GetComponent<tk2dSpriteAnimator>();
		if (!heroAnim)
			return;

		string? clip = heroAnim.CurrentClip?.name;
		if (clip != lastHeroClip && clip != null && clip.StartsWith("Dash Attack Antic ")) {
			LastWeave = Time.time;

			bool firstOrThird = clip == "Dash Attack Antic 1" || clip == "Dash Attack Antic 3";
			Sounds.PlayAttack(firstOrThird ? "DashSlashA" : "DashSlashB");

			if (firstOrThird)
				MaskTrade.MaskCost();
		}
		lastHeroClip = clip;
	}
}

// At 0 silk, attacking breaks a mask for silk
internal static class MaskTrade {
	private const int Cost = 1; //number of masks broken
	private const int Silk = 3; //value of silk given
	private const int LifebloodSilk = 1; //value of silk given for a lifeblood mask

	internal static bool DuringTrade() =>
		PlayerData.instance.silk <= 0
		&& !HalfSilk.GetHasHalfSilk()
		&& (PlayerData.instance.healthBlue > 0 || PlayerData.instance.health > Cost); //Never Last Mask

	internal static bool MaskCost(bool normalAttack = false) {
		if (DuringTrade()) {
			Sounds.MuteForTrade(); //Pain grunt replaces normal attack grunt

                // Lifeblood mask value changes
			bool tradedLifeblood = PlayerData.instance.healthBlue > 0;
			PlayerData.instance.TakeHealth(Cost, HeroController.instance.IsInLifebloodState, allowFracturedMaskBreak: false);
			HeroController.instance.damageEffectFSM.SendEvent("DAMAGE"); // the black damage vignette
			EventRegister.SendEvent("HEALTH UPDATE");

			if (PlayerData.instance.health == 1)
				EventRegister.SendEvent("HERO DAMAGED");
			HeroController.instance.AddSilk(tradedLifeblood ? LifebloodSilk : Silk, false, SilkSpool.SilkAddSource.Normal, false);
			HeroController.instance.SpriteFlash.flashFocusHeal();
			Sounds.PlayHurt();
			PlayFlash();
		}
		//visual delay at 9 silk to prevent a greater visual annoyance
		bool DipBelowBind = PlayerData.instance.silk == SilkSpool.BindCost && !HalfSilk.GetHasHalfSilk();
		if (DipBelowBind) {
			pendingCosts.Enqueue((Time.time, normalAttack));
			return true;
		}
		HalfSilk.RemoveHalfSilk(interruptRegen: false);
		return false;
	}

	internal static bool Swinging() {
		var state = HeroController.instance.cState;
		return state.attacking || state.downSpikeAntic || state.downSpiking || state.downAttacking;
	}

	internal const float WeaveWindow = 0.5f;
	private static readonly System.Collections.Generic.Queue<(float time, bool normalAttack)> pendingCosts = new();

	internal static bool CancelCost() {
		if (pendingCosts.Count == 0)
			return false;
		pendingCosts.Dequeue();
		return true;
	}

	internal static void PayCosts() {
		if (!YenCrest.IsEquipped) {
			pendingCosts.Clear();
			return;
		}
		bool swinging = Swinging();
		while (pendingCosts.Count > 0) {
			var (time, normalAttack) = pendingCosts.Peek();
			float waited = Time.time - time;
			bool canStillHit = waited < WeaveWindow && (!normalAttack || swinging);
			if (canStillHit)
				break;
			pendingCosts.Dequeue();
			HalfSilk.RemoveHalfSilk(interruptRegen: false);
		}
	}

	// A flash played is played when a mask is broken/traded.
	private static GameObject? flash;
	private static Vector3 flashScale;
	private static int flashRun;
	private static bool flashRestore;

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

// Built in half silk heart, when you have no silk hearts. (only at 0 silk, and at normal regen speed)
[HarmonyPatch]
internal static class HalfSilkHeart {
	private static bool regenerating;

	private static bool Applies(PlayerData pd) =>
		YenCrest.IsEquipped && pd.CurrentSilkRegenMax <= 0 && pd.silk <= 0
		&& !HalfSilk.GetHasHalfSilk() && pd.CurrentSilkMax > 0;

	private static float WithWhiteRing(float time) =>
		GlobalSettings.Gameplay.WhiteRingTool.IsEquipped ? time * GlobalSettings.Gameplay.WhiteRingSilkRegenTimeMultiplier : time;

	[HarmonyPatch(typeof(HeroController), "StartSilkRegen")]
	[HarmonyPrefix]
	private static bool StartHalfRegen() {
		regenerating = false;
		if (!Applies(PlayerData.instance))
			return true;
		SilkSpool.Instance.SetRegen(1, false);
		regenerating = true;
		return false;
	}

	[HarmonyPatch(typeof(HeroController), "StartSilkRegen")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	private static void SetHalfRegenTime(ref float ___silkRegenDurationLeft, float ___SILK_REGEN_DURATION) {
		if (regenerating)
			___silkRegenDurationLeft = WithWhiteRing(___SILK_REGEN_DURATION);
	}

	[HarmonyPatch(typeof(HeroController), "DoSilkRegen")]
	[HarmonyPrefix]
	private static bool SkipFullSilk() => !regenerating;

	[HarmonyPatch(typeof(HeroController), "DoSilkRegen")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	private static void GiveHalfSilk() {
		if (!regenerating)
			return;
		regenerating = false;
		SilkSpool.Instance.SetRegen(0, false);
		if (!HalfSilk.GetHasHalfSilk())
			HalfSilk.AddHalfSilk(heroEffect: false);
	}
	
	private static bool wasAtBench;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void RestoreAtBench(HeroController __instance, ref float ___silkRegenDurationLeft) {
		if (HeroController.instance != __instance)
			return;
		bool atBench = __instance.playerData.atBench;
		if (atBench && !wasAtBench && Applies(__instance.playerData)) {
			if (regenerating) {
				regenerating = false;
				___silkRegenDurationLeft = 0f;
				SilkSpool.Instance.SetRegen(0, false);
			}
			HalfSilk.AddHalfSilk(heroEffect: false);
		}
		wasAtBench = atBench;
	}
	
	[HarmonyPatch(typeof(HeroController), "ResetSilkRegen")]
	[HarmonyPostfix]
	[HarmonyPriority(Priority.Last)]
	private static void CancelHalfRegen(ref float ___silkRegenDelayLeft, float ___SILK_REGEN_DELAY) {
		regenerating = false;
		if (Applies(PlayerData.instance))
			___silkRegenDelayLeft = WithWhiteRing(___SILK_REGEN_DELAY);
	}

	private static bool hadHalf;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void RestartAfterUse(HeroController __instance, ref float ___silkRegenDelayLeft,
		ref float ___silkRegenDurationLeft, float ___SILK_REGEN_DELAY) {
		if (HeroController.instance != __instance)
			return;
		bool hasHalf = HalfSilk.GetHasHalfSilk();
		if (hadHalf && !hasHalf && !regenerating && Applies(PlayerData.instance)) {
			___silkRegenDelayLeft = WithWhiteRing(___SILK_REGEN_DELAY);
			___silkRegenDurationLeft = 0f;
			SilkSpool.Instance.SetRegen(0, false);
		}
		hadHalf = hasHalf;
	}
}
// Halves the cost of wisp Lantern to now use only 1 half silk.
[HarmonyPatch]
internal static class WispHalfCost {
	[HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), [typeof(int), typeof(SilkSpool.SilkTakeSource)])]
	[HarmonyPrefix]
	private static bool HalfSilkForWisp(int amount, SilkSpool.SilkTakeSource source) {
		if (source != SilkSpool.SilkTakeSource.Wisp || amount != 1 || !YenCrest.IsEquipped || !WispCostSetting.Value)
			return true;
		HalfSilk.RemoveHalfSilk();
		return false;
	}
}
