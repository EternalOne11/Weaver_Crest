using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Weaver_Crest.Patches;

// Requires .wav not .ogg
internal static class Sounds {
	private static readonly Dictionary<string, string> AttackSounds = new() {
		["Slash"] = "Slash1.wav",
		["AltSlash"] = "Slash2.wav",
		["WallSlash"] = "Slash2.wav", //Usually the same as alt attack
		["UpSlash"] = "Slash3_up.wav",
		["DownSlash"] = "Slash4_down.wav",
		["DashSlashA"] = "Dash_slash1.wav", //for dash slashes 1 and 3
		["DashSlashB"] = "Dash_slash2.wav", //for dash slashes 2 and 4
		["ChargedSlash"] = "Charge_slash_attack.wav",
		["ChargedWindup"] = "Charge_slash_windup.wav",
	};

	private static readonly Dictionary<string, AudioClip?> clips = [];
	private static readonly Dictionary<string, AudioClip?> attackClips = [];
	private static AudioClip[]? gameHurtClips;
	private static float NextHurt;
	private static AudioSource? source;
	private static string[]? resourceNames;

	internal static void Load() {
		foreach (var pair in AttackSounds) {
			if (!clips.TryGetValue(pair.Value, out AudioClip? clip))
				clips[pair.Value] = clip = LoadEmbeddedWav(pair.Value);
			attackClips[pair.Key] = clip;
		}
	}

	internal static void PlayAttack(string attack) {
		if (attackClips.TryGetValue(attack, out AudioClip? clip))
			Play(clip);
	}

	internal static void PlayHurt() {
		if (!GameManager.instance.gameSettings.playerVoiceEnabled)
			return;

		if ((gameHurtClips == null || gameHurtClips.Length == 0) && Time.unscaledTime >= NextHurt) {
			NextHurt = Time.unscaledTime + 5f;
			gameHurtClips = [.. Resources.FindObjectsOfTypeAll<AudioClip>()
				.Where(c => c.name is "w1" or "w2" or "w3" or "w4" or "w5" or "w6" or "w7")
				.GroupBy(c => c.name).Select(g => g.First())];
		}

		if (gameHurtClips != null && gameHurtClips.Length > 0)
			Play(gameHurtClips[UnityEngine.Random.Range(0, gameHurtClips.Length)]);
	}

	private const float MuteTime = 0.35f;
	private static float MuteUntil = -1f;
	private static int skippedFrame = -1;

	internal static void MuteForTrade() {
		if (Time.frameCount == skippedFrame)
			return;
		MuteUntil = Time.time + MuteTime;
	}

	private static bool IsVoice(string name) {
		if (name.Length < 2 || name[0] != 'a')
			return false;
		for (int i = 1; i < name.Length; i++)
			if (!char.IsDigit(name[i]))
				return false;
		return true;
	}

	internal static bool ActiveMute => Time.time < MuteUntil;

	internal static bool shouldMute(AudioClip? clip) {
		if (clip == null || !IsVoice(clip.name))
			return false;
		MuteUntil = -1f;
		skippedFrame = Time.frameCount;
		return true;
	}

	private static UnityEngine.Audio.AudioMixerGroup? heroGroup;

	internal static void FixVolume(AudioSource source, AudioClip? clip) {
		if (source.outputAudioMixerGroup || clip == null || !IsVoice(clip.name))
			return;
		if (!heroGroup && HeroController.instance)
			heroGroup = HeroController.instance.GetComponent<AudioSource>()?.outputAudioMixerGroup;
		if (heroGroup)
			source.outputAudioMixerGroup = heroGroup;
	}

	internal static void Play(AudioClip? clip, float volume = 1f) {
		try {
			if (clip == null)
				return;
			if (!source) {
				var hero = HeroController.instance;
				source = hero.GetComponent<AudioSource>();
				if (!source) {
					source = hero.gameObject.AddComponent<AudioSource>();
					source.playOnAwake = false;
					source.outputAudioMixerGroup = hero.GetComponentsInChildren<AudioSource>(true)
						.FirstOrDefault(s => s != source && s.outputAudioMixerGroup)?.outputAudioMixerGroup;
				}
			}
			source!.PlayOneShot(clip, volume);
		}
		catch (Exception) {
		}
	}

	private static AudioClip? LoadEmbeddedWav(string fileName) {
		try {
			var assembly = typeof(Sounds).Assembly;
			resourceNames ??= assembly.GetManifestResourceNames();
			string? resource = resourceNames.FirstOrDefault(n =>
				n == fileName || n.EndsWith("." + fileName, StringComparison.Ordinal));
			if (resource == null)
				return null;

			using Stream stream = assembly.GetManifestResourceStream(resource)!;
			using MemoryStream memory = new();
			stream.CopyTo(memory);
			byte[] wav = memory.ToArray();

			int channels = BitConverter.ToInt16(wav, 22);
			int sampleRate = BitConverter.ToInt32(wav, 24);
			int bits = BitConverter.ToInt16(wav, 34);
			if (bits != 16)
				return null;

			int pos = 12;
			while (!(wav[pos] == 'd' && wav[pos + 1] == 'a' && wav[pos + 2] == 't' && wav[pos + 3] == 'a'))
				pos += 8 + BitConverter.ToInt32(wav, pos + 4);
			int dataSize = BitConverter.ToInt32(wav, pos + 4);
			int dataStart = pos + 8;

			float[] samples = new float[dataSize / 2];
			for (int i = 0; i < samples.Length; i++)
				samples[i] = BitConverter.ToInt16(wav, dataStart + i * 2) / 32768f;

			AudioClip clip = AudioClip.Create(fileName, samples.Length / channels, channels, sampleRate, false);
			clip.SetData(samples, 0);
			return clip;
		}
		catch (Exception) {
			return null;
		}
	}
}

// Replaces normal attack grunt during mask silk trade.
[HarmonyPatch]
internal static class AttackGrunts {
	[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), [typeof(AudioClip), typeof(float)])]
	[HarmonyPrefix]
	private static bool SkipOneShot(AudioSource __instance, AudioClip clip) {
		Sounds.FixVolume(__instance, clip);
		return !(Sounds.ActiveMute && Sounds.shouldMute(clip));
	}

	[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), [])]
	[HarmonyPrefix]
	private static bool SkipPlay(AudioSource __instance) {
		if (!__instance.outputAudioMixerGroup) // only then is the clip looked at
			Sounds.FixVolume(__instance, __instance.clip);
		return !(Sounds.ActiveMute && Sounds.shouldMute(__instance.clip));
	}
}
