using HarmonyLib;
using System.Collections.Generic;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]

// silk spell damage modifer (other tools can be added)
// 1 = 100%, 1.5 = 150% and 0.5 = 50%
// For this crest, it will be 0.75-1 during playtests...
internal static class ToolDamage {

	private static Dictionary<ToolItem, float>? targetedTools;
	private static Dictionary<ToolItem, float> TargetedTools => targetedTools ??= new() {
		[ToolItemManager.GetToolByName("Silk Spear")] = 1.0f,
		[ToolItemManager.GetToolByName("Thread Sphere")] = 1.0f,
		[ToolItemManager.GetToolByName("Parry")] = 1.0f,
		[ToolItemManager.GetToolByName("Silk Charge")] = 1.0f,
		[ToolItemManager.GetToolByName("Silk Bomb")] = 1.0f,
		[ToolItemManager.GetToolByName("Silk Boss Needle")] = 1.0f,
		
		//Clawmirror nerfs
		[ToolItemManager.GetToolByName("Dazzle Bind")] = 0.5f,
		[ToolItemManager.GetToolByName("Dazzle Bind Upgraded")] = 0.5f,
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
// Potential silk skill casting speed modifer.
