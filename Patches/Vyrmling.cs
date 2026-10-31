using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Silksong.AssetHelper.ManagedAssets;
using Silksong.UnityHelper.Util;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using static Weaver_Crest.Utils.ResourceUtils;
using static Weaver_Crest.Weaver_CrestPlugin;

#pragma warning disable HARMONIZE004, Harmony003

namespace Weaver_Crest.Patches;

// The Noodlevyrm a simple enemy, that has 1 attack and moves up and down.
// It has a hunter journal entry.
[HarmonyPatch]
internal static class Vyrmling
{
    //Assethelper stuff
    private const string OrbScene = "Coral_29";
    private const string OrbObjectPath = "Boss Scene/Zap Clusters/Cluster 1/Mega Jelly Zap";

    private static ManagedAsset<GameObject>? orbAsset;
    private static GameObject? orbSource; // the loaded orb, kept for the session
    private static bool orbLoading;
    private static VyrmlingCoroutineRunner? coroutineRunner;
    private static bool initialized;

    //Rooms, spawn positions and rotation
    private static readonly (string Scene, Vector2 Position, float Rotation, bool RequiresZapCoreDefeated)[] Spawns =
    [
        // Always present.
        ("Song_10", new Vector2(25.6f, 52.5f), 0f, false),
        ("Arborium_07", new Vector2(154.5f, 10.5f), 180f, false),
		//after voltvyrm death
        ("Coral_29", new Vector2(172f, 32.8f), 0f, true),
    ];

    // The many sounds of the enemy, either through imported .wav or using Assethelper.
    private const string WiggleSoundFile = "Flukeman_aggressive_loop.wav";
    private const string OutSoundFile = "sfxstatic_assets_areacoral|Assets/Audio/SFX/Silksong Deliveries/2023-02 v2 Coral zap stuff coral tree hornet lands peak creature ui profiles/coral_zap_boss_emerge.ogg";
    private const string InSoundFile = "sfxstatic_assets_areacoral|Assets/Audio/SFX/Silksong Deliveries/2023-02 v2 Coral zap stuff coral tree hornet lands peak creature ui profiles/coral_zap_boss_dissapear.wav";
    private const string AttackSoundFile = "fluke_fairy_call.wav";
    private const string DeathSoundFile = "sfxstatic_assets_shared|Assets/audio/SFX/Enemy/Hornet/boss_death_pt_1.wav";
    private const string IdleSoundFile = "Fluke_Mother_rest_idle.wav";
    internal const float WiggleVolume = 0.6f; // 1 is full volume
    internal const float IdleHearRange = 12f; // distance you can hear the idle sounds, when its hidden

    //stats
    internal const int MaxHp = 1;
    internal const float TriggerRange = 6f; // How close hornet must be for it to come out
    internal const float RetreatRange = 8f; // Hornet far away until it goes back in.
    internal static readonly Vector2 AttackDelay = new(1.25f, 2.5f); //length of time between attacks
    internal const float AttackMaxHold = 1.5f; // longest it waits for the attack sound to end before wiggling again
    internal static readonly Vector2 OrbOffset = new(0f, 0.9f); //Attack orbs location relative to the Noodlevyrm
    internal const float OrbLifetime = 4f;
    internal const float OrbScale = 1.25f; // the orb's size, relative to the Voltvyrm's
    internal const float WiggleFps = 10f;
    internal const float OutFps = 13f;
    internal const float AttackFps = 8f;
    internal const float DeathFps = 10f;

    //Hitbox
    internal static readonly Vector2 HitboxSize = new(1.2f, 2.2f);
    internal static readonly Vector2 HitboxOffset = new(0f, 1.1f);

    // Hit tint... (barely noticed at 1 hp)
    internal static readonly Color HitTint = new(1f, 0.55f, 0.55f, 1f);
    internal const float HitTintTime = 0.12f;

    //Journal stuff
    private static readonly LocalisedString
        JournalName = new($"Mods.{Id}", "VYRMLING_NAME"),
        JournalDesc = new($"Mods.{Id}", "VYRMLING_DESC"),
        JournalNotes = new($"Mods.{Id}", "VYRMLING_NOTES");

    private const int KillsRequired = 3;
    private const string PlaceAfter = "Voltvyrm";
    private static EnemyJournalRecord? record;

    internal static EnemyJournalRecord Record
    {
        get
        {
            if (record)
                return record!;

            Sprite icon = LoadEmbeddedPngAsSprite("Journal.png");
            record = ScriptableObject.CreateInstance<EnemyJournalRecord>();
            record.name = "Weaver Noodlevyrm";
            record.displayName = JournalName;
            record.description = JournalDesc;
            record.notes = JournalNotes;
            record.iconSprite = icon;
            record.enemySprite = icon;
            record.killsRequired = KillsRequired;
            record.recordType = EnemyJournalRecord.RecordTypes.Enemy;
            record.isAlwaysUnlocked = false;
            record.requiredType = EnemyJournalRecord.RequiredTypes.NotRequired;
            record.completeOthers = [];
            UnityEngine.Object.DontDestroyOnLoad(record);
            return record;
        }
    }

    [HarmonyPatch(typeof(EnemyJournalManager), "Awake")]
    [HarmonyPostfix]
    private static void AddToJournal(EnemyJournalRecordList ___recordList)
    {
        if (!___recordList || ___recordList.Contains(Record))
            return;

        int after = -1;
        for (int i = 0; i < ___recordList.Count; i++)
        {
            EnemyJournalRecord other = ___recordList[i];
            if (other && (Matches(other.name) || Matches(other.DisplayName.ToString())))
            {
                after = i;
                break;
            }
        }

        if (after >= 0)
            ___recordList.Insert(after + 1, Record);
        else
            ___recordList.Add(Record);
    }

    private static bool Matches(string? text) =>
        text != null && text.IndexOf(PlaceAfter, StringComparison.OrdinalIgnoreCase) >= 0;

    internal static void RecordKill() => EnemyJournalManager.RecordKill(Record);

    // Assets and sounds
    internal static tk2dSpriteCollectionData Collection = null!;
    internal static tk2dSpriteAnimation Library = null!;
    internal static Sprite? CorpseSprite;

    internal static SoundRef WiggleSound = null!;
    internal static SoundRef OutSound = null!;
    internal static SoundRef InSound = null!;
    internal static SoundRef AttackSound = null!;
    internal static SoundRef DeathSound = null!;
    internal static SoundRef IdleSound = null!;

    internal sealed class SoundRef
    {
        private readonly AudioClip? embedded;
        private readonly ManagedAsset<AudioClip>? asset;
        private bool loading;

        internal SoundRef(string setting)
        {
            int split = setting.IndexOf('|');
            if (split >= 0)
            {
                asset = ManagedAsset<AudioClip>.FromNonSceneAsset(setting.Substring(split + 1), setting.Substring(0, split));
                return;
            }

            embedded = Sounds.LoadClip(setting);
            if (!embedded)
                Log.LogWarning($"Noodlevyrm: embedded sound '{setting}' not found (or not 16-bit)");
        }

        internal IEnumerator Load()
        {
            if (asset == null || loading || Loaded)
                yield break;

            loading = true;
            yield return asset.Load();
            loading = false;

            if (asset.Handle.OperationException != null)
                Log.LogError($"Noodlevyrm failed to load a sound using AssetHelper: {asset.Handle.OperationException}");
        }

        private bool Loaded => asset != null && asset.Handle.IsValid() && asset.Handle.Status == AsyncOperationStatus.Succeeded;

        internal AudioClip? Clip => embedded ? embedded : Loaded ? asset!.Handle.Result : null;
    }

    private static IEnumerable<SoundRef> AllSounds() => [WiggleSound, OutSound, InSound, AttackSound, DeathSound, IdleSound];

    internal static void Initialize()
    {
        if (initialized)
            return;


        string[] wiggle = [.. Enumerable.Range(0, 5).Select(i => $"Wiggle_{i:0000}.png")];
        string[] outFrames = [.. Enumerable.Range(0, 3).Select(i => $"Out_{i:0000}.png")];
        string[] attack = [.. Enumerable.Range(0, 4).Select(i => $"Attack_{i:0000}.png")];
        string[] death = [.. Enumerable.Range(0, 5).Select(i => $"Death_{i:0000}.png")];

        Texture2D[] textures = [.. wiggle.Concat(outFrames).Concat(attack).Concat(death).Select(Load)];

        Collection = Tk2dUtil.CreateTk2dSpriteCollection(
            sprites: textures,
            spriteCenters: [.. textures.Select(t => new Vector2(t.width * 0.5f, t.height))],
            pixelsPerUnit: 64f);

        UnityEngine.Object.DontDestroyOnLoad(Collection.gameObject);
        Collection.gameObject.name = $"{YenId}_NoodlevyrmAnim";

        Library = Collection.gameObject.AddComponent<tk2dSpriteAnimation>();
        Library.clips =
        [
            Clip("Wiggle", wiggle, WiggleFps, tk2dSpriteAnimationClip.WrapMode.Loop),
            Clip("Out", outFrames, OutFps, tk2dSpriteAnimationClip.WrapMode.Once),
            Clip("In", [.. System.Linq.Enumerable.Reverse(outFrames)], OutFps, tk2dSpriteAnimationClip.WrapMode.Once),
            Clip("Attack", attack, AttackFps, tk2dSpriteAnimationClip.WrapMode.Once),
            Clip("Death", death, DeathFps, tk2dSpriteAnimationClip.WrapMode.Once),
        ];
        Library.ValidateLookup();

        Texture2D corpseTex = Load("Corpse.png");
        CorpseSprite = Sprite.Create(
            corpseTex,
            new Rect(0f, 0f, corpseTex.width, corpseTex.height),
            new Vector2(0.5f, 0f),
            64f);

        // AssetHelper requires these be made in awake.
        orbAsset = ManagedAsset<GameObject>.FromSceneAsset(OrbScene, OrbObjectPath);
        WiggleSound = new(WiggleSoundFile);
        OutSound = new(OutSoundFile);
        InSound = new(InSoundFile);
        AttackSound = new(AttackSoundFile);
        DeathSound = new(DeathSoundFile);
        IdleSound = new(IdleSoundFile);

        GameObject runnerObject = new("Noodlevyrm Coroutine Runner");
        UnityEngine.Object.DontDestroyOnLoad(runnerObject);
        coroutineRunner = runnerObject.AddComponent<VyrmlingCoroutineRunner>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        initialized = true;
    }

    private static Texture2D Load(string file)
    {
        Texture2D texture = LoadEmbeddedPngAsTexture2D(file);
        texture.name = file;
        return texture;
    }

    private static tk2dSpriteAnimationClip Clip(
        string name,
        string[] frames,
        float fps,
        tk2dSpriteAnimationClip.WrapMode wrap) =>
        new()
        {
            name = name,
            fps = fps,
            wrapMode = wrap,
            frames = Collection.CreateFrames(frames)
        };


    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var sceneSpawns = Spawns.Where(spawn =>
            string.Equals(spawn.Scene, scene.name, StringComparison.OrdinalIgnoreCase) &&
            (!spawn.RequiresZapCoreDefeated ||
             (PlayerData.instance != null && PlayerData.instance.defeatedZapCoreEnemy)))
            .ToArray();

        if (sceneSpawns.Length == 0)
            return;

        if (!coroutineRunner)
        {
            return;
        }

        foreach (SoundRef sound in AllSounds())
            coroutineRunner!.StartCoroutine(sound.Load());

        if (!orbSource && !orbLoading)
            coroutineRunner!.StartCoroutine(LoadOrb());

        foreach (var spawn in sceneSpawns)
            Spawn(scene, spawn.Position, spawn.Rotation);
    }

    private static void Spawn(Scene scene, Vector2 position, float visualRotation)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject body = new("Noodlevyrm");
        body.SetActive(false);
        SceneManager.MoveGameObjectToScene(body, scene);

        try
        {
            int enemyLayer = LayerMask.NameToLayer("Enemies");
            if (enemyLayer < 0)
                enemyLayer = 11;
            body.layer = enemyLayer;

            body.transform.position = new Vector3(position.x, position.y, 0.004f);
            body.transform.rotation = Quaternion.identity;
            body.transform.localScale = Vector3.one;

            Rigidbody2D rb = body.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.simulated = true;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // The hitbox turns with the art
            Quaternion turn = Quaternion.Euler(0f, 0f, visualRotation);
            bool sideways = Mathf.Abs(Mathf.DeltaAngle(visualRotation, 90f)) < 45f
                || Mathf.Abs(Mathf.DeltaAngle(visualRotation, 270f)) < 45f;
            BoxCollider2D rootCollider = body.AddComponent<BoxCollider2D>();
            rootCollider.isTrigger = false;
            rootCollider.size = sideways ? new Vector2(HitboxSize.y, HitboxSize.x) : HitboxSize;
            rootCollider.offset = turn * HitboxOffset;

            HealthManager health = body.AddComponent<HealthManager>();
            health.hp = HeldHp; //true hp
            InitializeHealthManagerDefaults(health);

            NoodlevyrmBody behaviour = body.AddComponent<NoodlevyrmBody>();
            behaviour.SetVisualRotation(visualRotation);
            body.SetActive(true);
        }
        catch (Exception e)
        {
            if (body)
                UnityEngine.Object.Destroy(body);
            Log.LogError($"Noodlevyrm failed spawn in {scene.name}: {e}");
        }
    }

    private static void InitializeHealthManagerDefaults(HealthManager health)
    {
        health.itemDropGroups ??= new();
        health.damageScaling ??= new()
        {
            Level1Mult = 1f,
            Level2Mult = 1f,
            Level3Mult = 1f,
            Level4Mult = 1f,
            Level5Mult = 1f,
        };
    }

    private const int HeldHp = 9999;

    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    [HarmonyPrefix]
    private static void HoldHp(HealthManager __instance)
    {
        if (__instance.GetComponent<NoodlevyrmBody>())
            __instance.hp = HeldHp;
    }

    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    [HarmonyPostfix]
    private static void PassDamage(HealthManager __instance)
    {
        NoodlevyrmBody? body = __instance.GetComponent<NoodlevyrmBody>();
        if (!body)
            return;

        int taken = HeldHp - __instance.hp;
        __instance.hp = HeldHp;
        body!.TakeHit(taken);
    }

    // Zap orb attach stuff...
    internal static void Strike(Vector2 at)
    {
        if (!coroutineRunner)
        {
            return;
        }
        coroutineRunner!.StartCoroutine(SpawnOrbRoutine(at));
    }

    private static IEnumerator LoadOrb()
    {
        if (orbAsset == null)
        {
            yield break;
        }

        orbLoading = true;
        yield return orbAsset.Load();
        orbLoading = false;

        if (orbAsset.Handle.OperationException != null)
        {
            Log.LogError($"Noodlevyrm failed to load Mega Jelly Zap using AssetHelper: {orbAsset.Handle.OperationException}");
            yield break;
        }

        orbSource = orbAsset.Handle.Result;
        if (!orbSource)
            Log.LogError("AssetHelper loaded Mega Jelly Zap, but its GameObject result was null.");
    }

    private static IEnumerator SpawnOrbRoutine(Vector2 at)
    {
        // Normally already loaded (it starts when the room loads); otherwise it's loaded (or finished loading) first
        if (!orbSource)
        {
            if (!orbLoading)
                yield return LoadOrb();
            while (orbLoading)
                yield return null;
            if (!orbSource)
                yield break;
        }
        GameObject source = orbSource!;

        GameObject holder = new("Noodlevyrm Orb Holder");
        holder.SetActive(false);
        Scene targetScene = SceneManager.GetActiveScene();
        if (targetScene.IsValid() && targetScene.isLoaded)
            SceneManager.MoveGameObjectToScene(holder, targetScene);

        GameObject? orbObject = null;
        try
        {
            orbObject = UnityEngine.Object.Instantiate(source, holder.transform, false);
            orbObject.name = "Noodlevyrm Zap Orb";
            orbObject.SetActive(false);
            orbObject.transform.SetParent(null, true);
            if (targetScene.IsValid() && targetScene.isLoaded)
                SceneManager.MoveGameObjectToScene(orbObject, targetScene);
            UnityEngine.Object.Destroy(holder);

            MegaJellyZap? zap = orbObject.GetComponent<MegaJellyZap>() ??
                orbObject.GetComponentInChildren<MegaJellyZap>(true);
            if (!zap)
            {
                UnityEngine.Object.Destroy(orbObject);
                yield break;
            }

            zap!.delay = 0f;
            zap.yDelay = false;
            zap.shift = 0f;
            orbObject.transform.position = new Vector3(at.x, at.y, -0.01f);
            orbObject.transform.localScale *= OrbScale;

            orbObject.SetActive(true);
            UnityEngine.Object.Destroy(orbObject, OrbLifetime);
        }
        catch (Exception e)
        {
            if (orbObject)
                UnityEngine.Object.Destroy(orbObject);
            if (holder)
                UnityEngine.Object.Destroy(holder);
            Log.LogError($"Noodlevyrm failed to create its zap orb: {e}");
        }
    }
}

internal sealed class VyrmlingCoroutineRunner : MonoBehaviour { }

// The Noodlevyrm's behaviour, based upon hornets distance.
internal sealed class NoodlevyrmBody : MonoBehaviour
{
    private enum State { Hidden, Out, Wiggle, Attack, In, Dead }

    private State state;
    private float attackTimer;
    private float attackHoldLeft;
    private bool attackOrbSpawned;
    private int hp = Vyrmling.MaxHp;
    private float visualRotation;

    private tk2dSpriteAnimator anim = null!;
    private tk2dSprite sprite = null!;
    private Transform visualTransform = null!;
    private Renderer visual = null!;
    private BoxCollider2D body = null!;
    private AudioSource audio = null!;
    private AudioSource idleAudio = null!;
    private AudioSource wiggleAudio = null!;
    private Coroutine? hitTint;

    internal void SetVisualRotation(float degrees) => visualRotation = degrees;

    private void Awake()
    {
        GameObject visuals = new("Visuals");
        visuals.transform.SetParent(transform, false);
        visuals.transform.localPosition = Vector3.zero;
        visuals.transform.localRotation = Quaternion.Euler(0f, 0f, visualRotation);
        visuals.transform.localScale = Vector3.one;
        visuals.layer = gameObject.layer;
        visualTransform = visuals.transform;

        sprite = tk2dBaseSprite.AddComponent<tk2dSprite>(visuals, Vyrmling.Collection, 0);
        anim = visuals.AddComponent<tk2dSpriteAnimator>();
        anim.Library = Vyrmling.Library;
        visual = visuals.GetComponent<Renderer>();

        body = GetComponent<BoxCollider2D>();
        if (!body)
            throw new InvalidOperationException("Noodlevyrm root BoxCollider2D was not created before activation.");

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (!rb)
            throw new InvalidOperationException("Noodlevyrm root Rigidbody2D was not created before activation.");

        // Sounds follow audio settings
        audio = gameObject.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        AudioSource? heroAudio = HeroController.instance
            ? HeroController.instance.GetComponent<AudioSource>()
            : null;
        if (heroAudio)
            audio.outputAudioMixerGroup = heroAudio!.outputAudioMixerGroup;

        idleAudio = gameObject.AddComponent<AudioSource>();
        idleAudio.playOnAwake = false;
        idleAudio.spatialBlend = 0f;
        idleAudio.loop = true;
        idleAudio.outputAudioMixerGroup = audio.outputAudioMixerGroup;

        wiggleAudio = gameObject.AddComponent<AudioSource>();
        wiggleAudio.playOnAwake = false;
        wiggleAudio.spatialBlend = 0f;
        wiggleAudio.loop = true;
        wiggleAudio.volume = Vyrmling.WiggleVolume;
        wiggleAudio.outputAudioMixerGroup = audio.outputAudioMixerGroup;

        SetExposed(false);
        state = State.Hidden;
        Play("Wiggle");
    }

    private void SetExposed(bool exposed)
    {
        if (visual)
            visual.enabled = exposed;
        if (body)
            body.enabled = exposed;
    }

    private void Play(string clip)
    {
        wiggleAudio.Stop();
        audio.Stop();

        if (state != State.Hidden)
            idleAudio.Stop();

        anim.Play(Vyrmling.Library.GetClipByName(clip));

        if (state == State.Hidden)
            return;

        if (clip == "Wiggle")
        {
            wiggleAudio.clip = Vyrmling.WiggleSound.Clip;
            if (wiggleAudio.clip)
                wiggleAudio.Play();
            return;
        }

        audio.clip = clip switch
        {
            "Out" => Vyrmling.OutSound.Clip,
            "In" => Vyrmling.InSound.Clip,
            "Attack" => Vyrmling.AttackSound.Clip,
            "Death" => Vyrmling.DeathSound.Clip,
            _ => null
        };

        if (audio.clip)
            audio.Play();
    }

    private float HornetDistance() =>
        HeroController.instance
            ? Vector2.Distance(HeroController.instance.transform.position, transform.position)
            : float.MaxValue;

    private void UpdateIdleSound(float distance)
    {
        if (state != State.Hidden || distance > Vyrmling.IdleHearRange)
        {
            if (idleAudio.isPlaying)
                idleAudio.Stop();
            return;
        }

        if (!idleAudio.clip)
            idleAudio.clip = Vyrmling.IdleSound.Clip;
        if (idleAudio.clip && !idleAudio.isPlaying)
            idleAudio.Play();
    }

    private void Update()
    {
        float distance = HornetDistance();
        UpdateIdleSound(distance);

        switch (state)
        {
            case State.Hidden:
                if (distance <= Vyrmling.TriggerRange)
                {
                    SetExposed(true);
                    state = State.Out;
                    Play("Out");
                }
                break;

            case State.Out:
                if (!anim.Playing)
                    StartWiggling();
                break;

            case State.Wiggle:
                if (distance > Vyrmling.RetreatRange)
                {
                    state = State.In;
                    Play("In");
                    break;
                }

                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackOrbSpawned = false;
                    attackHoldLeft = Vyrmling.AttackMaxHold;
                    state = State.Attack;
                    Play("Attack");
                }
                break;

            case State.Attack:
                if (!anim.Playing)
                {
                    if (!attackOrbSpawned)
                    {
                        attackOrbSpawned = true;
                        Vector2 rotatedOffset = (Vector2)(visualTransform.rotation * (Vector3)Vyrmling.OrbOffset);
                        Vyrmling.Strike((Vector2)transform.position + rotatedOffset);
                    }

                    attackHoldLeft -= Time.deltaTime;
                    if (!audio.isPlaying || attackHoldLeft <= 0f)
                        StartWiggling();
                }
                break;

            case State.In:
                if (!anim.Playing)
                {
                    SetExposed(false);
                    audio.Stop();
                    state = State.Hidden;
                }
                break;
        }
    }

    private void StartWiggling()
    {
        attackTimer = UnityEngine.Random.Range(Vyrmling.AttackDelay.x, Vyrmling.AttackDelay.y);
        state = State.Wiggle;
        Play("Wiggle");
    }

    internal void TakeHit(int damage)
    {
        if (state == State.Dead || damage <= 0)
            return;

        hp -= damage;
        if (hp <= 0)
        {
            Die();
            return;
        }

        if (hitTint != null)
            StopCoroutine(hitTint);
        hitTint = StartCoroutine(HitTint());
    }

    private IEnumerator HitTint()
    {
        sprite.color = Vyrmling.HitTint;
        yield return new WaitForSeconds(Vyrmling.HitTintTime);
        sprite.color = Color.white;
        hitTint = null;
    }

    private void Die()
    {
        state = State.Dead;
        body.enabled = false;
        if (hitTint != null)
            StopCoroutine(hitTint);
        sprite.color = Color.white;
        if (visual)
            visual.enabled = true;

        Play("Death");
        Vyrmling.RecordKill();
        StartCoroutine(LeaveCorpse());
    }

    private IEnumerator LeaveCorpse()
    {
        while (anim.Playing)
            yield return null;

        if (Vyrmling.CorpseSprite)
        {
            GameObject corpse = new("Noodlevyrm Corpse");
            SceneManager.MoveGameObjectToScene(corpse, gameObject.scene);
            corpse.transform.position = transform.position;
            corpse.transform.rotation = visualTransform.rotation;
            corpse.transform.localScale = Vector3.one;
            corpse.AddComponent<SpriteRenderer>().sprite = Vyrmling.CorpseSprite;
        }

        Destroy(gameObject);
    }
}