using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace LunaticPatch
{
	[HarmonyPatch]
	internal static class LunaticPatch
	{
		internal static ManualLogSource Logger;

		// manually patched
		//[HarmonyPrefix]
		//[HarmonyPatch(typeof(Resources), "Load", typeof(string))]
		private static bool ReplaceAsset(ref Object __result, string path)
		{
			return !Lunatic.ReplaceAsset(ref __result, path);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Player_Feet), "Jump")]
		private static void OnPlayerJump(Player_Control_scr ___Player)
		{
			Lunatic.OnPlayerJump(___Player);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Save), "LOAD_FILE")]
		private static void OnFileLoad(int Save_Slot, Vector3 POS, CONTROL CON)
		{
			string file = Application.dataPath + "/SAVE_" + Save_Slot + ".LUNATIC";

			Lunatic.Internal_LoadModData(file);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Save), "SAVE_FILE")]
		private static void OnFileSave(int Save_Slot, Vector3 POS, CONTROL CON)
		{
			string file = Application.dataPath + "/SAVE_" + Save_Slot + ".LUNATIC";

			Lunatic.Internal_SaveModData(file);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(AREA_SAVED_ITEM), "Load")]
		private static bool OnMultipleStatesLoad(AREA_SAVED_ITEM __instance)
		{
			if (__instance.CON == null)
				__instance.CON = Lunatic.Control;

			if (__instance is ModMultipleStates modMultipleStates)
			{
				modMultipleStates.OnLoad();
				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(AREA_SAVED_ITEM), "Save")]
		private static bool OnMultipleStatesSave(AREA_SAVED_ITEM __instance)
		{
			if (__instance.CON == null)
				__instance.CON = Lunatic.Control;

			if (__instance is ModMultipleStates modMultipleStates)
			{
				modMultipleStates.OnSave();
				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Useable_Item), "Use")]
		private static bool OnUseableItemUse(Useable_Item __instance)
		{
			if (__instance is ModItem modItem)
			{
				modItem.OnUse();

				if (modItem.ITM_CAST != "0" && !string.IsNullOrEmpty(modItem.ITM_CAST))
				{
					Transform tr = modItem.transform;

					Object.Instantiate(Resources.Load(modItem.ITM_CAST), tr.position, tr.rotation);
				}

				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Dialog), "LOAD")]
		private static void OnDialogLoad(Dialog __instance)
		{
			if (__instance.CON == null)
				__instance.CON = Lunatic.Control;

			for (int i = 0; i < __instance.OBJS.Length; i++)
			{
				switch (__instance.OBJS[i].name)
				{
					case "UIREF_PLAYER_NAME":
						__instance.OBJS[i] = Lunatic.UIReferences.PlayerName;
						break;
					case "UIREF_PLAYER_TYPED_TEXT":
						__instance.OBJS[i] = Lunatic.UIReferences.PlayerTypedText;
						break;
					default:
						break;
				}
			}

			for (int i = 0; i < __instance.OBJS2.Length; i++)
			{
				switch (__instance.OBJS2[i].name)
				{
					case "UIREF_PLAYER_RESPONSE_YES":
						__instance.OBJS2[i] = Lunatic.UIReferences.PlayerResponseYes;
						break;
					case "UIREF_PLAYER_RESPONSE_NO":
						__instance.OBJS2[i] = Lunatic.UIReferences.PlayerResponseNo;
						break;
					case "UIREF_PLAYER_RESPONSE_EXIT":
						__instance.OBJS2[i] = Lunatic.UIReferences.PlayerResponseExit;
						break;
					default:
						break;
				}
			}
		}
	}
}
