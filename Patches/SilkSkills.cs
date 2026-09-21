using HarmonyLib;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]

// silk spell damage modifer
// 1 = 100%, 1.5 = 150% and 0.5 = 50%
// For this crest, it will be 0.75-1 during playtests...
internal static class SilkSkills {
	private const float DamageMultiplier = 0.85f;

	[HarmonyPrefix]
	private static void SilkDamageReduction(ref HitInstance hitInstance) {
		if (!YenCrest.IsEquipped)
			return;

		if (hitInstance.RepresentingTool == null)
			return;

		hitInstance.Multiplier *= DamageMultiplier;
	}
}

// Potential silk skill casting speed modifer.
