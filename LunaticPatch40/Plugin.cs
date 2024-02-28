using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

[BepInPlugin("com.empiodavion.lunatic40", "Lunatic Modding API net40", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
	private void Awake()
	{
		LunaticPatch40.LunaticPatch40.Logger = Logger;

		Harmony harmony = new Harmony("com.empiodavion.lunatic40");

		// manually patch the coroutine for Dialog.AnimateText()
		{
			MethodInfo animateText = typeof(Dialog).GetMethod("AnimateText", BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo transpiler = GetMethod(LunaticPatch40.LunaticPatch40.DialogAnimateTextTranspiler);

			Logger.LogInfo($"AT: {animateText}, TR: {transpiler}");

			animateText = AccessTools.EnumeratorMoveNext(animateText);

			Logger.LogInfo("ATEMN: " + animateText);

			harmony.Patch(animateText, transpiler: new HarmonyMethod(transpiler));
		}

		harmony.PatchAll();
	}

	private static MethodInfo GetMethod(System.Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>> func)
	{
		return func.Method;
	}

	private static MethodInfo GetMethod(System.Func<IEnumerable<CodeInstruction>, ILGenerator, IEnumerable<CodeInstruction>> func)
	{
		return func.Method;
	}
}
