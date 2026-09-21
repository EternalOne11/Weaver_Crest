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

		if (PlayerData.instance.silk > 0)
			HeroController.instance.AddSilk(-1, true, SilkSpool.SilkAddSource.Normal, false);
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

		if (PlayerData.instance.silk > 0)
			HeroController.instance.AddSilk(-1, true, SilkSpool.SilkAddSource.Normal, false);
	}
}
