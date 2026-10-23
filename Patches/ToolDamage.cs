using HarmonyLib;
using System.Collections.Generic;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]

// silk spell damage modifer (other tools can be added)
// 1 = 100%, 1.5 = 150% and 0.5 = 50%
internal static class ToolDamage {

	private static Dictionary<ToolItem, float>? targetedTools;
	private static Dictionary<ToolItem, float> TargetedTools => targetedTools ??= new() {
		// The six silk skills
		[ToolItemManager.GetToolByName("Silk Spear")] = 0.85f,
		[ToolItemManager.GetToolByName("Thread Sphere")] = 0.85f,
		[ToolItemManager.GetToolByName("Parry")] = 0.85f,
		[ToolItemManager.GetToolByName("Silk Charge")] = 0.85f,
		[ToolItemManager.GetToolByName("Silk Bomb")] = 0.85f,
		[ToolItemManager.GetToolByName("Silk Boss Needle")] = 0.85f,

		//Clawmirror nerfs
		[ToolItemManager.GetToolByName("Dazzle Bind")] = 0.45f,
		[ToolItemManager.GetToolByName("Dazzle Bind Upgraded")] = 0.45f,
	};

	[HarmonyPrefix]
	private static void SilkDamageReduction(ref HitInstance hitInstance) {
		if (!YenCrest.IsEquipped)
			return;

		if (hitInstance.RepresentingTool == null)
			return;

		if (!TargetedTools.TryGetValue(hitInstance.RepresentingTool, out float DamageMultiplier))
			return;

		hitInstance.Multiplier *= DamageMultiplier;
	}
}
