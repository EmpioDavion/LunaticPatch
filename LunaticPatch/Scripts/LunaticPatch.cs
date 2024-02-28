using BepInEx.Logging;
using HarmonyLib;
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

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Dialog), "AnimateText")]
		internal static void OnDialogAnimateText(Dialog __instance)
		{
			if (__instance is ModDialog modDialog)
				modDialog.Internal_OnSpeak(__instance.Current_Line);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Player_Feet), "Jump")]
		internal static void OnPlayerFeetJump(Player_Control_scr ___Player)
		{
			Lunatic.Internal_OnPlayerJump(___Player);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Save), "ResetData")]
		internal static void OnSaveResetData()
		{
			Lunatic.Internal_OnPlayerDataDelete();
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(AREA_SAVED_ITEM), "Load")]
		internal static bool OnMultipleStatesLoad(AREA_SAVED_ITEM __instance)
		{
			if (__instance is ModMultipleStates modMultipleStates)
			{
				modMultipleStates.Internal_Load();
				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(AREA_SAVED_ITEM), "Save")]
		internal static bool OnMultipleStatesSave(AREA_SAVED_ITEM __instance)
		{
			if (__instance is ModMultipleStates modMultipleStates)
			{
				modMultipleStates.Internal_Save();
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
		internal static bool OnDialogLoad(Dialog __instance)
		{
			if (__instance is ModDialog modDialog)
			{
				modDialog.Internal_Init();
				return false;
			}
			
			return true;
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
			Lunatic.Internal_RemoveUnavailableIngredients(__instance);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Item_Pickup_scr), "Pickup")]
		internal static void OnItemPickupPickup(Item_Pickup_scr __instance)
		{
			if (__instance is ModItemPickup modItemPickup)
				modItemPickup.OnPickup();
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Menus), "Click")]
		internal static void OnMenusClick(Menus __instance, int which)
		{
			Debug.Log($"Menu Click: {which}, Query: {__instance.current_query}");
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Menus), "LoadMenu")]
		internal static void OnMenusLoadMenu(Menus __instance)
		{
			Debug.Log("Menu: " + __instance.current_menu);
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

			//if (__instance.sub_menu == 12 && text2load == 9)
			//	Lunatic.Internal_ResetHeldData();

			// alchemy materials menu
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
