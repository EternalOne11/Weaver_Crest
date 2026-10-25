using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;
using static Weaver_Crest.Utils.ResourceUtils;
using Silksong.UnityHelper.Util;
using Unity.Collections;

#pragma warning disable HARMONIZE003, HARMONIZE004

namespace Weaver_Crest.Patches;

public static class HalfSilk
{
    /// <summary>
    /// Adds a half silk or completes a half silk into a full silk
    /// </summary>
    /// <param name="heroEffect">Should hornet flash if a silk is added</param>
    public static void AddHalfSilk(bool heroEffect = true)
    {
        if (GetHasHalfSilk())
        {
            Log.LogInfo($"AddHalfSilk - Already has half silk");
            HasHalfSilk = false;
            UpgradeHalfSilk();
            HeroController.instance.AddSilk(1, heroEffect);
        }
        else if (PlayerData.instance.silk < PlayerData.instance.CurrentSilkMax)
        {
            Log.LogInfo($"AddHalfSilk - Doesn't have half silk");
            HasHalfSilk = true;
            SpawnHalfSilk();
            AfterAddSilk(HeroController.instance);
            SilkSpool.Instance.EvaluatePositions();
            if (heroEffect)
                HeroController.instance.spriteFlash.flashFocusHeal();
        }
    }
    
    /// <summary>
    /// Removes a half silk or turns a full silk into a half silk
    /// </summary>
    /// <param name="interruptRegen">False = silk-heart regeneration keeps going</param>
    public static void RemoveHalfSilk(bool interruptRegen = true)
    {
        if (GetHasHalfSilk())
        {
            HasHalfSilk = false;
            DespawnHalfSilk();
            SilkSpool.Instance.EvaluatePositions();
        }
        else if (PlayerData.instance.silk > 0)
        {
            HasHalfSilk = true;
            if (!TransformLastSilk())
            {
                PlayerData.instance.silk = Mathf.Max(0, PlayerData.instance.silk - 1);
                return;
            }
            if (interruptRegen)
                HeroController.instance.TakeSilk(1);
            else
                TakeSilkQuietly();
        }
    }
    private static void TakeSilkQuietly()
    {
        PlayerData.instance.silk = Mathf.Max(0, PlayerData.instance.silk - 1);
        SilkSpool.Instance.EvaluatePositions();

        if (PlayerData.instance.silk < SilkSpool.BindCost)
        {
            if (CurrentHalf)
                CurrentHalf.EndGlow();
            foreach (SilkChunk chunk in SilkSpool.Instance.silkChunks)
                chunk.EndGlow();
        }
        SilkSpool.Instance.RefreshSilk(default, default);
    }

    /// <summary>
    /// Sets the state of having a half silk without adding or removing full silk
    /// </summary>
    /// <param name="active">The state the half silk will be set to</param>
    /// <param name="heroEffect">Should hornet flash if a silk is added?</param>
    public static void SetHalfSilkState(bool active, bool heroEffect = true)
    {
        Log.LogInfo($"SetHalfSilkState - {true}");
        if (active)
        {
            HasHalfSilk = true;
            SpawnHalfSilk();
            if (heroEffect)
                HeroController.instance.spriteFlash.flashFocusHeal();
        }
        else
        {
            HasHalfSilk = false;
            DespawnHalfSilk();
        }
        SilkSpool.Instance.EvaluatePositions();
    }
    
    /// <summary>
    /// Checks if there is currently a half silk
    /// </summary>
    /// <returns>True if there is currently a half silk</returns>
    public static bool GetHasHalfSilk() => HasHalfSilk;

    public static void Initialize()
    {
        SpriteCollection = BuildCollection("weaver hud silk blob.000", 0, 7);
        HalfSilkIdle = BuildClip(SpriteCollection, "weaver hud silk blob.000", 4, 4, 30f, tk2dSpriteAnimationClip.WrapMode.Once);
        HalfSilkAppear = BuildClip(SpriteCollection, "weaver hud silk blob.000", 0, 4, 30f, tk2dSpriteAnimationClip.WrapMode.Once);
        HalfSilkDisappear = BuildClip(SpriteCollection, "weaver hud silk blob.000", 3, 0, 30f, tk2dSpriteAnimationClip.WrapMode.Once);
        HalfSilkGrow = BuildClip(SpriteCollection, "weaver hud silk blob.000", 5, 7, 30f, tk2dSpriteAnimationClip.WrapMode.Once);
        HalfSilkShrink = BuildClip(SpriteCollection, "weaver hud silk blob.000", 7, 4, 30f, tk2dSpriteAnimationClip.WrapMode.Once);

        // Half silk glow
        Texture2D glowTex = LoadNamedTexture("silk blob chunk glow.png");
        tk2dSpriteCollectionData glowCollection = Tk2dUtil.CreateTk2dSpriteCollection(
            sprites: [glowTex],
            spriteCenters: [new Vector2(glowTex.width / 2f + 0f, glowTex.height / 2f + 3.5f)],
            pixelsPerUnit: 64f
        );
        UnityEngine.Object.DontDestroyOnLoad(glowCollection.gameObject);
        tk2dSpriteAnimationFrame[] glowFrame = glowCollection.CreateFrames([glowTex.name]);
        tk2dSpriteAnimationFrame[] idleFrame = SpriteCollection.CreateFrames(["weaver hud silk blob.0004.png"]);
        HalfSilkGlow = new tk2dSpriteAnimationClip
        {
            name = "HalfSilkGlow",
            fps = 8f,
            wrapMode = tk2dSpriteAnimationClip.WrapMode.Loop,
            frames = [.. glowFrame, .. idleFrame],
        };
    }
    private static tk2dSpriteCollectionData BuildCollection(string prefix, int startFrame, int endFrame)
    {
        Texture2D[] textures = Enumerable.Range(startFrame, endFrame + 1 - startFrame).Select(i => LoadNamedTexture($"{prefix}{i}.png")).ToArray();

        Vector2[] centers = textures.Select(t => new Vector2(t.width / 2f, t.height / 2f)).ToArray();

        return Tk2dUtil.CreateTk2dSpriteCollection(
            sprites: textures,
            spriteCenters: centers,
            pixelsPerUnit: 64f
        );
    }

    private static tk2dSpriteAnimationClip BuildClip(tk2dSpriteCollectionData collection, string prefix, int startFrame, int endFrame, float fps, tk2dSpriteAnimationClip.WrapMode wrapMode)
    {
        return new tk2dSpriteAnimationClip
        {
            name = $"{prefix}{startFrame}-{endFrame}",
            fps = fps,
            wrapMode = wrapMode,
            frames = collection.CreateFrames(endFrame > startFrame ?
                Enumerable.Range(startFrame, endFrame + 1 - startFrame).Select(i => $"{prefix}{i}.png") :
                Enumerable.Range(endFrame, startFrame + 1 - endFrame).Reverse().Select(i => $"{prefix}{i}.png")),
        };
    }

    private static Texture2D LoadNamedTexture(string filename)
    {
        Texture2D tex = LoadEmbeddedPngAsTexture2D(filename);
        tex.name = filename;
        return tex;
    }

    private static tk2dSpriteAnimationClip HalfSilkIdle = null!;
    private static tk2dSpriteAnimationClip HalfSilkAppear = null!;
    private static tk2dSpriteAnimationClip HalfSilkDisappear = null!;
    private static tk2dSpriteAnimationClip HalfSilkGrow = null!;
    private static tk2dSpriteAnimationClip HalfSilkShrink = null!;
    private static tk2dSpriteAnimationClip HalfSilkGlow = null!;
    private static tk2dSpriteCollectionData SpriteCollection = null!;
    private static bool AddedAnimations = false;

    internal static bool NeedolinUsingHalf;
    internal static void RefreshHalfSilkColor()
    {
        if (!GetHasHalfSilk() || !CurrentHalf)
            return;
        CurrentHalf.FadeToCorrectColor();
        CurrentHalf.animator.Play(NeedolinUsingHalf ? HalfSilkGlow : HalfSilkIdle);
    }

    private static SilkChunk CurrentHalf = null!;
    private static bool HasHalfSilk = false;
    private static SilkSpool.SilkUsingFlags HalfSilkUsingFlags = SilkSpool.SilkUsingFlags.None;

    private static void TryAddAnims()
    {
        if (AddedAnimations)
            return;
        
        // SilkChunk Temp Use for half silk.
        tk2dSpriteAnimationClip tempUse = CurrentHalf.animator.Library.GetClipByName("SilkChunk Temp Use");
        if (tempUse != null && tempUse.frames.Length > 0)
        {
            tk2dSpriteAnimationFrame glow = HalfSilkGlow.frames[0];
            tk2dSpriteAnimationFrame idle = HalfSilkGlow.frames.Length > 1 ? HalfSilkGlow.frames[1] : glow;
            HalfSilkGlow.fps = tempUse.fps;
            HalfSilkGlow.wrapMode = tempUse.wrapMode;
            HalfSilkGlow.frames = [.. Enumerable.Range(0, tempUse.frames.Length).Select(i => i % 2 == 0 ? glow : idle)];
        }
        CurrentHalf.animator.Library.isValid = false;
        AddedAnimations = true;
    }

    private static void SpawnHalfSilk()
    {
        Log.LogInfo($"SpawnHalfSilk");
        if (CurrentHalf)
        {
            ClearHalfMaggot();
            CurrentHalf.gameObject.Recycle();
        }
        CurrentHalf = SilkSpool.Instance.SpawnNewChunk(false);
        CurrentHalf.SetHalfSilk();
        TryAddAnims();

        if (SilkSpool.Instance.wasUsingChunk)
        {
            SilkSpool.Instance.wasUsingChunk.PlayIdle();
		    SilkSpool.SilkUsingFlags silkUsingFlags = SilkSpool.SilkUsingFlags.None;
            foreach (SilkSpool.SilkUsingFlags silkUsingFlags2 in SilkSpool.Instance.usingSilk)
                silkUsingFlags |= silkUsingFlags2;
            silkUsingFlags = HalfSilkSetUsing(silkUsingFlags);
            if (silkUsingFlags != SilkSpool.SilkUsingFlags.None)
                SilkSpool.Instance.wasUsingChunk.SetUsing(silkUsingFlags);
            else
                SilkSpool.Instance.wasUsingChunk = null;
        }
    }

    private static bool TransformLastSilk()
    {
        SilkChunk? last = SilkSpool.Instance.silkChunks.LastOrDefault(s => !s.IsRegen);
        if (!last)
            return false;

        if (CurrentHalf)
            CurrentHalf.gameObject.Recycle();
        CurrentHalf = last!;
        SilkSpool.Instance.silkChunks.Remove(CurrentHalf);
        CurrentHalf.SetHalfSilk(true);
        CurrentHalf.upAnim = HalfSilkShrink.name;
        TryAddAnims();

        if (SilkSpool.Instance.wasUsingChunk)
        {
            if (SilkSpool.Instance.wasUsingChunk != CurrentHalf)
            {
                SilkSpool.Instance.wasUsingChunk.PlayIdle();
            }
            SilkSpool.Instance.wasUsingChunk = null;
            SilkSpool.SilkUsingFlags silkUsingFlags = SilkSpool.SilkUsingFlags.None;
            foreach (SilkSpool.SilkUsingFlags silkUsingFlags2 in SilkSpool.Instance.usingSilk)
                silkUsingFlags |= silkUsingFlags2;
            silkUsingFlags = HalfSilkSetUsing(silkUsingFlags);
            if (silkUsingFlags != SilkSpool.SilkUsingFlags.None)
            {
                SilkSpool.Instance.wasUsingChunk = SilkSpool.Instance.silkChunks.LastOrDefault(s => !s.IsRegen);
                if (SilkSpool.Instance.wasUsingChunk)
                    SilkSpool.Instance.wasUsingChunk.SetUsing(silkUsingFlags);
            }
        }
        return true;
    }

    private static void UpgradeHalfSilk()
    {
        if (!CurrentHalf)
            return;
        Log.LogInfo($"UpgradeHalfSilk");
        ResetHalfSilkUsing();
        CurrentHalf.FinishHalfSilk();
        SilkSpool.Instance.silkChunks.Add(CurrentHalf);
        CurrentHalf.animator.AnimationCompletedEvent += UpgradeHalfSilkAnimComplete;
        CurrentHalf.animator.AnimationChanged += UpgradeHalfSilkAnimChanged;
        CurrentHalf.SetAnims();
        CurrentHalf.upAnim = HalfSilkGrow.name;
        SilkChunk upgraded = CurrentHalf;

        if (SilkSpool.Instance.wasUsingChunk)
        {
            SilkSpool.Instance.wasUsingChunk.PlayIdle();
            SilkSpool.Instance.wasUsingChunk = CurrentHalf;
            CurrentHalf.PlayIdle();
            SilkSpool.SilkUsingFlags silkUsingFlags = SilkSpool.SilkUsingFlags.None;
            foreach (SilkSpool.SilkUsingFlags silkUsingFlags2 in SilkSpool.Instance.usingSilk)
                silkUsingFlags |= silkUsingFlags2;
            CurrentHalf.SetUsing(silkUsingFlags);
        }
        CurrentHalf = null!;

        // The completed silk joins the "can bind" glow if there's now enough silk to bind. Done after it
        // stops being the half silk, since animations on the half silk are blocked
        if (SilkSpool.Instance.silkChunks.Count(s => !s.IsRegen) >= SilkSpool.BindCost)
            upgraded.StartGlow();
	}

    private static void UpgradeHalfSilkAnimComplete(tk2dSpriteAnimator animator, tk2dSpriteAnimationClip clip)
    {
        animator.AnimationCompletedEvent -= UpgradeHalfSilkAnimComplete;
        animator.AnimationChanged -= UpgradeHalfSilkAnimChanged;

        animator.Play(animator.GetComponent<SilkChunk>().idleAnim);
        //animator.GetComponent<SilkChunk>().PlayIdle();
        // revert void
    }

    private static void UpgradeHalfSilkAnimChanged(tk2dSpriteAnimator animator, tk2dSpriteAnimationClip previousClip, tk2dSpriteAnimationClip newClip)
    {
        animator.AnimationCompletedEvent -= UpgradeHalfSilkAnimComplete;
        animator.AnimationChanged -= UpgradeHalfSilkAnimChanged;

        // revert void
    }

    private static void ClearHalfMaggot()
    {
        if (!CurrentHalf)
            return;
        ResetHalfSilkUsing();
        CurrentHalf.isMaggoted = false;
    }

    private static void DespawnHalfSilk()
    {
        if (!CurrentHalf)
            return;
        ClearHalfMaggot();
        CurrentHalf.FinishHalfSilk(false);
        CurrentHalf.removeCounter = HalfSilkDisappear.Duration;
        //CurrentHalf.animator.AnimationCompletedEvent += DespawnHalfSilkAnimComplete;
        CurrentHalf = null!;
        SilkSpool.SilkUsingFlags silkUsingFlags = SilkSpool.SilkUsingFlags.None;
        foreach (SilkSpool.SilkUsingFlags silkUsingFlags2 in SilkSpool.Instance.usingSilk)
            silkUsingFlags |= silkUsingFlags2;
        if (SilkSpool.Instance.wasUsingChunk)
        {
            if (silkUsingFlags == SilkSpool.SilkUsingFlags.None)
            {
                SilkSpool.Instance.wasUsingChunk.PlayIdle();
                SilkSpool.Instance.wasUsingChunk = null;
            }
            else
            {
                SilkSpool.Instance.wasUsingChunk.SetUsing(silkUsingFlags);
            }
        }
        else if (silkUsingFlags != SilkSpool.SilkUsingFlags.None)
        {
            SilkSpool.Instance.wasUsingChunk = SilkSpool.Instance.silkChunks.LastOrDefault(s => !s.IsRegen);
            if (SilkSpool.Instance.wasUsingChunk)
            {
                SilkSpool.Instance.wasUsingChunk.SetUsing(silkUsingFlags);
            }
        }
    }

    private static void DespawnHalfSilkAnimComplete(tk2dSpriteAnimator animator, tk2dSpriteAnimationClip clip)
    {
        animator.AnimationCompletedEvent -= DespawnHalfSilkAnimComplete;

        animator.gameObject.Recycle();
    }

    // These two methods mimick the implimentation Silksong already has

    public static void SetHalfSilk(this SilkChunk silk, bool fromFull = false)
    {
        if (fromFull)
		    silk.animator.Play(HalfSilkShrink);
        else
		    silk.animator.Play(HalfSilkAppear);
		silk.isExtraChunk = true;
        silk.idleAnim = silk.animator.currentClip.name;
    }

    public static void FinishHalfSilk(this SilkChunk silk, bool toFull = true)
    {
        if (toFull)
		    silk.animator.Play(HalfSilkGrow);
        else
		    silk.animator.Play(HalfSilkDisappear);
		silk.isExtraChunk = false;
    }

    [HarmonyPatch(typeof(SilkSpool), nameof(SilkSpool.ChangeSilk))]
    [HarmonyILManipulator]
    private static void InjectSilkSpoolChangeSilk(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(typeof(SilkSpool), nameof(SilkSpool.silkChunks)),
            x => x.MatchCallvirt(typeof(List<SilkChunk>), "get_Count")))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdarg(2),
            x => x.MatchLdcI4(0),
            x => x.MatchBle(out ILLabel _)))
        {
            cursor.EmitDelegate(ResetHalfSilkUsing);
        }
    }

    private static void ResetHalfSilkUsing()
    {
        HalfSilkUsingFlags = SilkSpool.SilkUsingFlags.None;
        if (!CurrentHalf)
            return;
		CurrentHalf.isUsing = false;
		CurrentHalf.isUsingAcid = false;
		CurrentHalf.FadeToColor(0f, CurrentHalf.acidFadeBackTime, CurrentHalf.BaseTint);
		if (CurrentHalf.acidUseEffect)
			CurrentHalf.acidUseEffect.StopParticleSystems();
		if (CurrentHalf.maggotUseEffect)
			CurrentHalf.maggotUseEffect.StopParticleSystems();
		if (CurrentHalf.voidUseEffect && CurrentHalf.voidUseEffect.IsPlaying())
		{
			CurrentHalf.voidUseEffect.StopParticleSystems();
			if (CurrentHalf.voidProtectEffect && CurrentHalf.hc && CurrentHalf.hc.playerData.HasWhiteFlower)
				CurrentHalf.voidProtectEffect.SetActive(true);
		}
		if (CurrentHalf.drainEffect)
			CurrentHalf.drainEffect.StopParticleSystems();
        CurrentHalf.FadeToCorrectColor();
    }

    [HarmonyPatch(typeof(SilkSpool), nameof(SilkSpool.RefreshSilk), [typeof(SilkSpool.SilkAddSource), typeof(SilkSpool.SilkTakeSource)])]
    [HarmonyILManipulator]
    private static void InjectSilkSpoolRefreshSilk(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdloc(1),
            x => x.MatchBrtrue(out ILLabel _)))
        {
            cursor.Index++;
            cursor.EmitDelegate(HalfSilkSetUsing);
        }
    }

    private static SilkSpool.SilkUsingFlags HalfSilkSetUsing(SilkSpool.SilkUsingFlags usingFlags)
    {
        if (!GetHasHalfSilk() || !CurrentHalf)
            return usingFlags;
        
        HalfSilkUsingFlags = usingFlags;

        SilkSpool.SilkUsingFlags ret = SilkSpool.SilkUsingFlags.None;
        
        bool needolinStyleUse = usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Normal);
        if (!needolinStyleUse && NeedolinUsingHalf)
        {
            NeedolinUsingHalf = false;
            CurrentHalf.animator.Play(HalfSilkIdle);
        }

        bool needolinOnHalf = NeedolinUsingHalf && needolinStyleUse;
        if (!needolinOnHalf && (usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Normal) || usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Drain)))
            ret = usingFlags;

        // Acid seems to be unused?
        if (usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Acid))
        {
            CurrentHalf.isUsingAcid = true;
            //CurrentHalf.sprite.color = CurrentHalf.acidUseColor;
            if (CurrentHalf.acidUseEffect)
                CurrentHalf.acidUseEffect.PlayParticleSystems();
        }
        else if (usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Maggot))
        {
            CurrentHalf.isUsingAcid = true;
            //CurrentHalf.StopColorFade();
            //CurrentHalf.sprite.color = CurrentHalf.maggotUseColor;
            if (CurrentHalf.maggotUseEffect)
                CurrentHalf.maggotUseEffect.PlayParticleSystems();
        }
        else if (usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Void))
        {
            CurrentHalf.isUsingAcid = true;
            //CurrentHalf.sprite.color = new Color(0f, 0f, 0f, 1f);
            if (CurrentHalf.voidUseEffect)
                CurrentHalf.voidUseEffect.PlayParticleSystems();
        }

        CurrentHalf.isUsing = true;

        CurrentHalf.FadeToCorrectColor();

        return ret;
    }

    [HarmonyPatch(typeof(SilkChunk), nameof(SilkChunk.Update))]
    [HarmonyPostfix]
    private static void AfterSilkChunkUpdate(SilkChunk __instance)
    {
        if (!GetHasHalfSilk() || __instance != CurrentHalf)
            return;

        bool maggoted = HeroController.instance.cState.isMaggoted;
        if (__instance.isMaggoted != maggoted)
        {
            __instance.isMaggoted = maggoted;
            __instance.FadeToCorrectColor();
        }
    }

    [HarmonyPatch(typeof(SilkChunk), nameof(SilkChunk.FadeToCorrectColor))]
    [HarmonyILManipulator]
    private static void InjectSilkColor(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(SilkChunk), nameof(SilkChunk.FadeToColor))))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(SetHalfSilkColor);
        }
    }

    private static Color SetHalfSilkColor(Color orig, SilkChunk self)
    {
        if (self != CurrentHalf)
            return orig;
        
        if (HalfSilkUsingFlags.HasFlag(SilkSpool.SilkUsingFlags.Normal) || HalfSilkUsingFlags.HasFlag(SilkSpool.SilkUsingFlags.Drain))
            orig *= 1.5f;
			
        if (HalfSilkUsingFlags.HasFlag(SilkSpool.SilkUsingFlags.Acid))
            orig = self.acidUseColor;
        
        if (HalfSilkUsingFlags.HasFlag(SilkSpool.SilkUsingFlags.Void))
            orig = new Color(0f, 0f, 0f, 1f);
        
        return orig;
    }

    [HarmonyPatch(typeof(SilkChunk), nameof(SilkChunk.PlayIdle))]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent1(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(SilkChunk), nameof(SilkChunk.FadeToColor))))
        {
            cursor.EmitDelegate(SilkChunkIdleFade);
            cursor.Remove();
        }

        InjectSilkAnimPrevent(cursor);
    }
    [HarmonyPatch(typeof(SilkChunk), "<FadeToColor>b__77_1")]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent2(ILContext il, MethodBase original) => InjectSilkAnimPrevent(new ILCursor(il));
    [HarmonyPatch(typeof(SilkChunk), nameof(SilkChunk.SetUsing))]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent3(ILContext il, MethodBase original) => InjectSilkAnimPrevent(new ILCursor(il));
    [HarmonyPatch(typeof(SilkChunk), "<SetUsing>b__65_0")]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent4(ILContext il, MethodBase original) => InjectSilkAnimPrevent(new ILCursor(il));
    [HarmonyPatch(typeof(SilkChunk), "<SetUsing>b__65_1")]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent5(ILContext il, MethodBase original) => InjectSilkAnimPrevent(new ILCursor(il));
    [HarmonyPatch(typeof(SilkChunk), "<SetUsing>b__65_2")]
    [HarmonyILManipulator]
    private static void InjectSilkAnimPrevent6(ILContext il, MethodBase original) => InjectSilkAnimPrevent(new ILCursor(il));
    private static void InjectSilkAnimPrevent(ILCursor cursor)
    {

        while (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCallvirt(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.Play))))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(SilkChunkPlayAnim);
            cursor.Remove();
        }
    }

    private static void SilkChunkPlayAnim(tk2dSpriteAnimator anim, string name, SilkChunk self)
    {
        if (self != CurrentHalf)
            anim.Play(name);
    }
    private static void SilkChunkIdleFade(SilkChunk self, float delay, float fadeTime, Color color)
    {
        if (self != CurrentHalf)
            self.FadeToColor(delay, fadeTime, color);
    }

    [HarmonyPatch(typeof(HeroSilkAcid), nameof(HeroSilkAcid.Update))]
    [HarmonyILManipulator]
    private static void InjectAcidUpdate(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }
    }

    [HarmonyPatch(typeof(HeroSilkAcid), nameof(HeroSilkAcid.StartSizzle))]
    [HarmonyILManipulator]
    private static void InjectAcidStartSizzle(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }
    }

    [HarmonyPatch(typeof(HeroSilkAcid), nameof(HeroSilkAcid.TakeSilk))]
    [HarmonyILManipulator]
    private static void InjectAcidTakeSilk(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdcI4(1),
            x => x.MatchCallvirt(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeSilkOrHalf);
            cursor.RemoveRange(2);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TickSilkEat))]
    [HarmonyILManipulator]
    private static void InjectHeroMaggot1(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeMaggotSilk);
            cursor.Remove();
        }

        if (cursor.TryGotoNext(MoveType.Before,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk)),
            x => x.MatchLdcI4(0),
            x => x.MatchBgt(out ILLabel _)))
        {
            cursor.Index++;
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }
    }

    private static void TakeMaggotSilk(HeroController hc, int _, SilkSpool.SilkTakeSource takeSource)
    {
        if (GetHasHalfSilk())
            RemoveHalfSilk();
        else
            hc.TakeSilk(1, takeSource);
    }

//Half Silk degredation speed
    private static bool maggotHalf;

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TickSilkEat))]
    [HarmonyPrefix]
    private static void MaggotHalfTiming(bool shouldEatSilk, ref HeroController.SilkEatTracker tracker,
        SilkSpool.SilkUsingFlags usingType, float delay)
    {
        if ((usingType & SilkSpool.SilkUsingFlags.Maggot) == 0)
            return;

        bool hasHalf = GetHasHalfSilk();
        bool halfAppeared = hasHalf && !maggotHalf;
        maggotHalf = hasHalf;
        if (!shouldEatSilk || !hasHalf)
            return;

        if (halfAppeared && (tracker.EatDelayLeft > 0f || tracker.EatDurationLeft > 0f))
        {
            if (tracker.EatDurationLeft > 0f)
                SilkSpool.Instance.RemoveUsing(usingType);
            tracker.EatDurationLeft = 0f;
            tracker.EatDelayLeft = delay;
        }

        float extra = Time.deltaTime * (2f - 1f);
        if (tracker.EatDurationLeft > 0f)
            tracker.EatDurationLeft = Mathf.Max(tracker.EatDurationLeft - extra, 0.001f);
        else if (tracker.EatDelayLeft > 0f)
            tracker.EatDelayLeft = Mathf.Max(tracker.EatDelayLeft - extra, 0.001f);
    }

    // Silk heart interaction
    private static bool regenHalf;

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.StartSilkRegen))]
    [HarmonyPostfix]
    private static void StartHalfRegen(HeroController __instance, ref float ___silkRegenDurationLeft)
    {
        PlayerData pd = __instance.playerData;
        regenHalf = ___silkRegenDurationLeft > 0f && GetHasHalfSilk()
            && pd.silk + 1 >= pd.CurrentSilkRegenMax;
        if (regenHalf)
            ___silkRegenDurationLeft /= 2f;
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.DoSilkRegen))]
    [HarmonyPostfix]
    private static void HalfSilkRegenDone()
    {

        if (regenHalf && GetHasHalfSilk())
            SetHalfSilkState(false);
        regenHalf = false;
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Update))]
    [HarmonyILManipulator]
    private static void InjectHeroMaggot2(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
        }
    }

    private static int IncreaseSilkIfHalfSilk(int orig) => GetHasHalfSilk() ? orig + 1 : orig;

    [HarmonyPatch("MaggotRegion+<TakeSilk>d__35, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", "MoveNext")]
    [HarmonyILManipulator]
    private static void InjectMaggot(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdcI4(1),
            x => x.MatchCallvirt(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeSilkOrHalf);
            cursor.RemoveRange(2);
        }
    }

    private static void TakeSilkOrHalf(HeroController hc)
    {
        if (GetHasHalfSilk())
            RemoveHalfSilk();
        else
            hc.TakeSilk(1);
    }

    [HarmonyPatch(typeof(SilkSpool), nameof(SilkSpool.EvaluatePositions))]
    [HarmonyILManipulator]
    private static void InjectPosition(ILContext il, MethodBase original)
    {
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(typeof(SilkSpool), nameof(SilkSpool.mossChunks))))
        {
            cursor.Emit(OpCodes.Ldloc_0);
            cursor.EmitDelegate(SetHalfSilkPosition);
            cursor.Emit(OpCodes.Stloc_0);
        }
    }

    private static float SetHalfSilkPosition(float currentPos)
    {
        if (CurrentHalf)
        {
            CurrentHalf.transform.localPosition = new Vector3(currentPos, 0f, -0.001f);
            currentPos += SilkSpool.Instance.chunkDistance_x;
        }
        return currentPos;
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Die), [typeof(bool), typeof(bool)])]
	[HarmonyPostfix]
	private static void ResetOnDeath()
    {
        Log.LogInfo($"ResetOnDeath");
        SetHalfSilkState(false);
	}

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Start))]
    [HarmonyPostfix]
    private static void ResetOnLoad()
    {
        NeedolinUsingHalf = false;
        HasHalfSilk = false;
        CurrentHalf = null!;
    }

    [HarmonyPatch(typeof(SilkSpool), nameof(SilkSpool.DrawSpool), [typeof(int)])]
    [HarmonyPostfix]
    private static void AfterDrawSpool()
    {
        if (GetHasHalfSilk() && !CurrentHalf)
        {
            SpawnHalfSilk();
            SilkSpool.Instance.EvaluatePositions();
        }
    }

    //Mossberry Interaction
    private static float mossMerge = -1f;
    private static int mossRingsMerged;

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Update))]
    [HarmonyPostfix]
    private static void MergeMossWithHalf(HeroController __instance, ref int ___mossCreep1Hits, ref int ___mossCreep2Hits)
    {
        if (HeroController.instance != __instance)
            return;

        if (___mossCreep2Hits != 1)
            mossRingsMerged = 0;

        bool ring1 = ___mossCreep1Hits == 1;
        bool ring2 = ___mossCreep2Hits == 1 && mossRingsMerged < 2;
        if ((!ring1 && !ring2) || !GetHasHalfSilk() || __instance.playerData.silk >= __instance.playerData.CurrentSilkMax)
        {
            mossMerge = -1f;
            return;
        }

        if (mossMerge < 0f)
        {
            mossMerge = Time.time + 0.3f;
            return;
        }
        if (Time.time < mossMerge)
            return;
        mossMerge = -1f;

        // Mossberry one ring merge
        if (ring1)
            ___mossCreep1Hits = 0;
        else if (++mossRingsMerged >= 2)
        {
            ___mossCreep2Hits = 0; // both of its rings are used up
            mossRingsMerged = 0;
        }
        RemoveOneMossRing();
        AddHalfSilk(heroEffect: false);
    }

    private static void RemoveOneMossRing()
    {
        SilkSpool spool = GameCameras.instance.silkSpool;
        if (spool.mossChunks == null || spool.mossChunks.Count <= 1)
        {
            spool.SetMossState(0);
            return;
        }
        SilkChunk ring = spool.mossChunks[spool.mossChunks.Count - 1];
        spool.mossChunks.RemoveAt(spool.mossChunks.Count - 1);
        ring.gameObject.Recycle();
        spool.EvaluatePositions();
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.AddSilk), [typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool)])]
    [HarmonyPostfix]
    private static void AfterMossCompletes(HeroController __instance, SilkSpool.SilkAddSource source, ref int ___mossCreep2Hits)
    {
        if (source != SilkSpool.SilkAddSource.Moss || ___mossCreep2Hits != 0 || mossRingsMerged == 0)
            return;
        if (__instance.playerData.silk >= __instance.playerData.CurrentSilkMax)
        {
            mossRingsMerged = 0;
            return;
        }

        __instance.TakeSilk(1);
        ___mossCreep2Hits = 1;
        mossRingsMerged = 1;
        GameCameras.instance.silkSpool.SetMossState(1, 2);
        RemoveOneMossRing();
    }

//Bench interaction
    private static bool AtBench;
    private static int silkSit;

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Update))]
    [HarmonyPostfix]
    private static void HalfBench(HeroController __instance)
    {
        if (HeroController.instance != __instance)
            return;

        PlayerData pd = __instance.playerData;
        if (pd.atBench && !AtBench)
            silkSit = pd.silk;
        AtBench = pd.atBench;

        if (pd.atBench && GetHasHalfSilk() && pd.silk > silkSit)
            SetHalfSilkState(false);
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.AddSilk), [typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool)])]
    [HarmonyPostfix]
    private static void AfterAddSilk(HeroController __instance)
    {
        int silkTotal = __instance.playerData.silk;
        if (__instance.playerData.silkParts > 0)
            silkTotal++;
        if (GetHasHalfSilk())
        {
            if (silkTotal >= __instance.playerData.CurrentSilkMax)
                SetHalfSilkState(false);
            else
                silkTotal++;
        }
        if (silkTotal >= __instance.playerData.CurrentSilkMax)
		{
			__instance.mossCreep1Hits = 0;
			__instance.mossCreep2Hits = 0;
			SilkSpool.Instance.SetMossState(0, 1);
		}
    }

    // Needolin interactions/mechanics
    private static bool NeedolinHasHalf() => YenCrest.IsEquipped && GetHasHalfSilk();

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeSilk), [typeof(int), typeof(SilkSpool.SilkTakeSource)])]
    [HarmonyPrefix]
    private static bool NeedolinTakeHalfFirst(int amount, SilkSpool.SilkTakeSource source)
    {
        if (source != SilkSpool.SilkTakeSource.ActiveUse || amount != 1 || !NeedolinHasHalf())
            return true;
        NeedolinUsingHalf = false;
        RemoveHalfSilk();
        return false;
    }

    // Half duration interaction
    private static float? needolinFullTime;

    [HarmonyPatch(typeof(TimeLimitSetV2), nameof(TimeLimitSetV2.OnEnter))]
    [HarmonyPrefix]
    private static void NeedolinHalfTime(TimeLimitSetV2 __instance)
    {
        if (__instance.TimeDelay?.Name != "Silk Drain Time" || !NeedolinHasHalf())
            return;
        NeedolinUsingHalf = true;
        needolinFullTime = __instance.TimeDelay.Value;
        __instance.TimeDelay.Value *= 0.5f;

        SilkSpool spool = SilkSpool.Instance;
        if (spool.wasUsingChunk)
        {
            spool.wasUsingChunk.PlayIdle();
            spool.wasUsingChunk = null;
        }
        spool.RefreshSilk(default, default);
        RefreshHalfSilkColor();
    }

    [HarmonyPatch(typeof(TimeLimitSetV2), nameof(TimeLimitSetV2.OnEnter))]
    [HarmonyPostfix]
    private static void NeedolinRestoreDrainTime(TimeLimitSetV2 __instance)
    {
        if (needolinFullTime is float full)
        {
            __instance.TimeDelay.Value = full;
            needolinFullTime = null;
        }
    }

    //Silkspeed Anklet
    private static PlayMakerFSM? ankletFsm;
    private static FsmFloat? ankletTimer;

    private static FsmFloat? AnkletTimer(HeroController hc)
    {
        if (ankletFsm && ankletTimer != null)
            return ankletTimer;
        foreach (PlayMakerFSM fsm in hc.GetComponentsInChildren<PlayMakerFSM>(true))
        {
            FsmFloat? found = fsm.FsmVariables.FindFsmFloat("Use Silk Timer");
            if (found != null)
            {
                ankletFsm = fsm;
                ankletTimer = found;
                return found;
            }
        }
        return null;
    }

    private static bool ankletLook;

    [HarmonyPatch(typeof(HeroController), "Update")]
    [HarmonyPostfix]
    private static void AnkletHalfLook(HeroController __instance)
    {
        if (HeroController.instance != __instance)
            return;
        SilkSpool spool = SilkSpool.Instance;
        if (!spool)
            return;

        FsmFloat? timer = AnkletTimer(__instance);
        bool on = YenCrest.IsEquipped && GetHasHalfSilk() && timer != null
            && ankletFsm!.ActiveStateName == "Usage";

        if (on)
            timer!.Value += Time.deltaTime;

        if (on == ankletLook)
            return;
        ankletLook = on;

        NeedolinUsingHalf = on;
        if (spool.wasUsingChunk)
        {
            spool.wasUsingChunk.PlayIdle();
            spool.wasUsingChunk = null;
        }
        spool.RefreshSilk(default, default);
        RefreshHalfSilkColor();
    }
}
//Spa Interaction
[HarmonyPatch]
internal static class SpaHalfSilk {
	private static readonly List<PlayMakerFSM> spas = [];
	private static string? spasScene;

	private static bool InSpa() {
		string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
		if (scene != spasScene) {
			spasScene = scene;
			spas.Clear();
			foreach (var fsm in UnityEngine.Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
				if (fsm.FsmVariables.FindFsmGameObject("Spa Ripple") != null)
					spas.Add(fsm);
		}
		foreach (var spa in spas)
			if (spa && spa.ActiveStateName is "Stay" or "Heal Pause" or "Healing")
				return true;
		return false;
	}

	[HarmonyPatch(typeof(HeroController), nameof(HeroController.AddSilk), [typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool)])]
	[HarmonyPostfix]
	private static void CompleteHalfInSpa(int __0) {
		if (__0 > 0 && YenCrest.IsEquipped && HalfSilk.GetHasHalfSilk() && InSpa())
			HalfSilk.RemoveHalfSilk(interruptRegen: false);
	}
}

// Wraiths and claw maidens can now drain your last half silk :P
[HarmonyPatch]
internal static class EnemySilkDrain {
	private static readonly string[] Drainers = ["Wraith", "Claw"];

	private static bool IsDrainer(FsmStateAction action) {
		GameObject owner = action.Fsm.GameObject;
		if (!owner || !YenCrest.IsEquipped || !HalfSilk.GetHasHalfSilk())
			return false;
		string name = owner.transform.root.name + "/" + owner.name;
		return System.Array.Exists(Drainers, d => name.IndexOf(d, System.StringComparison.OrdinalIgnoreCase) >= 0);
	}

	private static bool HandleLastHalf(FsmStateAction action, bool take) {
		if (PlayerData.instance.silk > 0 || !IsDrainer(action))
			return true;
		if (take)
			HalfSilk.RemoveHalfSilk();
		action.Finish();
		return false;
	}

	[HarmonyPatch(typeof(CallMethodProper), nameof(CallMethodProper.OnEnter))]
	[HarmonyPrefix]
	private static bool WraithTake(CallMethodProper __instance) =>
		__instance.methodName?.Value != "TakeSilk" || HandleLastHalf(__instance, take: true);

	[HarmonyPatch(typeof(TakeSilk), nameof(TakeSilk.OnEnter))]
	[HarmonyPrefix]
	private static bool ClawTake(TakeSilk __instance) => HandleLastHalf(__instance, take: true);

	[HarmonyPatch(typeof(PlayerdataIntCompare), nameof(PlayerdataIntCompare.OnEnter))]
	[HarmonyPrefix]
	private static bool ClawCheck(PlayerdataIntCompare __instance) =>
		__instance.playerdataInt?.Value != "silk" || HandleLastHalf(__instance, take: false);

	[HarmonyPatch(typeof(GetPlayerDataInt), nameof(GetPlayerDataInt.OnEnter))]
	[HarmonyPostfix]
	private static void WraithCheck(GetPlayerDataInt __instance) {
		if (__instance.intName?.Value == "silk" && IsDrainer(__instance))
			__instance.storeValue.Value += 1;
	}
}
