using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE003, HARMONIZE004

namespace Weaver_Crest.Patches;

[HarmonyPatch]
internal static class NeedolinHalfSilk {
	internal static bool UsingHalf;

	private static bool HasHalf() => YenCrest.IsEquipped && HalfSilk.GetHasHalfSilk();


	[HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), [typeof(int), typeof(SilkSpool.SilkTakeSource)])]
	[HarmonyPrefix]
	private static bool TakeHalfSilkFirst(int amount, SilkSpool.SilkTakeSource source) {
		if (source != SilkSpool.SilkTakeSource.ActiveUse || amount != 1 || !HasHalf())
			return true;
		UsingHalf = false;
		HalfSilk.RemoveHalfSilk();
		return false;
	}

	private static float? fullDrainTime;

	[HarmonyPatch(typeof(TimeLimitSetV2), nameof(TimeLimitSetV2.OnEnter))]
	[HarmonyPrefix]
	private static void HalveDrainTime(TimeLimitSetV2 __instance) {
		if (__instance.TimeDelay?.Name != "Silk Drain Time" || !HasHalf())
			return;
		UsingHalf = true;
		fullDrainTime = __instance.TimeDelay.Value;
		__instance.TimeDelay.Value *= 0.5f;

		SilkSpool spool = SilkSpool.Instance;
		if (spool.wasUsingChunk) {
			spool.wasUsingChunk.PlayIdle();
			spool.wasUsingChunk = null;
		}
		spool.RefreshSilk(default, default);
		HalfSilk.RefreshColor();
	}

	[HarmonyPatch(typeof(TimeLimitSetV2), nameof(TimeLimitSetV2.OnEnter))]
	[HarmonyPostfix]
	private static void RestoreDrainTime(TimeLimitSetV2 __instance) {
		if (fullDrainTime is float full) {
			__instance.TimeDelay.Value = full;
			fullDrainTime = null;
		}
	}
}