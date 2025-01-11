using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
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

		private enum SubMenus
		{
			Weapons		= 4,
			Magics		= 5,
			Items		= 6,
			Shop		= 14,
			Materials	= 18
		}

		internal static ManualLogSource Logger;

		const string MATERIAL_PREFIX = "Materials/MAT";

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
		internal static void OnUseableItemUse(Useable_Item __instance)
		{
			if (__instance is ModItem modItem)
			{
				if (__instance.Count > 0)
				{
					modItem.OnUse();

					if (modItem.spawnOnUse != null)
					{
						Transform tr = modItem.transform;

						Object.Instantiate(modItem.spawnOnUse, tr.position, tr.rotation);
					}
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Dialog), "LOAD")]
		internal static bool OnDialogLoad(Dialog __instance)
		{
			if (__instance is ModDialog modDialog)
				modDialog.Internal_Init();
			
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
			if (__instance.sub_menu == 18 && (MenusTexts)text2load == MenusTexts.PlayerMaterials)
				Lunatic.Internal_AddMaterialTexts(__instance);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Menus), "LoadText")]
		internal static void OnMenusLoadTextPost(Menus __instance, int text2load)
		{
			if ((MenusTexts)text2load == MenusTexts.PlayerWeapons)
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
		internal static void OnTMPTextSetTextPre(TMP_Text __instance, ref string value)
		{
			if (!string.IsNullOrEmpty(value) &&
				value.StartsWith("L#"))
			{
				int slash = value.IndexOf('/', 3);
				value = value.Substring(slash + 1);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(I2.Loc.LocalizationManager), "RegisterSourceInResources")]
		internal static void OnLocalizationManagerRegisterSourceInResourcesPre()
		{
			Lunatic.Internal_RegisterModLocalisations(AddSource);
		}

		[HarmonyReversePatch]
		[HarmonyPatch(typeof(I2.Loc.LocalizationManager), "AddSource")]
		private static void AddSource(I2.Loc.LanguageSourceData languageSourceData)
		{
			throw new System.NotImplementedException("This is replaced by I2.Loc.LocalizationManager.AddSource");
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(I2.Loc.LocalizationManager), "TryGetTranslation")]
		internal static void OnLocalizationManagerTryGetTranslationPre(ref string Term, out string Translation, bool FixForRTL = true, int maxLineLengthForRTL = 0, bool ignoreRTLnumbers = true, bool applyParameters = false, GameObject localParametersRoot = null, string overrideLanguage = null, bool allowLocalizedParameters = true)
		{
			Translation = null;

			if (string.IsNullOrEmpty(Term))
				return;

			if (Term.Contains("TEMPLATE"))
			{
				Debug.Log("Term: " + Term);
				Lunatic.PrintStackTrace();
			}

			if (Term.StartsWith(MATERIAL_PREFIX))
			{
				int len = MATERIAL_PREFIX.Length;
				int underscore = Term.IndexOf('_', len);

				string idStr;

				if (underscore >= 0)
					idStr = Term.Substring(len, underscore - len);
				else
					idStr = Term.Substring(len);

				if (int.TryParse(idStr, out int id))
				{
					if (id >= Lunatic.BaseMaterialCount)
					{
						ModMaterial modMat = Lunatic.GetModMaterial(id * 2);

						if (modMat != null)
							Term = "Materials/" + modMat.Name;
					}
				}
				else
					Debug.LogWarning("Failed to parse id from term " + Term);
			}
			else
			{
				int index = Term.IndexOf("L#");

				if (index >= 0)
				{
					string prefix = Term.Substring(0, index);
					string internalName = Term.Substring(index);
					Lunatic.ReadInternalName(internalName, out string modName, out string objectName, false);
					Term = prefix + objectName;
				}
			}
			
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(I2.Loc.LanguageSourceData), "TryGetTranslation")]
		internal static void OnLanguageSourceDataTryGetTranslationPre(I2.Loc.LanguageSourceData __instance, string term, ref string Translation,
			string overrideLanguage = null, string overrideSpecialization = null,
			bool skipDisabled = false, bool allowCategoryMistmatch = false)
		{
			if (__instance.owner is ModLanguageSourceAsset modOwner)
			{
				modOwner.OnTryGetTranslation(term, ref Translation, overrideLanguage, overrideSpecialization, skipDisabled, allowCategoryMistmatch);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Menus), "ItemLoad")]
		internal static void OnMenusItemLoad(Menus __instance, int ___EQ_SEL)
		{
			SubMenus subMenu = (SubMenus)__instance.sub_menu;

			switch (subMenu)
			{
				case SubMenus.Materials:
					SetModMaterialTexts(__instance, ___EQ_SEL);
					break;
				default:
					break;
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Loot_scr), "OnEnable")]
		internal static void OnLootAwake(Loot_scr __instance)
		{
			Lunatic.Internal_AddLoot(__instance);
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Damage_Trigger), "OnTriggerStay")]
		internal static void OnDamageTriggerOnTriggerStay(Damage_Trigger __instance, Collider obj, float ___last)
		{
			if (__instance.Constant && ___last < Time.time &&
				__instance is ModDamageTrigger modDamageTrigger)
			{
				if (!modDamageTrigger.OnlyPL &&
					obj.TryGetComponent(out OBJ_HEALTH health))
					modDamageTrigger.HitObject(health, true);
				else if (modDamageTrigger.EffectPlayer &&
					obj.TryGetComponent(out Player_Control_scr player))
					modDamageTrigger.HitPlayer(player, true);
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(Damage_Trigger), "Die")]
		internal static void OnDamageTriggerDie(Damage_Trigger __instance, Transform obj)
		{
			if (__instance is ModDamageTrigger modDamageTrigger)
				modDamageTrigger.HitPhysical(obj);
		}

		private static void SetModMaterialTexts(Menus menus, int selected)
		{
			string mat = menus.CON.CURRENT_PL_DATA.MATER[selected];

			if (string.IsNullOrEmpty(mat))
				return;

			string idStr = mat.Substring(0, mat.Length - 2);

			if (!int.TryParse(idStr, out int id))
			{
				Debug.LogWarning("Could not read ID from material " + mat);
				return;
			}
			else
				Debug.Log("Read material ID " + id);

			if (id <= Lunatic.BaseMaterialCount)
				return;

			ModMaterial modMat = Lunatic.GetModMaterial(id);

			if (modMat == null)
			{
				Debug.LogWarning("Could not find mod material " + id);
				return;
			}

			string term = $"Materials/{modMat.name}";

			menus.TXT[62].text = I2.Loc.LocalizationManager.GetTranslation($"Materials/{modMat.name}");
			menus.TXT[61].text = I2.Loc.LocalizationManager.GetTranslation($"Material Descriptions/{modMat.name}_Details");
		}
	}
}
