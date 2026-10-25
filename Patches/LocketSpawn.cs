using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest;

// 4 memory lockets are placed in the world
internal static class LocketSpawn {
	private const string LocketName = "Crest Socket Unlocker";

        // Locations scene name and coordinate position
	private static readonly (string Id, string Scene, Vector2 Position)[] Locations = [
		("WeaverLocket1", "Aqueduct_03", new Vector2(67f, 9.3f)),
		("WeaverLocket2", "Peak_08", new Vector2(308f, 170f)),
		("WeaverLocket3", "Song_10", new Vector2(23.5f, 51f)),
		("WeaverLocket4", "Under_23", new Vector2(36.5f, 2f)),
	];

	private static CollectableItem? locket;

	internal static void Initialize() => SceneManager.sceneLoaded += OnSceneLoaded;

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
			Log.LogWarning($"LocketSpawn: couldn't spawn {id} (locket item or pickup prefab not found)");
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

	private class LocketMarker : MonoBehaviour {
		public string Id = "";
		public string SceneName = "";
		public int CountAtSpawn;

		private void OnDisable() {
			HeroController hero = HeroController.instance;
			if (!hero || !locket)
				return;
			bool nextToHornet = Vector2.Distance(hero.transform.position, transform.position) < 3f;
			if (nextToHornet && locket!.CollectedAmount > CountAtSpawn)
				SetCollected(Id, SceneName);
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
