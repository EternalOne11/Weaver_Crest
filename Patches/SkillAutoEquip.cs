using HarmonyLib;
using System.Collections.Generic;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

// Silk skill auto equip settings
[HarmonyPatch]
internal static class SkillAutoEquip {
	[HarmonyPatch(typeof(ToolItemManager), nameof(ToolItemManager.AutoEquip), [typeof(ToolItem)])]
	[HarmonyPostfix]
	private static void EquipNewSilkSkill(ToolItem tool) {
		if (!tool || tool.Type != ToolItemType.Skill || !YenCrest.IsEquipped)
			return;
		SkillSlotChoice choice = SkillSlotSetting.Value;
		if (choice == SkillSlotChoice.None)
			return;
		AttackToolBinding binding = choice == SkillSlotChoice.Top ? AttackToolBinding.Up : AttackToolBinding.Down;

		string crestId = PlayerData.instance.CurrentCrestID;
		ToolCrest crest = ToolItemManager.GetCrestByName(crestId);
		if (!crest)
			return;

		List<ToolItem> equipped = ToolItemManager.GetEquippedToolsForCrest(crestId);
		List<string> loadout = new(crest.Slots.Length);
		for (int i = 0; i < crest.Slots.Length; i++) {
			ToolItem? item = equipped != null && i < equipped.Count ? equipped[i] : null;
			loadout.Add(item ? item!.name : string.Empty);
		}

		int slot = -1;
		for (int i = 0; i < crest.Slots.Length; i++)
			if (crest.Slots[i].Type == ToolItemType.Skill && crest.Slots[i].AttackBinding == binding) {
				slot = i;
				break;
			}
		if (slot < 0)
			return;

		loadout[slot] = tool.name;
		ToolItemManager.SetEquippedTools(crestId, loadout);
		ToolItemManager.SendEquippedChangedEvent();
	}
}

// Choice options
public enum SkillSlotChoice { None, Top, Bottom }
