using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static readonly Vector2[] UpHitbox = [new(-1.4f, -0.7f), new(-1.4f, 1.2f), new(-1f, 2.2f),
	new(-0.2f, 2.8f), new(0.6f, 2.8f), new(1.1f, 1.8f), new(0.7f, -0.3f)];

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
				.. upSlashTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(20f, 80f)),
				.. upSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(upSlashData.gameObject);
		upSlashData.gameObject.name = $"{YenId}_UpSlashAnim";
		upSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		
		heroShaderCollections.Add(upSlashData);

		tk2dSpriteAnimation upSlashAnims = upSlashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		upSlashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver UpSlash Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					upSlashData.CreateFrame(upSlashTex[0].name, triggerEvent: true),
					.. upSlashData.CreateFrames(upSlashTex.Skip(1).Take(upSlashTex.Length - 2).Select(t => t.name)),
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
			Hitbox = UpHitbox,
		};
	}
}
