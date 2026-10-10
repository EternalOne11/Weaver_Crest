using GlobalSettings;
using HutongGames.PlayMaker;
using Silksong.UnityHelper.Util;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static partial class Moveset {

	internal static tk2dSpriteCollectionData BindCollection { get; private set; } = null!;
	internal static tk2dSpriteAnimation BindLibrary { get; private set; } = null!;
	internal static tk2dSpriteAnimationClip BindClip { get; private set; } = null!;
	internal static tk2dSpriteAnimationClip QuickBindClip { get; private set; } = null!;

	private static void BindAnims() {
		string[] bindFiles = ["bindSilk_0000.png", "bindSilk_0001.png", "bindSilk_0002.png", "bindSilk_0003.png",
			"bindSilk_0004.png", "bindSilk_0005.png", "bindSilk_0006.png", "bindSilk_0007.png", "bindSilk_0008.png",
			"bindSilk_0009.png", "bindSilk_0010.png", "bindSilk_0011.png", "bindSilk_0012.png", "bindSilk_0013.png",
			"bindSilk_0014.png", "bindSilk_0015.png", "bindSilk_0016.png", "bindSilk_0017.png", "bindSilk_0018.png",
			"bindSilk_0019.png", "bindSilk_0020.png"];
		string[] quickBindFiles = ["bindSilkQ_0000.png", "bindSilkQ_0001.png", "bindSilkQ_0002.png", "bindSilkQ_0003.png",
			"bindSilkQ_0004.png", "bindSilkQ_0005.png", "bindSilkQ_0006.png", "bindSilkQ_0007.png", "bindSilkQ_0008.png",
			"bindSilkQ_0009.png", "bindSilkQ_0010.png", "bindSilkQ_0011.png", "bindSilkQ_0012.png", "bindSilkQ_0013.png"];

		Texture2D[] bindTex = LoadNamedTextures(bindFiles);
		Texture2D[] quickBindTex = LoadNamedTextures(quickBindFiles);
		Texture2D[] all = [.. bindTex, .. quickBindTex];

		BindCollection = Tk2dUtil.CreateTk2dSpriteCollection(
			sprites: all,
			spriteCenters: [.. all.Select(t => new Vector2(t.width, t.height) * 0.5f)],
			pixelsPerUnit: 64f
		);
		Object.DontDestroyOnLoad(BindCollection.gameObject);
		BindCollection.gameObject.name = $"{YenId}_BindAnim";

		//Animation speeds
		BindClip = new tk2dSpriteAnimationClip {
			name = "Weaver Bind",
			fps = 12,
			wrapMode = tk2dSpriteAnimationClip.WrapMode.Once,
			frames = BindCollection.CreateFrames(bindTex.Select(t => t.name)),
		};
		QuickBindClip = new tk2dSpriteAnimationClip {
			name = "Weaver Quick Bind",
			fps = 12,
			wrapMode = tk2dSpriteAnimationClip.WrapMode.Once,
			frames = BindCollection.CreateFrames(quickBindTex.Select(t => t.name)),
		};

		BindLibrary = BindCollection.gameObject.AddComponent<tk2dSpriteAnimation>();
		BindLibrary.clips = [BindClip, QuickBindClip];
	}
}

internal static class Bind
{
	//bind animation length
	private const float BindCycle = 0.81f;
	private const float QuickCycle = 0.54f;

    private static int cachedBindCount;
    private static bool cachedQuick;

    internal static void TripleBind(FsmInt amountHealed, FsmInt numberOfBinds, FsmFloat secondsPerBind, PlayMakerFSM bindFsm)
    {
        //Bind amount values
        amountHealed.Value = 1;
        numberOfBinds.Value = Gameplay.MultibindTool.IsEquipped ? 4 : 3;
        secondsPerBind.Value = 0.65f;

        cachedBindCount = numberOfBinds.Value;
        cachedQuick = ToolItemManager.IsToolEquipped("Quickbind");

        HeroController.instance.StartCoroutine(PlayBindEffect(bindFsm));
    }

	private static readonly Dictionary<ParticleSystem, (float size, float speed, float radius, Vector3 shapeScale)> particleDefaults = [];

    private static GameObject? effectObj;
    private static tk2dSpriteAnimator? effectAnim;
    private static bool effectPlaying;
    private static int effectRun;

	private static IEnumerator PlayBindEffect(PlayMakerFSM bindFsm)
    {
        if (effectPlaying)
            yield break;
        effectPlaying = true;
        int run = ++effectRun;

        if (!effectObj) {
            effectObj = new GameObject($"{YenId} Bind Effect");
            effectObj.transform.SetParent(HeroController.instance.transform, false);
            effectObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            tk2dBaseSprite.AddComponent<tk2dSprite>(effectObj, Moveset.BindCollection, 0);
            effectAnim = effectObj.AddComponent<tk2dSpriteAnimator>();
            effectAnim.Library = Moveset.BindLibrary;
        }

        Transform? defaultEffect = HeroController.instance.transform.Find("Bind Effects");
        tk2dSpriteAnimationClip clip = cachedQuick ? Moveset.QuickBindClip : Moveset.BindClip;
        float fps = clip.frames.Length / (cachedQuick ? QuickCycle : BindCycle);
        effectObj!.SetActive(true);
        ScaleParticles(defaultEffect, true);

        string lastState = "";
        int played = 0;
        while (true)
        {
            string state = bindFsm.Fsm.ActiveStateName;
            if (state == "Cooldown" || state == "Idle")
                break;

            if (state != lastState && state.StartsWith("Bind ") && state != "Bind Burst" && played < cachedBindCount) {
                effectAnim!.Stop();
                effectAnim.Play(clip, 0f, fps);
                played++;
            }
            lastState = state;
            SetEffect(defaultEffect, true);
            yield return null;
        }
        effectObj.SetActive(false);
        effectPlaying = false;
        for (float t = 0f; t < 1f && run == effectRun && defaultEffect != null
            && defaultEffect.Cast<Transform>().Any(c => c.gameObject.activeInHierarchy); t += Time.deltaTime) {
            SetEffect(defaultEffect, true);
            yield return null;
        }
        if (run == effectRun) {
            SetEffect(defaultEffect, false);
            ScaleParticles(defaultEffect, false);
        }
    }

    private static void SetEffect(Transform? defaultEffect, bool hide)
    {
        if (defaultEffect == null)
            return;
        foreach (var r in defaultEffect.GetComponentsInChildren<Renderer>(true))
            if (r is not ParticleSystemRenderer)
                r.enabled = !hide;
    }

	//particle spread and size
    private static void ScaleParticles(Transform? defaultEffect, bool shrink)
    {
        if (defaultEffect == null)
            return;
        foreach (var ps in defaultEffect.GetComponentsInChildren<ParticleSystem>(true)) {
            var main = ps.main;
            var shape = ps.shape;
            if (!particleDefaults.ContainsKey(ps))
                particleDefaults[ps] = (main.startSizeMultiplier, main.startSpeedMultiplier, shape.radius, shape.scale);
            var d = particleDefaults[ps];

            float size = shrink ? 0.7f : 1f;
            float spread = shrink ? 0.7f : 1f;
            main.startSizeMultiplier = d.size * size;
            main.startSpeedMultiplier = d.speed * spread;
            shape.radius = d.radius * spread;
            shape.scale = d.shapeScale * spread;
        }
    }
}
