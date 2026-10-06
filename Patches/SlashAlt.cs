using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static readonly Vector2[] AltHitbox = [new(0f, 0.5f), new(-1.1f, 0.9f), new(-2.1f, 0.6f), 
	new(-2.8f, 0.1f), new(-2.8f, -0.5f), new(-2.1f, -1.1f), new(-1.0f, -1.3f), new(0.2f, -0.8f)];

	private static void SlashAlt() {
		string[] altSlashFiles = ["slash_e0000.png", "slash_e0001.png", "slash_e0002.png", "slash_e0003.png", "slash_e0004.png"];
		Texture2D[] altSlashTex = LoadNamedTextures(altSlashFiles);
		string[] altSlashHornetFiles = ["slashAlt_0000.png", "slashAlt_0001.png", "slashAlt_0002.png", "slashAlt_0003.png", "slashAlt_0004.png"];
		Texture2D[] altSlashHornetTex = LoadNamedTextures(altSlashHornetFiles);

		tk2dSpriteCollectionData altSlashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. altSlashTex, .. altSlashHornetTex],
			spriteCenters: [
				.. altSlashTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(110f, -10f)),
				.. altSlashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(altSlashData.gameObject);
		altSlashData.gameObject.name = $"{YenId}_AltSlashAnim";
		altSlashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		
		heroShaderCollections.Add(altSlashData);

		tk2dSpriteAnimation altSlashAnims = altSlashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		altSlashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver SlashAlt Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					altSlashData.CreateFrame(altSlashTex[0].name, triggerEvent: true),
					.. altSlashData.CreateFrames(altSlashTex.Skip(1).Select(t => t.name)),
					altSlashData.CreateFrame(altSlashTex[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "SlashAlt",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = altSlashData.CreateFrames(altSlashHornetTex.Select(t => t.name)),
			},
		];
		altSlashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, altSlashAnims.clips[1]];

		YenCrest.Moveset.AltSlash = new Attack {
			Name = "Weaver SlashAlt",
			AnimName = "Weaver SlashAlt Effect",
			AnimLibrary = altSlashAnims,
			Hitbox = AltHitbox,
		};
	}
}
