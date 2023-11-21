using BepInEx;
using HarmonyLib;

[BepInPlugin("com.empiodavion.lunatic40", "Lunatic Modding API net40", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
	private void Awake()
	{
		LunaticBridge.LunaticPatch40.Logger = Logger;

		Harmony harmony = new Harmony("com.empiodavion.lunatic40");

		harmony.PatchAll();
	}
}
