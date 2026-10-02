using System;
using System.Collections;
using System.Linq;
using Needleforge.Data;
using Silksong.UnityHelper.Util;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;
using WrapMode = tk2dSpriteAnimationClip.WrapMode;

namespace Weaver_Crest.Patches;

internal static class Hud
{
    // Frames 0-4 are the transition frames for equipping and unquiping the crest.
    // Frame 5 is the static frame/default icon
	// Frames 6-12 are the transition frames for when silk is gained or lost. 
    // Frame 13, is the static/default frame when hornet has more than 1 silk.
	// Appear includes appear and disappear animations.
	// Idle means the static frame, that is displayed at most times.

    // Frames for regular Mode
    private static readonly string[] Appear =
        { "Hud0.png", "Hud1.png", "Hud2.png", "Hud3.png", "Hud4.png" };
    private const string IdleFrame = "Hud5.png";
    private static readonly string[] SilkFrames =
        { "Hud6.png", "Hud7.png", "Hud8.png", "Hud9.png", "Hud10.png", "Hud11.png", "Hud12.png" };
    private const string SilkIdle = "Hud13.png";

    // Frames For Steel Soul Mode
    private static readonly string[] SteelAppear =
        { "Hud0_S.png", "Hud1_S.png", "Hud2_S.png", "Hud3_S.png", "Hud4_S.png" };
    private const string SteelIdleFrame = "Hud5_S.png";
    private static readonly string[] SteelSilkFrames =
        { "Hud6_S.png", "Hud7_S.png", "Hud8_S.png", "Hud9_S.png", "Hud10_S.png", "Hud11_S.png", "Hud12_S.png" };
    private const string SteelSilkIdle = "Hud13_S.png";

    private const int AppearFps = 11;
    private const int SilkFps = 17;
    private const float PixelsPerUnit = 64f;

    // Position tuning for hud location.
	// The values X,Y Represent, horizontal and vertical respectivly. 
	// X is the horizontal axis, Negative values move the image right, Positive move them left.
	// Y is the Vertical axis, Negative values move it Down, Postive move it Up.
    private static readonly Vector2 OffsetPixels = new(-70f, 28f);

    internal static void Setup(CrestData crest)
    {
        BuildAndAssignClips(crest);
    }

    private static void BuildAndAssignClips(CrestData crest)
    {
        // Needleforge's inbuilt  switching
        // Automates Appear, Idle, Disappear, and Steel Soul.
        crest.HudFrame.Appear = BuildClip(crest, "Appear", Appear, AppearFps, WrapMode.Once);
        crest.HudFrame.SteelAppear = BuildClip(crest, "Appear", SteelAppear, AppearFps, WrapMode.Once);
        Log.LogInfo("Hud.Setup: Appear/SteelAppear clips built OK");

        crest.HudFrame.Idle = BuildClip(crest, "Idle", [IdleFrame], 1, WrapMode.Once);
        crest.HudFrame.SteelIdle = BuildClip(crest, "Idle", [SteelIdleFrame], 1, WrapMode.Once);
        Log.LogInfo("Hud.Setup: Idle/SteelIdle clips built OK");

        crest.HudFrame.Disappear = BuildClip(crest, "Disappear", Enumerable.Reverse(Appear).ToArray(), AppearFps, WrapMode.Once);
        crest.HudFrame.SteelDisappear = BuildClip(crest, "Disappear", Enumerable.Reverse(SteelAppear).ToArray(), AppearFps, WrapMode.Once);
        Log.LogInfo("Hud.Setup: Disappear/SteelDisappear clips built OK");

        // Custom SlkSilk Mechanic for the hud. 
        // Has a built in Steel Soul check.
        tk2dSpriteAnimationClip GainSilk = BuildClip(crest, "GainSilk", SilkFrames, SilkFps, WrapMode.Once);
        tk2dSpriteAnimationClip SteelGainSilk = BuildClip(crest, "GainSilkSteel", SteelSilkFrames, SilkFps, WrapMode.Once);
        Log.LogInfo("Hud.Setup: GainSilk/SteelGainSilk clips built OK");

        tk2dSpriteAnimationClip LoseSilk = BuildClip(crest, "LoseSilk", Enumerable.Reverse(SilkFrames).ToArray(), SilkFps, WrapMode.Once);
        tk2dSpriteAnimationClip SteelLoseSilk = BuildClip(crest, "LoseSilkSteel", Enumerable.Reverse(SteelSilkFrames).ToArray(), SilkFps, WrapMode.Once);
        Log.LogInfo("Hud.Setup: LoseSilk/SteelLoseSilk clips built OK");

        tk2dSpriteAnimationClip IdleSilk = BuildClip(crest, "IdleSilk", [SilkIdle], 1, WrapMode.Once);
        tk2dSpriteAnimationClip SteelIdleSilk = BuildClip(crest, "IdleSilkSteel", [SteelSilkIdle], 1, WrapMode.Once);
        Log.LogInfo("Hud.Setup: IdleSilk/SteelIdleSilk clips built OK");

        tk2dSpriteAnimationClip SteelIdleNoSilk = BuildClip(crest, "SteelIdleNoSilk", [SteelIdleFrame], 1, WrapMode.Once);
        Log.LogInfo("Hud.Setup: SteelIdleNoSilk clip built OK");

        crest.HudFrame.ExtraAnims.Add(GainSilk);
        crest.HudFrame.ExtraAnims.Add(LoseSilk);
        crest.HudFrame.ExtraAnims.Add(IdleSilk);
        crest.HudFrame.ExtraAnims.Add(SteelGainSilk);
        crest.HudFrame.ExtraAnims.Add(SteelLoseSilk);
        crest.HudFrame.ExtraAnims.Add(SteelIdleSilk);
        crest.HudFrame.ExtraAnims.Add(SteelIdleNoSilk);

        tk2dSpriteAnimationClip IdleNoSilk = crest.HudFrame.Idle!;

        crest.HudFrame.Coroutine = hudInstance => SilkCheck(
            crest, hudInstance,
            GainSilk, LoseSilk, IdleSilk, IdleNoSilk,
            SteelGainSilk, SteelLoseSilk, SteelIdleSilk, SteelIdleNoSilk);

        bool allSet = crest.HudFrame.Appear != null && crest.HudFrame.Idle != null && crest.HudFrame.Disappear != null;
        Log.LogInfo($"Hud.Setup: finished. Appear/Idle/Disappear all set = {allSet}");
    }

    private static Texture2D LoadNamedTexture(string filename)
    {
        Texture2D tex = LoadEmbeddedPngAsTexture2D(filename);
        tex.name = filename;
        return tex;
    }

    private static tk2dSpriteAnimationClip BuildClip(CrestData crest, string clipSuffix, string[] files, int fps, WrapMode wrapMode)
    {
        Texture2D[] textures = files.Select(LoadNamedTexture).ToArray();

        Vector2[] centers = textures
            .Select(t => new Vector2(
                t.width / 2f + OffsetPixels.x,
                t.height / 2f + OffsetPixels.y))
            .ToArray();

        tk2dSpriteCollectionData collection = Tk2dUtil.CreateTk2dSpriteCollection(
            sprites: textures,
            spriteCenters: centers,
            pixelsPerUnit: PixelsPerUnit
        );

        return new tk2dSpriteAnimationClip
        {
            name = $"{crest.name} HUD {clipSuffix}",
            fps = fps,
            wrapMode = wrapMode,
            frames = collection.CreateFrames(textures.Select(t => t.name)),
        };
    }

    // Check to see if the Hornet, has any silk, to decide if the hud should swap.
    private static IEnumerator SilkCheck(
        CrestData crest, BindOrbHudFrame hudInstance,
        tk2dSpriteAnimationClip GainSilk, tk2dSpriteAnimationClip LoseSilk,
        tk2dSpriteAnimationClip IdleSilk, tk2dSpriteAnimationClip IdleNoSilk,
        tk2dSpriteAnimationClip SteelGainSilk, tk2dSpriteAnimationClip SteelLoseSilk,
        tk2dSpriteAnimationClip SteelIdleSilk, tk2dSpriteAnimationClip SteelIdleNoSilk)
    {
        // Animation delay upon login, before the silk varient can activate.
		// This is to prevent the animation from being overridden by the standard one.
        yield return new WaitForSeconds(0.7f);

        // Steel Soul check
        bool isSteelSoul = (int)PlayerData.instance.permadeathMode == 1;
        Log.LogInfo($"SilkCheck: permadeathMode={PlayerData.instance.permadeathMode} ({(int)PlayerData.instance.permadeathMode}), isSteelSoul={isSteelSoul}");

        tk2dSpriteAnimationClip
            DecideGainSilk = isSteelSoul ? SteelGainSilk : GainSilk,
            DecideLoseSilk = isSteelSoul ? SteelLoseSilk : LoseSilk,
            DecideIdleSilk = isSteelSoul ? SteelIdleSilk : IdleSilk,
            DecideIdleNoSilk = isSteelSoul ? SteelIdleNoSilk : IdleNoSilk;

		bool hadSilk = PlayerData.instance.silk > 0 || HalfSilk.GetHasHalfSilk();
        if (hadSilk)
		{
			hudInstance.PlayFrameAnim(DecideGainSilk.name, 0);
			yield return new WaitForSeconds(DecideGainSilk.frames.Length / (float)DecideGainSilk.fps);
			hudInstance.PlayFrameAnim(DecideIdleSilk.name, 0);
		}
        while (true)
        {
            if (!crest.IsEquipped)
                yield break;

            bool hasSilk = PlayerData.instance.silk > 0 || HalfSilk.GetHasHalfSilk();
            if (hasSilk != hadSilk)
            {
                tk2dSpriteAnimationClip transition = hasSilk ? DecideGainSilk : DecideLoseSilk;
				tk2dSpriteAnimationClip settleOn = hasSilk ? DecideIdleSilk : DecideIdleNoSilk;
				hudInstance.PlayFrameAnim(transition.name, 0);
				yield return new WaitForSeconds(transition.frames.Length / (float)transition.fps);
				hudInstance.PlayFrameAnim(settleOn.name, 0);
				hadSilk = hasSilk;
            }

            yield return null;
        }
    }
}
