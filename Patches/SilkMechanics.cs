using GlobalEnums;
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
		MaskTrade.MaskCost(normalAttack: true);

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

		if (MaskTrade.CancelCost()) {
			HalfSilk.AddHalfSilk(heroEffect: false);
			return;
		}

		HeroController.instance.AddSilk(1, false, SilkSpool.SilkAddSource.Normal, false);
	}

        private static bool wasBinding;

        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPostfix]
        private static void HalfSilkAfterBind(HeroController __instance) {
                if (HeroController.instance != __instance)
                        return;
                bool binding = __instance.cState.isBinding;
                if (wasBinding && !binding && YenCrest.IsEquipped && BindHalfSilkSetting.Value
                        && PlayerData.instance.silk <= 0 && !HalfSilk.GetHasHalfSilk())
                        HalfSilk.AddHalfSilk(heroEffect: false);
                wasBinding = binding;
        }
        [HarmonyPatch(typeof(HeroController), "Update")]
        [HarmonyPostfix]

	private static void PayMissed(HeroController __instance) {
		if (HeroController.instance == __instance)
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

	internal static bool DuringTrade() =>
		PlayerData.instance.silk <= 0
		&& !HalfSilk.GetHasHalfSilk()
        && PlayerData.instance.health > Cost; // never on her last mask

	internal static void MaskCost(bool normalAttack = false) {
		if (DuringTrade()) {
			Sounds.MuteForTrade(); //Pain grunt replaces normal attack grunt

		// Lifeblood Exclusion

			int lifeblood = LifebloodTradeSetting.Value ? 0 : PlayerData.instance.healthBlue;
			PlayerData.instance.healthBlue -= lifeblood;
			PlayerData.instance.TakeHealth(Cost, HeroController.instance.IsInLifebloodState, allowFracturedMaskBreak: false);
			PlayerData.instance.healthBlue += lifeblood;
			HeroController.instance.damageEffectFSM.SendEvent("DAMAGE"); // the black damage vignette
			EventRegister.SendEvent("HEALTH UPDATE");

			if (PlayerData.instance.health == 1)
				EventRegister.SendEvent("HERO DAMAGED");
			HeroController.instance.AddSilk(Silk, false, SilkSpool.SilkAddSource.Normal, false);
			HeroController.instance.SpriteFlash.flashFocusHeal();
			Sounds.PlayHurt();
			PlayFlash();
		}
	//visual delay at 9 silk to prevent a greater visual annoyance
		bool DipBelowBind = PlayerData.instance.silk == SilkSpool.BindCost && !HalfSilk.GetHasHalfSilk();
		if (DipBelowBind)
			pendingCosts.Enqueue((Time.time, normalAttack));
		else
			HalfSilk.RemoveHalfSilk(interruptRegen: false);
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
		var state = HeroController.instance.cState;
		bool swinging = state.attacking || state.downSpikeAntic || state.downSpiking || state.downAttacking;
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
