using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

[BepInPlugin("com.empiodavion.lunatic", "Lunatic Modding API", "1.0.0.0")]
public class Bootstrap : BaseUnityPlugin
{
	private void Awake()
	{
		LunaticPatch.LunaticPatch.Logger = Logger;

		Lunatic.Init();

		// Plugin startup logic
		Logger.LogInfo("Lunatic is loaded!");
		Logger.LogInfo("Lunatic is patching");

		Harmony harmony = new Harmony("com.empiodavion.lunatic");

		// manually patch Resources.Load(string)
		{
			var originals = typeof(Resources).GetMethods(BindingFlags.Public | BindingFlags.Static);
			var original = System.Array.Find(originals, (x) => !x.IsGenericMethod && x.Name == "Load");
			var prefix = typeof(LunaticPatch.LunaticPatch).GetMethod("ReplaceAsset", BindingFlags.NonPublic | BindingFlags.Static);

			harmony.Patch(original, prefix: new HarmonyMethod(prefix));
		}

		harmony.PatchAll();

		Logger.LogInfo("Lunatic patching complete");
	}
}
