using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
            Log.LogInfo($"RemoveHalfSilk - Already has half silk");
            HasHalfSilk = false;
            DespawnHalfSilk();
            SilkSpool.Instance.EvaluatePositions();
        }
        else if (PlayerData.instance.silk > 0)
        {
            Log.LogInfo($"RemoveHalfSilk - Doesn't have half silk");
            HasHalfSilk = true;
            TransformLastSilk();
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
    private static tk2dSpriteCollectionData SpriteCollection = null!;
    private static bool AddedAnimations = false;

    private static SilkChunk CurrentHalf = null!;
    private static bool HasHalfSilk = false;
    private static SilkSpool.SilkUsingFlags HalfSilkUsingFlags = SilkSpool.SilkUsingFlags.None;

    private static void TryAddAnims()
    {
        if (AddedAnimations)
            return;
        
        CurrentHalf.animator.Library.clips = CurrentHalf.animator.Library.clips.Append(HalfSilkIdle).Append(HalfSilkAppear).Append(HalfSilkDisappear).Append(HalfSilkGrow).Append(HalfSilkShrink).ToArray();
        CurrentHalf.animator.Library.isValid = false;
        AddedAnimations = true;
    }

    private static void SpawnHalfSilk()
    {
        Log.LogInfo($"SpawnHalfSilk");
        if (CurrentHalf)
            CurrentHalf.gameObject.Recycle();
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

    private static void TransformLastSilk()
    {
        Log.LogInfo($"TransformLastSilk");
        if (CurrentHalf)
            CurrentHalf.gameObject.Recycle();
        CurrentHalf = SilkSpool.Instance.silkChunks.Last(s => !s.IsRegen);
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
                SilkSpool.Instance.wasUsingChunk = SilkSpool.Instance.silkChunks.Last(s => !s.IsRegen);
                Log.LogInfo("TransformLastSilk wasUsingChunk: " + SilkSpool.Instance.wasUsingChunk);
                if (SilkSpool.Instance.wasUsingChunk)
                    SilkSpool.Instance.wasUsingChunk.SetUsing(silkUsingFlags);
            }
        }
    }

    private static void UpgradeHalfSilk()
    {
        if (!CurrentHalf)
            return;
        Log.LogInfo($"UpgradeHalfSilk");
        CurrentHalf.FinishHalfSilk();
        SilkSpool.Instance.silkChunks.Add(CurrentHalf);
        CurrentHalf.animator.AnimationCompletedEvent += UpgradeHalfSilkAnimComplete;
        CurrentHalf.animator.AnimationChanged += UpgradeHalfSilkAnimChanged;
        CurrentHalf.SetAnims();
        CurrentHalf.upAnim = HalfSilkGrow.name;

        if (SilkSpool.Instance.silkChunks.Count(s => !s.IsRegen) == SilkSpool.BindCost)
            CurrentHalf.StartGlow();

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

    private static void DespawnHalfSilk()
    {
        Log.LogInfo($"DespawnLastSilk");
        if (!CurrentHalf)
            return;
        //if (CurrentHalf)
        //    CurrentHalf.gameObject.Recycle();
        CurrentHalf.FinishHalfSilk(false);
        CurrentHalf.removeCounter = HalfSilkDisappear.Duration;
        Log.LogInfo("RemoveCounter: " + CurrentHalf.removeCounter);
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
        Log.LogInfo($"InjectSilkSpoolChangeSilk");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(typeof(SilkSpool), nameof(SilkSpool.silkChunks)),
            x => x.MatchCallvirt(typeof(List<SilkChunk>), "get_Count")))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 1");
        }

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdarg(2),
            x => x.MatchLdcI4(0),
            x => x.MatchBle(out ILLabel _)))
        {
            cursor.EmitDelegate(ResetHalfSilkUsing);
            Log.LogInfo($"Injected 2");
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
        Log.LogInfo($"InjectSilkSpoolRefreshSilk");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdloc(1),
            x => x.MatchBrtrue(out ILLabel _)))
        {
            cursor.Index++;
            cursor.EmitDelegate(HalfSilkSetUsing);
            Log.LogInfo($"Injected");
        }
    }

    private static SilkSpool.SilkUsingFlags HalfSilkSetUsing(SilkSpool.SilkUsingFlags usingFlags)
    {
        if (!GetHasHalfSilk())
            return usingFlags;
        
        HalfSilkUsingFlags = usingFlags;

        Log.LogInfo($"SilkSpool.Instance.wasUsingChunk: {SilkSpool.Instance.wasUsingChunk}");
        //SilkSpool.Instance.wasUsingChunk = null;

        SilkSpool.SilkUsingFlags ret = SilkSpool.SilkUsingFlags.None;
        
        // if using a full silk, pass that to the next silk. I don't know what the silk drain effect is, so I'm passing it to the next silk too
        if (usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Normal) || usingFlags.HasFlag(SilkSpool.SilkUsingFlags.Drain))
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
        //Log.LogInfo($"AfterSilkChunkUpdate");
        
        if (GetHasHalfSilk() && __instance == CurrentHalf)
        {
            if (HeroController.instance.cState.isMaggoted && !__instance.isMaggoted)
            {
                __instance.isMaggoted = true;
                __instance.FadeToCorrectColor();
            }
            else if (__instance.isMaggoted)
            {
                __instance.isMaggoted = false;
                __instance.FadeToCorrectColor();
            }
        }
    }

    [HarmonyPatch(typeof(SilkChunk), nameof(SilkChunk.FadeToCorrectColor))]
    [HarmonyILManipulator]
    private static void InjectSilkColor(ILContext il)
    {
        Log.LogInfo($"InjectSilkColor");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(SilkChunk), nameof(SilkChunk.FadeToColor))))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(SetHalfSilkColor);
            Log.LogInfo($"Injected");
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
        Log.LogInfo($"InjectSilkAnimPrevent1");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(SilkChunk), nameof(SilkChunk.FadeToColor))))
        {
            cursor.EmitDelegate(SilkChunkIdleFade);
            cursor.Remove();
            Log.LogInfo($"Injected 2");
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
        Log.LogInfo($"InjectSilkAnimPrevent");

        while (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCallvirt(typeof(tk2dSpriteAnimator), nameof(tk2dSpriteAnimator.Play))))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(SilkChunkPlayAnim);
            cursor.Remove();
            Log.LogInfo($"Injected");
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
        Log.LogInfo($"InjectAcidUpdate");
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 1");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 2");
        }
    }

    [HarmonyPatch(typeof(HeroSilkAcid), nameof(HeroSilkAcid.StartSizzle))]
    [HarmonyILManipulator]
    private static void InjectAcidStartSizzle(ILContext il, MethodBase original)
    {
        Log.LogInfo($"InjectAcidStartSizzle");
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 1");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 2");
        }
    }

    [HarmonyPatch(typeof(HeroSilkAcid), nameof(HeroSilkAcid.TakeSilk))]
    [HarmonyILManipulator]
    private static void InjectAcidTakeSilk(ILContext il, MethodBase original)
    {
        Log.LogInfo($"InjectAcidTakeSilk");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdcI4(1),
            x => x.MatchCallvirt(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeSilkOrHalf);
            cursor.RemoveRange(2);
            Log.LogInfo($"Injected 1");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 2");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 3");
        }
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.TickSilkEat))]
    [HarmonyILManipulator]
    private static void InjectHeroMaggot1(ILContext il, MethodBase original)
    {
        Log.LogInfo($"InjectHeroMaggot1");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchCall(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeMaggotSilk);
            cursor.Remove();
            Log.LogInfo($"Injected 1");
        }

        if (cursor.TryGotoNext(MoveType.Before,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk)),
            x => x.MatchLdcI4(0),
            x => x.MatchBgt(out ILLabel _)))
        {
            cursor.Index++;
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 2");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 3");
        }
    }

    private static void TakeMaggotSilk(HeroController hc, int _, SilkSpool.SilkTakeSource takeSource)
    {
        if (GetHasHalfSilk())
            RemoveHalfSilk();
        else
            hc.TakeSilk(1, takeSource);
    }

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.Update))]
    [HarmonyILManipulator]
    private static void InjectHeroMaggot2(ILContext il, MethodBase original)
    {
        Log.LogInfo($"InjectHeroMaggot2");
        ILCursor cursor = new ILCursor(il);

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdfld(typeof(PlayerData), nameof(PlayerData.silk))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 1");
        }

        cursor.Index = 0;

        while (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdstr("silk"),
            x => x.MatchCallvirt(typeof(PlayerData), nameof(PlayerData.GetInt))))
        {
            cursor.EmitDelegate(IncreaseSilkIfHalfSilk);
            Log.LogInfo($"Injected 2");
        }
    }

    private static int IncreaseSilkIfHalfSilk(int orig) => GetHasHalfSilk() ? orig + 1 : orig;

    [HarmonyPatch("MaggotRegion+<TakeSilk>d__35, Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", "MoveNext")]
    [HarmonyILManipulator]
    private static void InjectMaggot(ILContext il, MethodBase original)
    {
        Log.LogInfo($"InjectMaggot");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdcI4(1),
            x => x.MatchCallvirt(typeof(HeroController), nameof(HeroController.TakeSilk))))
        {
            cursor.EmitDelegate(TakeSilkOrHalf);
            cursor.RemoveRange(2);
            Log.LogInfo($"Injected");
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
        Log.LogInfo($"InjectPosition");
        ILCursor cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.AfterLabel,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld(typeof(SilkSpool), nameof(SilkSpool.mossChunks))))
        {
            cursor.Emit(OpCodes.Ldloc_0);
            cursor.EmitDelegate(SetHalfSilkPosition);
            cursor.Emit(OpCodes.Stloc_0);
            Log.LogInfo($"Injected");
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

    [HarmonyPatch(typeof(HeroController), nameof(HeroController.AddSilk), [typeof(int), typeof(bool), typeof(SilkSpool.SilkAddSource), typeof(bool)])]
    [HarmonyPostfix]
    private static void AfterAddSilk(HeroController __instance)
    {
        Log.LogInfo($"After Add Silk");
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
}
