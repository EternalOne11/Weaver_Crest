using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Silksong.AssetHelper.ManagedAssets;
using Silksong.FsmUtil;
using System.Collections;
using TeamCherry.SharedUtils;
using System.Linq;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE004, Harmony003

namespace Weaver_Crest.Patches;

// The Weaver crest chapel/corpse
public static class Chapel {
	private const string ShrineScene = "Peak_10";
	private static readonly Vector2 ShrinePosition = new(31f, 33f);
	private const float ShrineZ = 0.5f;
	private const string CorpseSprite = "Weaver_Moss_Corpse_Glow.png";
	private const string CorpseMaterial = "Crossroads_Material_0.5";

	private const string RespawnMarkerName = "Crest Respawn Marker";
	private static readonly Vector2 RespawnPosition = new(31f, 32f);
	private const bool RespawnFacingRight = true;

	private const string VanillaShrineScene = "Greymoor_20c"; //(requires completely remaking probably for the true chapel)
	private const string VanillaShrinePath = "Crest Get Shrine";
	private const string ShrineFsm = "Control";

	public static event System.Func<bool>? ShrineBound;

	public static void GrantCrest() {
		ToolCrest? crest = ToolItemManager.GetCrestByName(YenId);
		if (!crest || crest!.IsUnlocked)
			return;
		crest.Unlock();
	}

	public static void ForceUnlock() {
		if (PlayerData.instance == null || !HeroController.SilentInstance) {
			return;
		}
		GrantCrest();
		if (GameObject.Find(ShrineName) is GameObject shrine)
			Object.Destroy(shrine);
	}

	private static ManagedAsset<GameObject>? vanillaShrine;

	internal static void Initialize() {
		vanillaShrine = ManagedAsset<GameObject>.FromSceneAsset(VanillaShrineScene, VanillaShrinePath);
		SceneManager.sceneLoaded += PlaceRespawnMarker;
		SceneManager.sceneLoaded += PlaceShrine;
	}

	// Respawn point sets when you bind the crest.
	private static void PlaceRespawnMarker(Scene scene, LoadSceneMode mode) {
		if (scene.name != ShrineScene
			|| scene.GetRootGameObjects().Any(o => o.name == RespawnMarkerName && o.GetComponent<RespawnMarker>()))
			return;
		GameObject marker = new(RespawnMarkerName);
		SceneManager.MoveGameObjectToScene(marker, scene);
		marker.transform.position = RespawnPosition;
		var respawn = marker.AddComponent<RespawnMarker>();
		respawn.respawnFacingRight = RespawnFacingRight;

		respawn.customFadeDuration = new OverrideFloat();
		respawn.overrideMapZone = new OverrideMapZone();
	}

	// The corpse is depdent upon the save, and weather or not the crest is unlocked.
	private static void PlaceShrine(Scene scene, LoadSceneMode mode) {
		if (scene.name != ShrineScene || PlayerData.instance == null || !GameManager.instance)
			return;
		ToolCrest? crest = ToolItemManager.GetCrestByName(YenId);
		if (crest && !crest!.IsUnlocked)
			GameManager.instance.StartCoroutine(SpawnShrine(scene));
	}

	private const string ShrineName = "Weaver Crest Shrine";
	private static Sprite? corpseSprite;
	private static Material? corpseMaterial;

	private static IEnumerator SpawnShrine(Scene scene) {
		GameObject root = new(ShrineName);
		SceneManager.MoveGameObjectToScene(root, scene);
		root.transform.position = new Vector3(ShrinePosition.x, ShrinePosition.y, ShrineZ);

		// The vanilla shrine
		if (vanillaShrine == null)
			yield break;
		if (!vanillaShrine.HasBeenLoaded)
			yield return vanillaShrine.Load();
		if (!vanillaShrine.Handle.IsDone || vanillaShrine.Handle.Status != AsyncOperationStatus.Succeeded || !vanillaShrine.Handle.Result) {
			yield break;
		}
		if (!root || !scene.isLoaded)
			yield break;

		// Copied while inactive, so it's set up before its FSM starts
		GameObject holder = new("Weaver Shrine Holder");
		holder.SetActive(false);
		GameObject shrine = Object.Instantiate(vanillaShrine.Handle.Result, holder.transform, false);
		shrine.name = "Weaver Crest Get Shrine";
		Vector3 source = vanillaShrine.Handle.Result.transform.position;
		shrine.transform.position = new Vector3(ShrinePosition.x, ShrinePosition.y, source.z);

		if (!SetUpShrine(shrine)) {
			Object.Destroy(holder);
			yield break;
		}
		shrine.transform.SetParent(null, true);
		SceneManager.MoveGameObjectToScene(shrine, scene);
		shrine.transform.SetParent(root.transform, true);
		Object.Destroy(holder);
		shrine.SetActive(true);
	}

	// The vanilla chapel FSM features. (mostly copied without full understanding)
	private static bool SetUpShrine(GameObject shrine) {
		PlayMakerFSM? fsm = shrine.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == ShrineFsm);
		ToolCrest? crest = ToolItemManager.GetCrestByName(YenId);
		if (!fsm || !crest) {
			return false;
		}
		if (!fsm!.Fsm.preprocessed)
			fsm.Preprocess();

		// Corpse placement... (mainly for force unlock)
		string[] needed = ["Crest Set", "Take Control", "State", "Crest Set 2", "End 2", "Crest Msg", "Crest Change"];
		var states = needed.ToDictionary(n => n, n => fsm.GetState(n));
		if (states.FirstOrDefault(s => s.Value == null) is { Key: not null } missing) {
			return false;
		}
		
		FsmState State(string name) => states[name]!;
		var vars = fsm.FsmVariables;
		if (vars.FindFsmGameObject("Hero") is FsmGameObject hero)
			hero.Value = HeroController.instance.gameObject;

		FsmState crestSet = State("Crest Set");
		foreach (var action in crestSet.Actions)
			action.Enabled = false;
		crestSet.InsertMethod(0, () => {
			if (vars.FindFsmObject("Crest Object") is FsmObject crestObject)
				crestObject.Value = crest;
			if (vars.FindFsmBool("Load Memory Scene") is FsmBool loadMemory)
				loadMemory.Value = false;
		});
		
		crestSet.AddTransition("FINISHED", "Check Unlocked");
		foreach (var action in State("Take Control").Actions.OfType<SetPlayerDataVariable>())
			action.Enabled = false;

		FsmState state = State("State");
		foreach (var action in state.Actions)
			action.Enabled = false;
		state.ChangeTransition("FINISHED", "Set Return");
		state.RemoveTransition("WARRIOR");

		FsmState crestSet2 = State("Crest Set 2");
		foreach (var action in crestSet2.Actions)
			action.Enabled = false;
		crestSet2.AddTransition("FINISHED", "Set Hero Facing");
		FsmState end = State("End 2");
		end.AddMethod(() => fsm.SetState("Inactive"));
		State("Crest Msg").InsertMethod(0, () => StopEffects(vars));

		// Uses Chapel of the reaper corpse but transforms its assets.
		Transform? figure = vars.FindFsmGameObject("Crest Figure")?.Value is GameObject f && f ? f.transform : null;
		if (figure)
			ReplaceFigure(figure!);
		else
			Log.LogWarning("Corpse wasn't found");

		if (vars.FindFsmGameObject("Crest Rune")?.Value is GameObject rune && rune)
			ReplaceRune(rune);
		else
			Log.LogWarning("Web glow rune wasn't found");

		FsmState crestChange = State("Crest Change");
		crestChange.InsertMethod(0, () => {
			bool handled = ShrineBound?.GetInvocationList().Cast<System.Func<bool>>().Select(h => h()).ToList().Any(h => h) ?? false;
			foreach (var action in crestChange.Actions.Where(a => a is UnlockCrest || a is AutoEquipCrestV2))
				action.Enabled = !handled;
		});
		return true;
	}

	// Corpse bind animations/effects
	private const float FigurePixelsPerUnit = 64f;
	private static readonly Vector2 FigureOffset = Vector2.zero;

	private static void ReplaceFigure(Transform figure) {
		if (!corpseSprite)
			corpseSprite = LoadEmbeddedPngAsSprite(CorpseSprite, new Vector2(0.5f, 0.5f), FigurePixelsPerUnit);
		if (!corpseMaterial)
			corpseMaterial = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == CorpseMaterial);
		// Centred where the vanilla figure is drawn, like the vanilla crests' figures (the rune is centred on it)
		Vector3 centre = figure.GetComponent<tk2dBaseSprite>() is tk2dBaseSprite drawn ? drawn.GetBounds().center : Vector3.zero;
		GameObject art = new("Weaver Shrine Figure");
		art.transform.SetParent(figure, false);
		art.transform.localPosition = new Vector3(centre.x + FigureOffset.x, centre.y + FigureOffset.y, 0f);
		var renderer = art.AddComponent<SpriteRenderer>();
		renderer.sprite = corpseSprite;
		if (corpseMaterial)
			renderer.sharedMaterial = corpseMaterial;
		art.AddComponent<FigureStandIn>().Figure = figure.GetComponent<Renderer>();
	}

	private class FigureStandIn : MonoBehaviour {
		public Renderer? Figure;
		private SpriteRenderer art = null!;

		private void Awake() => art = GetComponent<SpriteRenderer>();

		private void LateUpdate() {
			if (!Figure)
				return;
			Figure!.enabled = false;
			art.sortingLayerID = Figure.sortingLayerID;
			art.sortingOrder = Figure.sortingOrder;
		}
	}

	private static readonly string[] Effects = ["Active During Sequence", "Stab Effect", "Bind Slash", "FinalHit", "Hit Flash",
		"Bind Silk", "Bind Flash", "Big Bind Flash", "Blood Stream", "Crest Leak Loop Audio", "Stab Gout", "Pt SilkDust"];

	private static void StopEffects(FsmVariables vars) {
		foreach (string name in Effects)
			if (vars.FindFsmGameObject(name)?.Value is GameObject effect && effect)
				effect.SetActive(false);
	}

	// The Weaver crest's glow art
	private const string GlowArt = "CrestGlow.png";
	private const float GlowPixelsPerUnit = 48f;
	private const float WebScale = 1f;
	private static readonly Vector2 GlowOffset = Vector2.zero; // in pixels of the glow image: positive x right, y up
	private const string SharedGlow = "crest_glow_web";
	private const string CrestGlowPrefix = "crest_glow_";
	private static Sprite? glowSprite;

	private static void ReplaceRune(GameObject rune) {
		SpriteRenderer[] own = [.. rune.GetComponentsInChildren<SpriteRenderer>(true)
			.Where(r => r.sprite && r.sprite.name.StartsWith(CrestGlowPrefix, System.StringComparison.Ordinal)
				&& !r.sprite.name.StartsWith(SharedGlow, System.StringComparison.Ordinal))];
		if (own.Length == 0) {
			Log.LogWarning("Crest rune image wasn't found");
			return;
		}
		if (!glowSprite) {
			Sprite vanilla = own[0].sprite;
			Vector2 pivot = new(vanilla.pivot.x / vanilla.rect.width, vanilla.pivot.y / vanilla.rect.height);
			Texture2D art = LoadEmbeddedPngAsTexture2D(GlowArt);
			pivot -= new Vector2(GlowOffset.x / art.width, GlowOffset.y / art.height);
			glowSprite = Sprite.Create(art, new Rect(0f, 0f, art.width, art.height), pivot, GlowPixelsPerUnit);
		}

		foreach (var r in own) {
			r.sprite = glowSprite;
			r.transform.localScale *= WebScale;
		}
	}
}

// Force Crest unlock button
internal static class ChapelMenu {
	internal static void Register(BepInEx.Configuration.ConfigFile config) =>
		config.Bind("Crest", "Force unlock", false, new BepInEx.Configuration.ConfigDescription(
			"Unlocks the Weaver crest in the loaded save.", null,
			(Silksong.ModMenu.Plugin.ConfigEntryFactory.MenuElementGenerator)Button));

	private static bool Button(BepInEx.Configuration.ConfigEntryBase entry, out Silksong.ModMenu.Elements.MenuElement element) {
		var button = new Silksong.ModMenu.Elements.TextButton("Forcefully unlock Crest",
			"Unlocks the Crest of the Weaver immediatly, without the shrine.");
		button.OnSubmit = Chapel.ForceUnlock;
		element = button;
		return true;
	}
}

// The crest binding text
[HarmonyPatch]
internal static class CrestText {
	private const string VanillaCrest = " GlowReaper";

	[HarmonyPatch(typeof(ToolCrestUIMsg), "Setup")]
	[HarmonyPostfix]
	private static void FillWeaverText(ToolCrest crest, TMProOld.TMP_Text ___nameText, TMProOld.TMP_Text ___descText,
		TMProOld.TMP_Text ___itemPrefixText, TMProOld.TMP_Text ___equipText) {
		if (!crest || crest.name != YenId)
			return;
		ToolCrest? vanilla = ToolItemManager.GetCrestByName(VanillaCrest);

		if (___nameText)
			___nameText.text = YenName;
		if (___descText)
			___descText.text = YenDesc;
		if (___itemPrefixText && vanilla)
			___itemPrefixText.text = vanilla!.ItemNamePrefix;
		if (___equipText && vanilla)
			___equipText.text = vanilla!.EquipText;
	}
}