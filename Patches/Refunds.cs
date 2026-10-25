using HarmonyLib;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE004, Harmony003

namespace Weaver_Crest.Patches;

// Refund settings... Split in 2 (essential and parries) Both are WIP
[HarmonyPatch]
internal static class BreakableRefund {
	private static bool swingOpen, hitObstacle, hitEnemy, hitThorn, costOnHold;
	private static float swingStart;

	internal static void StartSwing(bool deferredCost) {
		EndSwing();
		swingOpen = true;
		swingStart = Time.time;
		hitObstacle = hitEnemy = hitThorn = false;
		costOnHold = deferredCost;
	}

	internal static void HitObstacle() {
		if (swingOpen && YenCrest.IsEquipped)
			hitObstacle = true;
	}

	internal static void HitEnemy() {
		if (swingOpen && Time.frameCount != podHitFrame)
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
		if (!hitObstacle || hitEnemy || hitThorn || !YenCrest.IsEquipped || !RefundSetting.Value)
			return;
		if (costOnHold)
			MaskTrade.CancelCost();
		else
			HalfSilk.AddHalfSilk(heroEffect: false);
	}

	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPostfix]
	private static void AfterSilklessHit(HitInstance hitInstance, bool ___doNotGiveSilk) {
		if (___doNotGiveSilk && Moveset.IsOurAttack(hitInstance.Source))
			HitObstacle();
	}
	
	private static int podHitFrame = -1;

		// bounce pods and wasp hives
	[HarmonyPatch(typeof(HealthManager), nameof(HealthManager.TakeDamage), [typeof(HitInstance)])]
	[HarmonyPrefix]
	private static void BeforePodHit(HealthManager __instance, HitInstance hitInstance) {
		bool notEnemy = __instance.GetComponent<BouncePod>()
			|| NameMatches(__instance.gameObject, ["Hive"]);
		if (notEnemy && Moveset.IsOurAttack(hitInstance.Source)) {
			podHitFrame = Time.frameCount;
			HitObstacle();
		}
	}

	private static bool NameMatches(GameObject obj, string[] words) {
		string name = obj.name + "/" + (obj.transform.parent ? obj.transform.parent.name : "");
		return System.Array.Exists(words, w => name.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0);
	}

	// coral stalactites
	[HarmonyPatch(typeof(Breakable), nameof(Breakable.Hit))]
	[HarmonyPrefix]
	private static void StalactiteHit(Breakable __instance, HitInstance damageInstance, bool ___isBroken) {
		if (!___isBroken && NameMatches(__instance.gameObject, ["Stalactite"]) && Moveset.IsOurAttack(damageInstance.Source))
			HitObstacle();
	}

	// Hazards which the needle clanks off... Requires name being listed below)
	private static readonly string[] TinkNames = ["Blade", "Cog", "Spike Ball", "Mill Spike", "Trap", "Pendulum"];

	[HarmonyPatch(typeof(TinkEffect), nameof(TinkEffect.TryDoTinkReactionNoDamager))]
	[HarmonyPostfix]
	private static void HazardTink(TinkEffect __instance, GameObject obj, bool __result) {
		if (__result && NameMatches(__instance.gameObject, TinkNames) && Moveset.IsOurAttack(obj))
			HitObstacle();
	}

	// Spikes and thorns, spikes refund silk thorns do not.
	[HarmonyPatch(typeof(DamageHero), "TryClashTinkCollider")]
	[HarmonyPrefix]
	private static void SpikeHit(DamageHero __instance, Collider2D collision) {
		if (__instance.hazardType != GlobalEnums.HazardType.SPIKES || !Moveset.IsOurAttack(collision.gameObject))
			return;
		SpikeSlashReaction reaction = __instance.GetComponent<SpikeSlashReaction>();
		if (reaction && reaction.enabled)
			hitThorn = true;
		else
			HitObstacle();
	}

	// Levers
	[HarmonyPatch(typeof(Lever), nameof(Lever.Hit))]
	[HarmonyPrefix]
	private static void LeverHit(HitInstance damageInstance, bool ___activated) {
		if (!___activated && Moveset.IsOurAttack(damageInstance.Source))
			HitObstacle();
	}

	[HarmonyPatch(typeof(Lever_tk2d), nameof(Lever_tk2d.Hit))]
	[HarmonyPrefix]
	private static void LeverTk2dHit(HitInstance damageInstance, bool ___activated, TrackTriggerObjects ___canHitTrigger) {
		if (!___activated && damageInstance.IsNailDamage && (!___canHitTrigger || ___canHitTrigger.IsInside)
			&& Moveset.IsOurAttack(damageInstance.Source))
			HitObstacle();
	}

	// More bounce pods
	[HarmonyPatch(typeof(BouncePod), nameof(BouncePod.Hit))]
	[HarmonyPrefix]
	private static void BouncePodHit(BouncePod __instance, HitInstance damageInstance) {
		if (Moveset.IsOurAttack(damageInstance.Source) && __instance.WillRespond(damageInstance))
			HitObstacle();
	}

	// Craw balloons
	[HarmonyPatch(typeof(BounceBalloon), nameof(BounceBalloon.Hit))]
	[HarmonyPrefix]
	private static void BalloonHit(HitInstance damageInstance) {
		if (Moveset.IsOurAttack(damageInstance.Source))
			HitObstacle();
	}

	// FSM objects like vines, trap wires, flea rescue branches, Shellwood twig walls, breakable wals, explosion rocks, 
	//Craft metal deposites, coral crusts, 
	private static readonly string[] ObstacleNames = ["Vine", "Trap", "Wire", "Branch", "Twig", "explod", "deposit", "Crust", "Lever"];

	[HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ReceivedDamageBase),
		nameof(HutongGames.PlayMaker.Actions.ReceivedDamageBase.RespondToHit))]
	[HarmonyPostfix]
	private static void AfterFsmHit(HutongGames.PlayMaker.Actions.ReceivedDamageBase __instance,
		HitInstance damageInstance, bool __result) {
		if (!__result || !Moveset.IsOurAttack(damageInstance.Source))
			return;
		GameObject owner = __instance.Fsm.GameObject;
		string name = owner ? owner.name + "/" + (owner.transform.parent ? owner.transform.parent.name : "") : "";
		bool obstacle = __instance.Fsm.GetState("Can Break?") != null
			|| System.Array.Exists(ObstacleNames, n => name.IndexOf(n, System.StringComparison.OrdinalIgnoreCase) >= 0);
		if (obstacle)
			HitObstacle();
	}

	private static bool wasBouncing;

	[HarmonyPatch(typeof(HeroController), "Update")]
	[HarmonyPostfix]
	private static void WatchForPogo(HeroController __instance) {
		if (HeroController.instance != __instance)
			return;
		var state = __instance.cState;
		bool bouncing = state.shroomBouncing || state.bouncing || state.downSpikeBouncing;
		if (bouncing && !wasBouncing)
			HitObstacle();
		wasBouncing = bouncing;
	}
}

//Shellshard wall
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
