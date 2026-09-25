using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static void UpSlash() {
		string[] upSlashFiles = ["upSlash_e0000.png", "upSlash_e0001.png", "upSlash_e0002.png"];
		Texture2D[] upSlashTex = LoadNamedTextures(upSlashFiles);
		string[] upSlashHornetFiles = ["upSlash0000.png", "upSlash0001.png", "upSlash0002.png", "upSlash0003.png",
			"upSlash0004.png", "upSlash0005.png", "upSlash0006.png", "upSlash0007.png",
			"upSlash0008.png", "upSlash0009.png",];
		Texture2D[] upSlashHornetTex = LoadNamedTextures(upSlashHornetFiles);

		tk2dSpriteCollectionData upSlashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. upSlashTex, .. upSlashHornetTex],
			spriteCenters: [
				.. upSlashTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(25f, 85f)),
				.. upSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(upSlashData.gameObject);
		upSlashData.gameObject.name = $"{YenId}_UpSlashAnim";
		upSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		{
			tk2dSprite? heroSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
			if (heroSprite != null) {
				Material heroMaterial = heroSprite.GetCurrentSpriteDef().material;
				foreach (var def in upSlashData.spriteDefinitions)
					if (def != null)
						def.material.shader = heroMaterial.shader;
			}
		}

		tk2dSpriteAnimation upSlashAnims = upSlashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		upSlashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver UpSlash Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					upSlashData.CreateFrame(upSlashTex[0].name, triggerEvent: true),
					.. upSlashData.CreateFrames(upSlashTex.Skip(1).Select(t => t.name)),
					upSlashData.CreateFrame(upSlashTex[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "UpSlash",
				fps = 20,
				wrapMode = WrapMode.Once,
				frames = upSlashData.CreateFrames(upSlashHornetTex.Select(t => t.name)),
			},
		];
		upSlashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, upSlashAnims.clips[1]];

		YenCrest.Moveset.UpSlash = new Attack {
			Name = "WeaverUpSlash",
			AnimName = "Weaver UpSlash Effect",
			AnimLibrary = upSlashAnims,
			Hitbox = [new Vector2(-1.5f, 0f),new Vector2(-1f, 2.6f),new Vector2(0f, 3f),new Vector2(1f, 2.6f),
			new Vector2(1.5f, 0f),new Vector2(0.5f, 0f),new Vector2(-0.5f, 0f),],
		};
	}
}
