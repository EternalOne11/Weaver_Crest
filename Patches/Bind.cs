using GlobalSettings;
using HutongGames.PlayMaker;
using Silksong.FsmUtil;
using System.Collections;
using UnityEngine;
using static Weaver_Crest.Weaver_CrestPlugin;

namespace Weaver_Crest.Patches;

internal static class Bind
{
	//speed of each bind
    private const float BaseDuration = 0.65f;

    private static float cachedTickDuration;
    private static bool cancelCheck;

    internal static void TripleBind(FsmInt amountHealed, FsmInt numberOfBinds, FsmFloat secondsPerBind, PlayMakerFSM bindFsm)
    {
        //Bind amount values
        amountHealed.Value = 1;
        numberOfBinds.Value = Gameplay.MultibindTool.IsEquipped ? 4 : 3;

		// Quick bind speed, multipler... (Vanilla is 0.6) 
		// Higher values mean slower bind, Lower values mean quicker bind.
        bool quickBindEquipped = ToolItemManager.IsToolEquipped("Quickbind");
        const float QuickBindSpeed = 1f;
        float DesiredDuration = quickBindEquipped ? BaseDuration * QuickBindSpeed : BaseDuration;
        secondsPerBind.Value = DesiredDuration;
        cachedTickDuration = DesiredDuration;

        if (!cancelCheck)
            cancelCheck = true;
            bindFsm.GetState("Bind Chain Start")!.AddMethod(() => BindChainStart(bindFsm));
    }

    private static bool BindCancel;

    private static void BindChainStart(PlayMakerFSM bindFsm)
    {
        if (!YenCrest.IsEquipped)
            return;

        if (BindCancel)
            return;

        HeroController.instance.StartCoroutine(PressTracker(bindFsm));
    }

    private static IEnumerator PressTracker(PlayMakerFSM bindFsm)
    {
        BindCancel = true;

        float BindDuration = cachedTickDuration;
        float elapsed = 0f;
        bool LastPressedFrame = HeroController.instance.inputHandler.inputActions.Cast.IsPressed;

        while (elapsed < BindDuration)
        {
            bool isPressed = HeroController.instance.inputHandler.inputActions.Cast.IsPressed;
            if (isPressed && !LastPressedFrame)
            {
                if (bindFsm.Fsm.ActiveStateName != "Idle")
                    bindFsm.Fsm.SetState("End Bind");
                BindCancel = false;
                yield break;
            }
            LastPressedFrame = isPressed;
            elapsed += Time.deltaTime;
            yield return null;
        }

        BindCancel = false;
    }
}
