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

	private static void WallSlash() {
		try {
			string[] wallSlashHornetFiles = ["Wall_1.png", "Wall_2.png", "Wall_3.png", "Wall_4.png"];
			Texture2D[] wallSlashHornetTex = LoadNamedTextures(wallSlashHornetFiles);

			tk2dSpriteCollectionData wallSlashData = Tk2dUtil.CreateTk2dSpriteCollection(
				sprites: wallSlashHornetTex,
				spriteCenters: wallSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f).ToArray()
			);
			Object.DontDestroyOnLoad(wallSlashData.gameObject);
			wallSlashData.gameObject.name = $"{YenId}_WallSlashAnim";
			wallSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
			{
				tk2dSprite? heroSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
				if (heroSprite != null) {
					Material heroMaterial = heroSprite.GetCurrentSpriteDef().material;
					foreach (var def in wallSlashData.spriteDefinitions)
						if (def != null)
							def.material.shader = heroMaterial.shader;
				}
			}

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
				AnimName = "Weaver Slash Effect",
				AnimLibrary = YenCrest.Moveset.Slash!.AnimLibrary,
				Hitbox = standardHitbox,
			};
		} catch {
			Log.LogWarning("Wall Slash sprites (Wall_1-4.png) not found or failed to load. Falling back to Hunter's default Wall Slash for now.");
		}
	}

	[HarmonyPatch(typeof(AttackBase), nameof(AttackBase.StartAttack))]
	[HarmonyPostfix]
	private static void WallSlashPositionLock(AttackBase __instance) {
		if (!YenCrest.IsEquipped)
			return;
		if (__instance != YenCrest.Moveset.WallSlash)
			return;

		Vector3 lockedPosition = HeroController.instance.transform.position;
		HeroController.instance.StartCoroutine(LockPosition());
		IEnumerator LockPosition() {
			float elapsed = 0f;
			while (elapsed < 0.5f) {
				HeroController.instance.transform.position = lockedPosition;
				elapsed += Time.deltaTime;
				yield return null;
			}
		}
	}
}