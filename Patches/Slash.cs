using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	private static void Slash() {
		string[] slashFiles = ["slash_e0000.png", "slash_e0001.png", "slash_e0002.png", "slash_e0003.png", "slash_e0004.png"];
		Texture2D[] slashTex = LoadNamedTextures(slashFiles);
		string[] slashHornetFiles = ["slash0000.png", "slash0001.png", "slash0002.png", "slash0003.png", "slash0004.png"];
		Texture2D[] slashHornetTex = LoadNamedTextures(slashHornetFiles);

		tk2dSpriteCollectionData slashData = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: [.. slashTex, .. slashHornetTex],
			spriteCenters: [
				.. slashTex.Select(t => new Vector2(t.width, t.height) * 0.5f + new Vector2(100f, 0f)),
				.. slashHornetTex.Select(t => new Vector2(t.width, t.height) * 0.5f),
			]
		);
		Object.DontDestroyOnLoad(slashData.gameObject);
		slashData.gameObject.name = $"{YenId}_SlashAnim";
		slashData.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		{
			tk2dSprite? heroSprite = HeroController.instance.GetComponentInChildren<tk2dSprite>();
			if (heroSprite != null) {
				Material heroMaterial = heroSprite.GetCurrentSpriteDef().material;
				foreach (var def in slashData.spriteDefinitions)
					if (def != null)
						def.material.shader = heroMaterial.shader;
			}
		}

		tk2dSpriteAnimation slashAnims = slashData.gameObject.AddComponent<tk2dSpriteAnimation>();
		slashAnims.clips = [
			new tk2dSpriteAnimationClip {
				name = "Weaver Slash Effect",
				fps = 12,
				wrapMode = WrapMode.Once,
				frames = [
					slashData.CreateFrame(slashTex[0].name, triggerEvent: true),
					.. slashData.CreateFrames(slashTex.Skip(1).Select(t => t.name)),
					slashData.CreateFrame(slashTex[^1].name, triggerEvent: true),
				],
			},
			new tk2dSpriteAnimationClip {
				name = "Slash",
				fps = 10,
				wrapMode = WrapMode.Once,
				frames = slashData.CreateFrames(slashHornetTex.Select(t => t.name)),
			},
		];
		slashAnims.ValidateLookup();
		sharedLib.clips = [.. sharedLib.clips, slashAnims.clips[1]];

		YenCrest.Moveset.Slash = new Attack {
			Name = "WeaverSlash",
			AnimName = "Weaver Slash Effect",
			AnimLibrary = slashAnims,
			Hitbox = [new Vector2(-0.3f, 0.8f),new Vector2(-1.5f, 1.3f),new Vector2(-2.8f, 1.0f),new Vector2(-3.6f, 0.3f),
			new Vector2(-3.6f, -0.3f),new Vector2(-2.8f, -1.0f),new Vector2(-1.5f, -1.3f),new Vector2(-0.3f, -0.8f),],
		};
		standardHitbox = YenCrest.Moveset.Slash.Hitbox;
	}
}
