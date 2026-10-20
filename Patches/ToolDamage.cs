using HarmonyLib;
using System.Collections.Generic;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]

// silk spell damage modifer (other tools can be added)
// 1 = 100%, 1.5 = 150% and 0.5 = 50%
// 100% by default 85% from config.
internal static class ToolDamage {

	private static HashSet<ToolItem>? silkSkills;
	private static HashSet<ToolItem> SilkSkills => silkSkills ??= [
		ToolItemManager.GetToolByName("Silk Spear"),
		ToolItemManager.GetToolByName("Thread Sphere"),
		ToolItemManager.GetToolByName("Parry"),
		ToolItemManager.GetToolByName("Silk Charge"),
		ToolItemManager.GetToolByName("Silk Bomb"),
		ToolItemManager.GetToolByName("Silk Boss Needle"),
	];

	private static Dictionary<ToolItem, float>? targetedTools;
	private static Dictionary<ToolItem, float> TargetedTools => targetedTools ??= new() {
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

		float DamageMultiplier;
		if (SilkSkills.Contains(hitInstance.RepresentingTool))
			DamageMultiplier = SilkDamageSetting.Value ? 0.85f : 1f;
		else if (!TargetedTools.TryGetValue(hitInstance.RepresentingTool, out DamageMultiplier))
			return;

		hitInstance.Multiplier *= DamageMultiplier;
	}
}
