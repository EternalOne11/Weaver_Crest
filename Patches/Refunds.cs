using HarmonyLib;
using TeamCherry.SharedUtils;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE004, Harmony003

namespace Weaver_Crest.Patches;

// Refunds for three kinds, each with its own setting
// Essentails (for vines, breakable walls, spikes, and stuff for pogos and shellshard walls)
// breakables
// Parries
[HarmonyPatch]
internal static class BreakableRefund {
	private static bool swingOpen, hitBreakable, hitObstacle, hitEnemy, costOnHold;
	private static float swingStart;

	internal static void StartSwing(bool deferredCost) {
		EndSwing();
		swingOpen = true;
		swingStart = Time.time;
		hitBreakable = hitObstacle = hitEnemy = false;
		costOnHold = deferredCost;
	}

	internal static void HitBreakable() {
		if (swingOpen && YenCrest.IsEquipped)
			hitBreakable = true;
	}

	internal static void HitObstacle() {
		if (swingOpen && YenCrest.IsEquipped)
			hitObstacle = true;
	}

	internal static void HitEnemy() {
		if (swingOpen)
			hitEnemy = true;
	}

	internal static void EndSwingIfOver() {
		if (swingOpen && (!MaskTrade.Swinging() || Time.time - swingStart >= MaskTrade.WeaveWindow))
			EndSwing();
	}

	private static void EndSwing() {
		if (!swingOpen)
			return;
		swingOpen = false;
		bool refund = (hitObstacle && RefundSetting.Value) || (hitBreakable && BreakableRefundSetting.Value);
		if (!refund || hitEnemy || !YenCrest.IsEquipped)
			return;
		if (costOnHold)
			MaskTrade.CancelCost();
		else
			HalfSilk.AddHalfSilk(heroEffect: false); // refund the half silk
	}

	[HarmonyPatch(typeof(Breakable), nameof(Breakable.Hit))]
	[HarmonyPrefix]
	private static void BeforeBreakableHit(int ___hitsLeft, bool ___isBroken, int ___silkHits,
		out (int hitsLeft, bool broken, int silkHits) __state) =>
		__state = (___hitsLeft, ___isBroken, ___silkHits);

	[HarmonyPatch(typeof(Breakable), nameof(Breakable.Hit))]
	[HarmonyPostfix]
	private static void AfterBreakableHit(Breakable __instance, HitInstance damageInstance, int ___hitsLeft,
		bool ___isBroken, int ___silkHits, (int hitsLeft, bool broken, int silkHits) __state) {
		if (!swingOpen || !YenCrest.IsEquipped || !Moveset.IsOurAttack(damageInstance.Source))
			return;

		if (___silkHits < __state.silkHits)
			return;

		bool counted = ___hitsLeft != __state.hitsLeft || (___isBroken && !__state.broken);

		if (counted && IsSolid(__instance))
			hitBreakable = true;
	}

	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void AfterSilklessHit(HitInstance hitInstance, bool ___doNotGiveSilk) {
		if (___doNotGiveSilk && Moveset.IsOurAttack(hitInstance.Source))
			HitObstacle();
	}

	private static readonly string[] ObstacleNames = ["Vine", "Trap", "Wire", "Branch"];

	[HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamageBase),
		nameof(HutongGames.PlayMaker.Actions.ReceivedDamageBase.RespondToHit))]
	[HarmonyPostfix]
	private static void AfterFsmHit(HutongGames.PlayMaker.Actions.ReceivedDamageBase __instance,
		HitInstance damageInstance, bool __result) {
		if (!__result || !Moveset.IsOurAttack(damageInstance.Source))
			return;
		string name = __instance.Fsm.GameObject ? __instance.Fsm.GameObject.name : "";
		bool obstacle = __instance.Fsm.GetState("Can Break?") != null
			|| System.Array.Exists(ObstacleNames, n => name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0);
		if (obstacle)
			HitObstacle();
		else
			HitBreakable();
	}

	[HarmonyPatch(typeof(DamageEnemies), nameof(DamageEnemies.OnHitSpikes))]
	[HarmonyPostfix]
	private static void AfterSpikesHit(DamageEnemies __instance) {
		if (Moveset.IsOurAttack(__instance.gameObject))
			HitObstacle();
	}

	private static bool IsSolid(Breakable breakable) {
		NonBouncer nonBouncer = breakable.GetComponent<NonBouncer>();
		return !nonBouncer || !nonBouncer.active;
	}
}

[HarmonyPatch]
internal static class ShardWallRefund {
	[HarmonyPatch(typeof(ShardRegion), "OnNailSlashTerrainThunked")]
	[HarmonyPrefix]
	private static void OnShardWallStruck(Vector2 thunkPos, bool ___hasBox, BoxCollider2D ___box) {
		if (___hasBox && ___box.OverlapPoint(thunkPos))
			BreakableRefund.HitObstacle();
	}
}

// Parry Refund system...
[HarmonyPatch]
internal static class ParryRefund {
	private static bool refundedSwing = true;

	[HarmonyPatch(typeof(HeroController), "Attack")]
	[HarmonyPostfix]
	private static void NewSwing() => refundedSwing = false;

	[HarmonyPatch(typeof(DamageEnemies), nameof(DamageEnemies.SendParried), [typeof(bool)])]
	[HarmonyPostfix]
	private static void AfterParried(DamageEnemies __instance) {
		if (refundedSwing || !YenCrest.IsEquipped || !ParrySetting.Value
			|| !Moveset.IsOurAttack(__instance.gameObject))
			return;
		refundedSwing = true;

		if (!MaskTrade.CancelCost())
			HalfSilk.AddHalfSilk(heroEffect: false);

		BreakableRefund.HitEnemy();
	}
}