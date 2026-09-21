using HarmonyLib;
using Needleforge.Attacks;
using System.Reflection;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

//Not working... (figure out why later)
internal static class LongClaw {
	[HarmonyPatch(typeof(AttackBase), nameof(AttackBase.StartAttack))]
	[HarmonyPrefix]
	private static void ApplyLongClawScale(AttackBase attack) 
	{
		object? nailAttack = HarmonyLib.AccessTools.Field(typeof(AttackBase), "NailAttack")?.GetValue(attack);
		if (nailAttack == null) {
			Log.LogWarning("LongClaw: NailAttack is null at creation time.");
			return;
		}

		var overrideField = HarmonyLib.AccessTools.Field(nailAttack.GetType(), "overrideLongNeedleScale");
		var scaleField = HarmonyLib.AccessTools.Field(nailAttack.GetType(), "longNeedleScale");
		if (overrideField == null || scaleField == null) {
			Log.LogWarning("LongClaw: overrideLongNeedleScale/longNeedleScale fields not found on NailAttack's type.");
			return;
		}

		overrideField.SetValue(nailAttack, true);
		scaleField.SetValue(nailAttack, new Vector3(1.3f, 1.3f, 1f));
		Log.LogInfo($"LongClaw: successfully set fields on {attack.Name}'s NailAttack.");
	}
}
