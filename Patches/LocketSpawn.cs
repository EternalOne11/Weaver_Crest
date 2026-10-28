using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest;

// 4 memory lockets are placed in the world, one for each of the Weaver crest's locked slots.
// They are stored within the save itself... Meaning they will be tracked like normal items.
internal static class LocketSpawn {
	private const string LocketName = "Crest Socket Unlocker";

	// Locations of each locket... Room and position... ID must remain different.
	private static readonly (string Id, string Scene, Vector2 Position)[] Locations = [
		("WeaverLocket1", "Aqueduct_03", new Vector2(57.7f, 9.3f)),
		("WeaverLocket2", "Peak_02", new Vector2(33.5f, 165f)),
		("WeaverLocket3", "Song_10", new Vector2(23.6f, 51f)),
		("WeaverLocket4", "Under_23", new Vector2(36.7f, 2f)),
	];

	private static CollectableItem? locket;

	internal static void Initialize() {
		SceneManager.sceneLoaded += OnSceneLoaded;
		SceneManager.sceneLoaded += PlatformSpawn.OnSceneLoaded;
		SceneManager.sceneLoaded += DecorSpawn.OnSceneLoaded;
	}

	private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		if (!GameManager.instance || SceneData.instance == null)
			return;
		foreach (var (id, sceneName, position) in Locations) {
			if (sceneName != scene.name)
				continue;
			if (!IsCollected(id, sceneName))
				Spawn(id, scene, position);
		}
	}

	private static void Spawn(string id, Scene scene, Vector2 position) {
		string sceneName = scene.name;
		locket ??= Resources.FindObjectsOfTypeAll<CollectableItem>().FirstOrDefault(i => i.name == LocketName);
		CollectableItemPickup prefab = GlobalSettings.Gameplay.CollectableItemPickupPrefab;
		if (!locket || !prefab) {
			return;
		}
		CollectableItemPickup pickup = Object.Instantiate(prefab);
		SceneManager.MoveGameObjectToScene(pickup.gameObject, scene);
		pickup.transform.SetPosition2D(position);
		pickup.SetItem(locket!);

		var marker = pickup.gameObject.AddComponent<LocketMarker>();
		marker.Id = id;
		marker.SceneName = sceneName;
		marker.CountAtSpawn = locket!.CollectedAmount;
	}

	// Collected check
	private class LocketMarker : MonoBehaviour {
		public string Id = "";
		public string SceneName = "";
		public int CountAtSpawn;
		private bool recorded;

		private void Update() {
			if (recorded || !locket || locket!.CollectedAmount <= CountAtSpawn)
				return;
			HeroController hero = HeroController.instance;
			if (hero && Vector2.Distance(hero.transform.position, transform.position) < 5f) {
				SetCollected(Id, SceneName);
				recorded = true;
			}
			else
				CountAtSpawn = locket.CollectedAmount;
		}
	}

	private class LocketData : PersistentItemData<bool> { }

	private static bool IsCollected(string id, string sceneName) =>
		SceneData.instance.PersistentBools.TryGetValue(sceneName, id, out var data) && data.Value;

	private static void SetCollected(string id, string sceneName) {
		if (SceneData.instance == null)
			return;
		SceneData.instance.PersistentBools.SetValue(new LocketData { ID = id, SceneName = sceneName, Value = true });
	}
}

// Adds platform
internal static class PlatformSpawn {
	private const string TargetScene = "Aqueduct_03";
	private const string SpriteName = "swamp_plats_basic_0003_1_plan_strapped_short";

	private static readonly Vector2 Position = new(58.7f, 8.3f);
	private const bool Flip = true;

	// Collider movement variable
	private const float ColliderDrop = 0.17f;

	// Pieces of the platform: offset, z, rotation, scale, and whether it's floor (covered by the collider)
	private static readonly (Vector2 Offset, float Z, float Rotation, Vector2 Scale, bool Floor)[] Pieces = [
		(new Vector2(-0.10f, 0.29f), -0.3153f, 359.6378f, new Vector2(-1f, 1f), true),
		(new Vector2(-0.42f, 1.65f), 1.0314f, 313.0127f, new Vector2(-0.99f, 0.99f), false),
		(new Vector2(-0.36f, 0.03f), -0.1834f, 359.2561f, new Vector2(-1.0104f, 0.98f), true),
		(new Vector2(-1.78f, -0.84f), -0.0234f, 17.3131f, new Vector2(-1.0104f, 0.98f), false),
	];

	private static Sprite? sprite;

	private static readonly Rect StrutRect = new(612f, 2953f, 489f, 48.9802f);
	private static Sprite? strut;

	internal static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		if (scene.name != TargetScene)
			return;
		sprite ??= Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == SpriteName);
		if (!sprite) {
			return;
		}
		strut ??= Sprite.Create(sprite!.texture, StrutRect, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);

		GameObject platform = new("Locket Platform");
		SceneManager.MoveGameObjectToScene(platform, scene);
		platform.transform.position = Position;
		float mirror = Flip ? -1f : 1f;

		Material? material = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
			.Select(r => r.sharedMaterial).FirstOrDefault(m => m);

		Bounds? bounds = null;
		foreach (var (offset, z, rotation, scale, floor) in Pieces) {
			GameObject piece = new("Platform Piece");
			piece.transform.SetParent(platform.transform, false);
			piece.transform.localPosition = new Vector3(offset.x * mirror, offset.y, z);
			piece.transform.localRotation = Quaternion.Euler(0f, 0f, Flip ? -rotation : rotation);
			piece.transform.localScale = new Vector3(scale.x * mirror, scale.y, 1f);
			var renderer = piece.AddComponent<SpriteRenderer>();
			renderer.sprite = floor ? sprite : strut;
			if (material)
				renderer.sharedMaterial = material;
			if (!floor)
				continue;
			if (bounds is Bounds b) {
				b.Encapsulate(renderer.bounds);
				bounds = b;
			}
			else
				bounds = renderer.bounds;
		}

		// Platform Collider
		if (bounds is Bounds all) {
			GameObject ground = new("Collider");
			ground.transform.SetParent(platform.transform, false);
			ground.layer = LayerMask.NameToLayer("Terrain");
			var collider = ground.AddComponent<BoxCollider2D>();
			collider.offset = (Vector2)all.center - Position + new Vector2(0f, -ColliderDrop / 2f);
			collider.size = new Vector2(all.size.x, Mathf.Max(0.1f, all.size.y - ColliderDrop));
		}
	}
}

// Peak Memory locket Corpse...
internal static class DecorSpawn {
	private static readonly (string Scene, string SpriteName, Vector2 Position, float Z, float Scale, bool Flip)[] Decor = [
		("Peak_02", "Peak_Broken_Servitors_0001_1_snow", new Vector2(33.5f, 165f), 0.5f, 1f, false),
	];

	private static readonly System.Collections.Generic.Dictionary<string, Sprite> sprites = new();

	internal static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
		foreach (var (sceneName, spriteName, position, z, scale, flip) in Decor) {
			if (sceneName != scene.name)
				continue;
			if (!sprites.TryGetValue(spriteName, out Sprite? sprite) || !sprite) {
				sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == spriteName);
				if (!sprite) {
					Log.LogWarning($"DecorSpawn: sprite '{spriteName}' not found");
					continue;
				}
				sprites[spriteName] = sprite!;
			}

			GameObject decor = new(spriteName);
			SceneManager.MoveGameObjectToScene(decor, scene);
			decor.transform.position = new Vector3(position.x, position.y, z);
			decor.transform.localScale = new Vector3(flip ? -scale : scale, scale, 1f);
			var renderer = decor.AddComponent<SpriteRenderer>();
			renderer.sprite = sprite;

			Material? material = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
				.Select(r => r.sharedMaterial).FirstOrDefault(m => m);
			if (material)
				renderer.sharedMaterial = material;
		}
	}
}
