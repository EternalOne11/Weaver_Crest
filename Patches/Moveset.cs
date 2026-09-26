using Needleforge.Data;
using Needleforge.Attacks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {
	private static GameObject? animLibObj;

	private static tk2dSpriteAnimation sharedLib = null!;
	private static readonly Vector2[] standardHitbox = [
	new Vector2(-0.3f, 0.8f), new Vector2(-1.5f, 1.3f), new Vector2(-2.8f, 1.0f), new Vector2(-3.6f, 0.3f),
	new Vector2(-3.6f, -0.3f), new Vector2(-2.8f, -1.0f), new Vector2(-1.5f, -1.3f), new Vector2(-0.3f, -0.8f),
];

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

		sharedLib.isValid = false;
		sharedLib.ValidateLookup();
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
			anticTime: 0.1f, time: 0.15f, recoveryTime: 0.05f,
			doesThrust: false, velocity: new Vector2(-15, -15), doesBurstEffect: true
		);

		yenConfig.canBind = true;
		yenConfig.SetCanUseAbilities(true);
		yenConfig.SetAttackFields(time: 0.35f, recovery: 0.15f, cooldown: 0.41f, // Regular attack speeds
			quickSpeedMult: 1.5f, quickCooldown: 0.205f); // Flea Brew

		yenConfig.SetDashStabFields(time: 0.3f, speed: -30, bounceJumpSpeed: 40); //default (experiment)
		
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
