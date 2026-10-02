using HarmonyLib;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Collections;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static readonly Vector2[] WallHitbox = [new(-0.3f, 0.8f), new(-1.5f, 1.3f), new(-2.8f, 1.0f),
	new(-3.6f, 0.3f), new(-3.6f, -0.3f), new(-2.8f, -1.0f), new(-1.5f, -1.3f), new(-0.3f, -0.8f),];

	private static void WallSlash() {
		string[] wallSlashHornetFiles = ["Wall_1.png", "Wall_2.png", "Wall_3.png", "Wall_4.png", "Wall_5.png"];
		Texture2D[] wallSlashHornetTex = LoadNamedTextures(wallSlashHornetFiles);

		tk2dSpriteCollectionData wallSlashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: wallSlashHornetTex,
			spriteCenters: [.. wallSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f)]
		);
		Object.DontDestroyOnLoad(wallSlashData.gameObject);
		wallSlashData.gameObject.name = $"{YenId}_WallSlashAnim";
		wallSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(wallSlashData);

		tk2dSpriteAnimation wallSlashAnims = wallSlashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		wallSlashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Wall Slash",
				fps = 10,
				wrapMode = WrapMode.Once,
				frames = wallSlashData.CreateFrames(wallSlashHornetTex.Select(t => t.name)),
			},
		];
		wallSlashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, wallSlashAnims.clips[0]];

		YenCrest.Moveset.WallSlash = new Attack {
			Name = "WeaverWallSlash",
			AnimName = "Weaver Slash Effect",                // reuses the regular slash's effect
			AnimLibrary = YenCrest.Moveset.Slash!.AnimLibrary,
			Hitbox = WallHitbox,
		};
	}
}

[HarmonyPatch]
internal static class WallPatch {

	[HarmonyPatch(typeof(AttackBase), nameof(AttackBase.StartAttack))]
	[HarmonyPostfix]
	private static void WallSlashPositionLock(AttackBase __instance) {
		if (!YenCrest.IsEquipped || __instance != YenCrest.Moveset.WallSlash)
			return;

		Vector3 lockedPosition = HeroController.instance.transform.position;
		HeroController.instance.StartCoroutine(LockPosition());
		IEnumerator LockPosition() {
			for (float elapsed = 0f; elapsed < 0.5f; elapsed += Time.deltaTime) {
				HeroController.instance.transform.position = lockedPosition;
				yield return null;
			}
		}
	}
}
