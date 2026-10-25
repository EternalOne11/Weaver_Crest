using BepInEx;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest;

// 4 memory lockets are placed in the world
// May change method later
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
		if (!GameManager.instance)
			return;
		foreach (var (id, sceneName, position) in Locations) {
				continue;
			Spawn(id, position);
		}
	}

	private static void Spawn(string id, Vector2 position) {
		locket ??= Resources.FindObjectsOfTypeAll<CollectableItem>().FirstOrDefault(i => i.name == LocketName);
		CollectableItemPickup prefab = GlobalSettings.Gameplay.CollectableItemPickupPrefab;
		if (!locket || !prefab) {
			Log.LogWarning($"LocketSpawn: couldn't spawn {id} (locket item or pickup prefab not found)");
			return;
		}
		CollectableItemPickup pickup = Object.Instantiate(prefab);
		pickup.transform.SetPosition2D(position);
		pickup.SetItem(locket!);

		var marker = pickup.gameObject.AddComponent<LocketMarker>();
		marker.Id = id;
		marker.CountAtSpawn = locket!.CollectedAmount;
	}

	private class LocketMarker : MonoBehaviour {
		public string Id = "";
		public int CountAtSpawn;

		private void OnDisable() {
			if (locket && locket!.CollectedAmount > CountAtSpawn)
				SetCollected(Id);
		}
	}

	private static string SavePath => Path.Combine(Paths.ConfigPath, "Weaver_Crest_lockets.txt");
	private static string Key(string id) => $"{GameManager.instance.profileID}:{id}";

	private static HashSet<string> Load() =>
		File.Exists(SavePath) ? [.. File.ReadAllLines(SavePath)] : [];

	private static bool IsCollected(string id) => Load().Contains(Key(id));

	private static void SetCollected(string id) {
		HashSet<string> collected = Load();
		if (collected.Add(Key(id)))
			File.WriteAllLines(SavePath, collected);
	}
}