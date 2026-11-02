using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;
[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]

// silk spell damage modifer (other tools can be added)
// 1 = 100%, 1.5 = 150% and 0.5 = 50%
internal static class ToolDamage {

	private static Dictionary<ToolItem, float>? targetedTools;
	private static Dictionary<ToolItem, float> TargetedTools => targetedTools ??= new() {
		// The six silk skills
		[ToolItemManager.GetToolByName("Silk Spear")] = 0.9f,
		[ToolItemManager.GetToolByName("Thread Sphere")] = 0.9f,
		[ToolItemManager.GetToolByName("Parry")] = 0.9f,
		[ToolItemManager.GetToolByName("Silk Charge")] = 0.9f,
		[ToolItemManager.GetToolByName("Silk Bomb")] = 0.9f,
		[ToolItemManager.GetToolByName("Silk Boss Needle")] = 0.9f,

		//Clawmirror nerfs damage wise... 0.4 means -60% damage...
		[ToolItemManager.GetToolByName("Dazzle Bind")] = 0.4f,
		[ToolItemManager.GetToolByName("Dazzle Bind Upgraded")] = 0.4f,
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

// Clawmirror size nerf both damage and size...
[HarmonyPatch]
internal static class ClawmirrorSize {
	internal const float SizeMultiplier = 0.8f;
	private const string FlashName = "hero_dazzle_flash";
	private static readonly Dictionary<Transform, Vector3> partScales = [];
	private static readonly Dictionary<ParticleSystem, float> particleSizes = [];

	[HarmonyPatch(typeof(DamageEnemies), "OnEnable")]
	[HarmonyPostfix]
	private static void Resize(DamageEnemies __instance) {
		ToolItem? tool = __instance.RepresentingTool;
		if (!tool || !tool!.name.StartsWith("Dazzle Bind"))
			return;

		float size = YenCrest.IsEquipped ? SizeMultiplier : 1f;
		foreach (Transform flash in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
			if (!flash.name.StartsWith(FlashName))
				continue;

			foreach (Transform part in flash) {
				if (!partScales.TryGetValue(part, out Vector3 scale))
					partScales[part] = scale = part.localScale;
				part.localScale = scale * size;
			}

			foreach (ParticleSystem ps in flash.GetComponentsInChildren<ParticleSystem>(true)) {
				if (!particleSizes.TryGetValue(ps, out float original))
					particleSizes[ps] = original = ps.main.startSizeMultiplier;
				ParticleSystem.MainModule main = ps.main;
				main.startSizeMultiplier = original * size;
			}
		}
	}
}
