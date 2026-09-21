using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Weaver_Crest.Utils;

internal static class ResourceUtils {
	/// (all copied from queen crest modify as needed)
	
	/// <summary>
	/// Loads an embedded resource PNG file into a unity Sprite object.
	/// </summary>
	/// <param name="filename">The path and file name of the PNG</param>
	/// <param name="pivot">Optional point representing the center/pivot point of the
	/// image. Default: (0.5, 0.5)</param>
	/// <param name="pixelsPerUnit">Optional in-game resolution for the image.
	/// Default: 100</param>
	
	public static Sprite LoadEmbeddedPngAsSprite(
		string filename,
		Vector2? pivot = null,
		float pixelsPerUnit = 100f
	) {
		Texture2D tex = LoadEmbeddedPngAsTexture2D(filename);
		tex.name = filename;
		return
			Sprite.Create(
				tex,
				new Rect(0, 0, tex.width, tex.height),
				pivot ?? new(0.5f, 0.5f),
				pixelsPerUnit
			);
	}
		/// <summary>
	/// Loads an embedded resource PNG file into a unity Sprite object.
	/// </summary>
	/// <param name="filename">The path and file name of the PNG</param>
	public static Texture2D LoadEmbeddedPngAsTexture2D(string filename) {
		var assembly = Assembly.GetExecutingAssembly();
		string resourceName = assembly.GetManifestResourceNames()
			.First(x => x.EndsWith(filename));

		byte[] data;

		using (Stream stream = assembly.GetManifestResourceStream(resourceName)) {
			using BinaryReader reader = new(stream);
			data = reader.ReadBytes((int)stream.Length);
		}

		Texture2D tex = new(1, 1);
		tex.LoadImage(data);
		return tex;
	}
}