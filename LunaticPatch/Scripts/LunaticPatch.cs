using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LunaticPatch
{
	[HarmonyPatch]
	internal static class LunaticPatch
	{
		private enum MenusTexts
		{
			PlayerData,
			SystemData,
			PlayerWeapons,
			PlayerEquipped,
			PlayerMagics,
			SaveData,
			Nothing,
			PlayerItems,
			PlayerClassData,
			PlayerStats,
			PlayerLevelUp,
			Shop,
			GameSettings,
			UpdateAlchemyMenu,
			ResetAlchemyMenu,
			PlayerMaterials,
			PlayerRecipes
		}

		internal static ManualLogSource Logger;

		// manually patched
		//[HarmonyPrefix]
		//[HarmonyPatch(typeof(Resources), "Load", typeof(string))]
		internal static bool OnResourcesLoad(ref Object __result, string path)
		{
			return !Lunatic.Internal_ReplaceAsset(ref __result, path);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Player_Feet), "Jump")]
		internal static void OnPlayerFeetJump(Player_Control_scr ___Player)
		{
			Lunatic.Internal_OnPlayerJump(___Player);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Save), "LOAD_FILE")]
		internal static void OnSaveLoadFile(int Save_Slot)
		{
			string file = Application.dataPath + "/SAVE_" + Save_Slot + ".LUNATIC";

			Lunatic.Internal_LoadModData(file);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Save), "SAVE_FILE")]
		internal static void OnSaveSaveFile(int Save_Slot, Vector3 POS, CONTROL CON)
		{
			string file = Application.dataPath + "/SAVE_" + Save_Slot + ".LUNATIC";

			Lunatic.Internal_SaveModData(file);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(AREA_SAVED_ITEM), "Load")]
		internal static bool OnMultipleStatesLoad(AREA_SAVED_ITEM __instance)
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
		internal static bool OnMultipleStatesSave(AREA_SAVED_ITEM __instance)
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
		internal static bool OnUseableItemUse(Useable_Item __instance)
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
		internal static void OnDialogLoad(Dialog __instance)
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

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Alki), "OnEnable")]
		internal static void OnAlkiOnEnable(Alki __instance)
		{
			Lunatic.Internal_InitRecipesArray(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Alki), "Reset")]
		internal static void OnAlkiReset(Alki __instance)
		{
			void CheckHasNeeded(Alki alki, ref int id, int count)
			{
				if (id == -1 || count == 0)
					return;

				string idStr = id.ToString();

				string[] array = alki.CON.CURRENT_PL_DATA.MATER;
				int index = System.Array.FindIndex(array, (x) => !string.IsNullOrEmpty(x) && x.Substring(0, x.Length - 2) == idStr);

				if (index >= 0)
				{
					int has = int.Parse(array[index].Substring(array[index].Length - 2));

					if (has < count)
						id = -1;
				}
			}

			// if any of the select materials are the same, the first of them will have their need at the total value,
			// and the remaining will be reduced by 1 per duplicate, example of all the same (need1 = 3, need2 = 2, need3 = 1)
			// this is because the amount of that material needed will be reduced as each one is removed from the forge selection
			int need1 = 0;
			int need2 = 0;
			int need3 = 0;

			if (__instance.current_1 != -1)
				need1++;

			if (__instance.current_2 != -1)
			{
				if (__instance.current_2 == __instance.current_1)
					need1++;
				
				need2++;
			}

			if (__instance.current_3 != -1)
			{
				if (__instance.current_3 == __instance.current_1)
					need1++;
				
				if (__instance.current_3 == __instance.current_2)
					need2++;
				
				need3++;
			}

			CheckHasNeeded(__instance, ref __instance.current_1, need1);
			CheckHasNeeded(__instance, ref __instance.current_2, need2);
			CheckHasNeeded(__instance, ref __instance.current_3, need3);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Item_Pickup_scr), "Pickup")]
		internal static void OnItemPickupPickup(Item_Pickup_scr __instance)
		{
			if (__instance is ModItemPickup modItemPickup)
				modItemPickup.OnPickup();
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Menus), "LoadSub")]
		internal static void OnMenusLoadSub(Menus __instance)
		{
			if (__instance.sub_menu == 16)
				Lunatic.Internal_AddMaterialTexts(__instance);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Menus), "LoadText")]
		internal static void OnMenusLoadTextPre(Menus __instance, int text2load)
		{
			Debug.Log($"Sub Menu: {__instance.sub_menu}, Text: {text2load}");

			if (__instance.sub_menu == 18 && text2load == 15)
				Lunatic.Internal_AddMaterialTexts(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Menus), "LoadText")]
		internal static void OnMenusLoadTextPost(Menus __instance, int text2load)
		{
			if (text2load == 2)
			{
				string[] weapons = __instance.CON.CURRENT_PL_DATA.WEPS;
				GameObject itemGO = __instance.ITEMS[4];
				Transform parent = itemGO.transform.parent;

				for (int i = 0; i < parent.childCount; i++)
				{
					if (!string.IsNullOrEmpty(weapons[i]) &&
						weapons[i].StartsWith("L#"))
					{
						Transform item = parent.GetChild(i);
						Transform child = item.GetChild(0);
						TextMeshProUGUI textMesh = child.GetComponent<TextMeshProUGUI>();

						Lunatic.ReadInternalName(weapons[i], out string modName, out string objectName, false);

						textMesh.text = StaticFuncs.REMOVE_NUMS(objectName);
					}
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
		internal static void OnTMPTextSetText(TMP_Text __instance, ref string value)
		{
			if (!string.IsNullOrEmpty(value) &&
				value.StartsWith("L#"))
			{
				int slash = value.IndexOf('/', 3);
				value = value.Substring(slash + 1);
			}
		}
	}
}
