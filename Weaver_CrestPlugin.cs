using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Needleforge;
using Needleforge.Data;
using Weaver_Crest.Patches;
using TeamCherry.Localization;
using UnityEngine;
using static Weaver_Crest.Utils.ResourceUtils;
using SlotInfo = ToolCrest.SlotInfo;

namespace Weaver_Crest;

/*Patches contain
Custum Hud implementation
Custum Bind mechanics

Note that moveset and Evo optout are still from queencrest. 
(evooptout will disable once I implament memory lockets)
*/

[BepInAutoPlugin(id: "io.github.eternalone11.Weaver_Crest")]
[BepInDependency("org.silksong-modding.i18n")]
[BepInDependency("io.github.needleforge", "0.9.0")]
[BepInDependency("org.silksong-modding.unityhelper", "1.1.1")]
[BepInDependency("org.silksong-modding.fsmutil", "0.3.16")] 
public partial class Weaver_CrestPlugin : BaseUnityPlugin
{

/// BELOW COPIED from queen crest. (modify as needed)

	private Harmony Harmony { get; } = new(Id);
	internal static ManualLogSource Log { get; private set; }

	internal static CrestData YenCrest { get; private set; }

	internal static SlotInfo YellowSlot1 { get; private set; }
	internal static SlotInfo YellowSlot2 { get; private set; }
	internal static SlotInfo BlueSlot1 { get; private set; }
	internal static SlotInfo BlueSlot2 { get; private set; }

	internal const string
		YenId = "Weaver";
	internal static readonly LocalisedString
		YenName = new($"Mods.{Id}", "CREST_NAME"),
		YenDesc = new($"Mods.{Id}", "CREST_DESC");

	private static Color AdminGreen { get; }
		= new Color32(r: 89, g: 255, b: 152, a: 255);
	internal static Color AttackColor { get; }
		= Color.Lerp(Color.white, AdminGreen, 0.3f);

	private void Awake()
	{
		Log = Logger;
		Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");

		#region Crest Init + Sprites
	
		Vector2 pivot = new(0.5f, 0.44f);
		Vector2 silhouettePivot = new(0.2f, 0.44f);

		Sprite
			linework = LoadEmbeddedPngAsSprite("Crest.png", pivot),
			silhouette = LoadEmbeddedPngAsSprite("CrestSilhouette.png", pivot),
			glow = LoadEmbeddedPngAsSprite("CrestEquipGlow.png", pivot, 140f);

		float slotOffset = (0.5f - pivot.y) * (linework.rect.height / linework.pixelsPerUnit);

		YenCrest = NeedleforgePlugin.AddCrest($"{YenId}", YenName, YenDesc, linework, silhouette, glow);
		YenCrest.HudFrame.ProfileIcon = LoadEmbeddedPngAsSprite("CrestIcon.png"); // Save profile for crest on normal save
		YenCrest.HudFrame.SteelProfileIcon = LoadEmbeddedPngAsSprite("CrestIcon_S.png"); // Save profile for Steel soul
		#endregion

		#region Tool Slots
	    // The values X,Y Represent, horizontal and vertical respectivly. 
	    // X is the horizontal axis, Negative values move the image left, Positive move them right.
	    // Y is the Vertical axis, Negative values move it Down, Postive move it Up
		YenCrest.AddSkillSlot(AttackToolBinding.Up,	new(-0f,  1.47f + slotOffset), false);
		YenCrest.AddSkillSlot(AttackToolBinding.Down,  new( -0f,  -2.19f + slotOffset), false);
		YenCrest.AddRedSlot(AttackToolBinding.Neutral, new( -0f,  -0.36f + slotOffset), false);
		YenCrest.AddYellowSlot(new(-1.57f,   0.4f + slotOffset), false); //set true when memory lockets are added
		YenCrest.AddYellowSlot(new(-1.57f, -1.1f + slotOffset), false); //set true when memory lockets are added
		YenCrest.AddBlueSlot(  new( 1.57f, 0.4f + slotOffset), false); //set true when memory lockets are added
		YenCrest.AddBlueSlot(  new( 1.57f,  -1.1f + slotOffset), false); //set true when memory lockets are added

		YenCrest.ApplyAutoSlotNavigation(slotDimensions: new(1.25f, 0.75f));
		#endregion

		#region Gameplay

		Hud.Setup(YenCrest);
		
		YenCrest.BindEvent = Bind.TripleBind;

		YenCrest.Moveset.OnInitialized += Moveset.Setup;
		Harmony.PatchAll(typeof(Moveset));
		Harmony.PatchAll(typeof(EvaOptOut));
        Harmony.PatchAll(typeof(SilkMechanics));
        Harmony.PatchAll(typeof(ToolDamage));
		#endregion
	}

	private void OnDestroy() => Harmony.UnpatchSelf();
}
