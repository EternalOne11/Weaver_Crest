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
		["DashSlashA"] = "Slash1.wav", //for dash slashes 1 and 3
		["DashSlashB"] = "Slash1.wav", //for dash slashes 2 and 4
		["ChargedSlash"] = "Slash1.wav",
		["ChargedWindup"] = "Slash1.wav",
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
		if ((gameHurtClips == null || gameHurtClips.Length == 0) && Time.unscaledTime >= NextHurt) {
			NextHurt = Time.unscaledTime + 5f;
			gameHurtClips = [.. Resources.FindObjectsOfTypeAll<AudioClip>()
				.Where(c => c.name is "w1" or "w2" or "w3" or "w4" or "w5" or "w6" or "w7")
				.GroupBy(c => c.name).Select(g => g.First())];

		}

		if (gameHurtClips != null && gameHurtClips.Length > 0)
			Play(gameHurtClips[UnityEngine.Random.Range(0, gameHurtClips.Length)]);
	}

	private static readonly HashSet<string> GruntNames = [
		"a1", "a2", "a3", "a4", "a5", "a6", "a7", "a8", "a9", "a10", "a11", "a12", "a13",
		"a17", "a19", "a20", "a23",
	];
	private const float MuteTime = 0.2f;
	private static float MuteUntil = -1f;

	internal static void MuteForTrade() => MuteUntil = Time.time + MuteTime;

	internal static bool MuteActive => Time.time < MuteUntil;

	internal static bool shouldMute(AudioClip? clip) =>
		MuteActive && clip != null && GruntNames.Contains(clip.name);

	internal static void Play(AudioClip? clip, float volume = 1f) {
		try {
			if (clip == null)
				return;
			if (!source) {
				source = HeroController.instance.gameObject.AddComponent<AudioSource>();
				source.playOnAwake = false;
				source.outputAudioMixerGroup = HeroController.instance.GetComponentsInChildren<AudioSource>(true)
					.FirstOrDefault(s => s != source && s.outputAudioMixerGroup)?.outputAudioMixerGroup;
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
	private static bool SkipOneShot(AudioClip clip) => !Sounds.shouldMute(clip);

	[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), [])]
	[HarmonyPrefix]
	private static bool SkipPlay(AudioSource __instance) =>
		!Sounds.MuteActive || !Sounds.shouldMute(__instance.clip);
}
