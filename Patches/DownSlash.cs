using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static void DownSlash() {
		string[] downSlashFiles = ["downSlash_e0000.png", "downSlash_e0001.png", "downSlash_e0002.png", "downSlash_e0003.png"];
		Texture2D[] downSlashTex = LoadNamedTextures(downSlashFiles);
		string[] downSlashHornetFiles = [
			"downSlash0000.png", "downSlash0001.png", "downSlash0002.png", "downSlash0003.png", "downSlash0004.png",
			"downSlash0005.png", "downSlash0006.png", "downSlash0007.png", "downSlash0008.png",
		];
		Texture2D[] downSlashHornetTex = LoadNamedTextures(downSlashHornetFiles);

		tk2dSpriteCollectionData downSlashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. downSlashTex, .. downSlashHornetTex],
			spriteCenters: [
				.. downSlashTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
				.. downSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(downSlashData.gameObject);
		downSlashData.gameObject.name = $"{YenId}_DownSlashAnim";
		downSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		{
			tk2dSprite? heroSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
			if (heroSprite != null) {
				Material heroMaterial = heroSprite.GetCurrentSpriteDef().material;
				foreach (var def in downSlashData.spriteDefinitions)
					if (def != null)
						def.material.shader = heroMaterial.shader;
			}
		}

		tk2dSpriteAnimation downSlashAnims = downSlashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		downSlashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver DownSlash Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					downSlashData.CreateFrame(downSlashTex[0].name, triggerEvent: true),
					.. downSlashData.CreateFrames(downSlashTex.Skip(1).Select(t => t.name)),
					downSlashData.CreateFrame(downSlashTex[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike Antic",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = downSlashData.CreateFrames(downSlashHornetTex.Take(2).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "DownSpike",
				fps = 15,
				wrapMode = WrapMode.Once,
				frames = downSlashData.CreateFrames(downSlashHornetTex.Skip(2).Take(5).Select(t => t.name)),
			},
			new tk2dSpriteAnimationClip {
				name = "Downspike Recovery",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = downSlashData.CreateFrames(downSlashHornetTex.Skip(7).Select(t => t.name)),
			},
		];
		downSlashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, downSlashAnims.clips[1], downSlashAnims.clips[2], downSlashAnims.clips[3]];

		YenCrest.Moveset.DownSlash = new DownAttack {
			Name = "WeaverDownSlash",
			AnimName = "Weaver DownSlash Effect",
			AnimLibrary = downSlashAnims,
			Hitbox = [new Vector2(-1.0f, 0.4f),new Vector2(-0.4f, 0.4f),new Vector2(1.2f, -3.2f),new Vector2(0.6f, -3.2f),],
		};
	}
}
