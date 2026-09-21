using Needleforge.Data;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {
	private static GameObject? animLibObj;

	private static tk2dSpriteAnimation sharedLib = null!;
	private static Vector2[] standardHitbox = null!;

	internal static void Setup() {
		sharedLib = GetOrCreateAnimationLibrary();

	// Individual attack .cs files
	// Add silkless versions later
		Slash();
		AltSlash();
		WallSlash();
		UpSlash();
		DownSlash();
		DashSlash();
		ChargedSlash();

		sharedLib.isValid = false;
		sharedLib.ValidateLookup();

		EditHeroConfig();
	}

	private static void EditHeroConfig() {
		HeroConfigNeedleforge yenConfig = YenCrest.Moveset.HeroConfig!;
		yenConfig.heroAnimOverrideLib = GetOrCreateAnimationLibrary();

		yenConfig.downSlashType = HeroControllerConfig.DownSlashTypes.DownSpike;
		yenConfig.SetDownspikeFields(
			anticTime: 0.1f, time: 0.15f, recoveryTime: 0.05f,
			doesThrust: true, velocity: new Vector2(-15, -15), doesBurstEffect: true
		);
		
		//attack cooldown... (figure out later)

		yenConfig.canBind = true;
		yenConfig.SetCanUseAbilities(true);
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

		animLibObj = new GameObject($"{YenId}_AnimLib") {
			hideFlags = HideFlags.HideAndDontSave
		};
		Object.DontDestroyOnLoad(animLibObj);
		var animLib = animLibObj.AddComponent<tk2dSpriteAnimation>();
		animLib.clips = [];
		animLib.isValid = false;
		animLib.ValidateLookup();
		return animLib;
	}
}
