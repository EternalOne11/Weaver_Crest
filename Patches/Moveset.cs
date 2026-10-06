using Needleforge.Data;
using Needleforge.Attacks;
using Silksong.UnityHelper.Util;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {
	private static GameObject? animLibObj;

	private static tk2dSpriteAnimation sharedLib = null!;

	// For Sprite collections that need Hero shader
	private static readonly List<tk2dSpriteCollectionData> heroShaderCollections = [];

	#region Called from Awake

	internal static void ImportAnimations() {
		sharedLib = GetOrCreateAnimationLibrary();
	}

	internal static void SetupSlashes() {
		// Individual attack .cs files
		// Add silkless versions later
		Slash();
		SlashAlt();
		WallSlash();
		UpSlash();
		DownSlash();
		DashSlash();
		ChargedSlash();
		MaskBreak(); //in silkMechanics
		BindAnims(); //in Bind.cs

		sharedLib.isValid = false;
		sharedLib.ValidateLookup();
	}

	//mask break animation and effect
	internal static tk2dSpriteAnimationClip MaskBreakClip { get; private set; } = null!;
	internal static tk2dSpriteCollectionData MaskBreakCollection { get; private set; } = null!;

	private static void MaskBreak() {
		string[] maskBreakFiles = ["slash_e0000.png", "slash_e0001.png", "slash_e0002.png"];
		Texture2D[] maskBreakTex = LoadNamedTextures(maskBreakFiles);

		MaskBreakCollection = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: maskBreakTex,
			spriteCenters: [.. maskBreakTex.Select(t => new Vector2(t.width, t.height) * 0.5f)]
		);
		Object.DontDestroyOnLoad(MaskBreakCollection.gameObject);
		MaskBreakCollection.gameObject.name = $"{YenId}_MaskBreak";
		MaskBreakCollection.spriteDefinitions[0].material.EnableKeyword("IS_HERO");
		heroShaderCollections.Add(MaskBreakCollection);

		MaskBreakClip = new tk2dSpriteAnimationClip {
			name = "Weaver Mask Break",
			fps = 12,
			wrapMode = tk2dSpriteAnimationClip.WrapMode.Once,
			frames = MaskBreakCollection.CreateFrames(maskBreakTex.Select(t => t.name)),
		};
	}

	#endregion

	// Applying hero shader to attack sprites
	internal static void ApplyHeroShader() {
		HeroController hero = HeroController.instance;

		tk2dSprite? heroSprite = hero.GetComponentInChildren<tk2dSprite>();
		if (heroSprite == null) return;

		Shader heroShader = heroSprite.GetCurrentSpriteDef().material.shader;
		foreach (var data in heroShaderCollections)
			foreach (var def in data.spriteDefinitions)
				if (def != null)
					def.material.shader = heroShader;
	}

	// OnInitialized not from Awake
	internal static void EditHeroConfig() {
		HeroConfigNeedleforge yenConfig = YenCrest.Moveset.HeroConfig!;

		yenConfig.heroAnimOverrideLib = GetOrCreateAnimationLibrary();

		yenConfig.downSlashType = HeroControllerConfig.DownSlashTypes.DownSpike;
		yenConfig.SetDownspikeFields(
			anticTime: DownAnticTime, time: DownThrustTime, recoveryTime: DownRecoveryTime,
			doesThrust: false, velocity: Vector2.zero, doesBurstEffect: false
		);

		yenConfig.canBind = true;
		yenConfig.SetCanUseAbilities(true);
		yenConfig.SetAttackFields(time: 0.35f, recovery: 0.15f, cooldown: 0.41f, // Regular attack speeds
			quickSpeedMult: 1.5f, quickCooldown: 0.205f); // Flea Brew

		yenConfig.SetDashStabFields(time: 0.15f, speed: -26f, bounceJumpSpeed: 20f);
		
		yenConfig.ChargedSlashFsmEdit = ChargedFsmEdit;
	}

	private static Texture2D[] LoadNamedTextures(string[] files) =>
		files.Select(f => {
			Texture2D t = LoadEmbeddedPngAsTexture2D(f);
			t.name = f;
			return t;
		}).ToArray();
	private static tk2dSpriteAnimation GetOrCreateAnimationLibrary() {
		if (animLibObj)
			return animLibObj.GetComponent<tk2dSpriteAnimation>();

		animLibObj = new GameObject($"{YenId}_AnimLib");
		Object.DontDestroyOnLoad(animLibObj);
		var animLib = animLibObj.AddComponent<tk2dSpriteAnimation>();
		animLib.clips = [];
		return animLib;
	}
}
