using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

[HarmonyPatch(typeof(CountCrestUnlockPoints), nameof(CountCrestUnlockPoints.OnEnter))]
internal static class EvaOptOut {

	[HarmonyPrefix]
	private static void SubtractWeaver_Slots(CountCrestUnlockPoints __instance) {
		ToolCrestList list = ScriptableObject.CreateInstance<ToolCrestList>();

		foreach (var crest in (ToolCrestList)__instance.CrestList.Value) {
			if (crest.name != YenCrest.name)
				list.Add(crest);
		}

		__instance.CrestList.Value = list;
	}

}
