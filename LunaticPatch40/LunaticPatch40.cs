using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace LunaticPatch40
{
	[HarmonyPatch]
	public static class LunaticPatch40
	{
		public static ManualLogSource Logger;

		// stops Lunacid from reloading save data unnecessarily only to overwrite it with the held save data
		[HarmonyTranspiler]
		[HarmonyPatch(typeof(CONTROL), "Start")]
		internal static IEnumerable<CodeInstruction> ControlStartTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			int step = 0;
			Label label = generator.DefineLabel();
			MethodInfo checkHeldData = typeof(Lunatic).GetMethod("Internal_CheckHeldData", BindingFlags.Public | BindingFlags.Static);

			foreach (CodeInstruction instruction in instructions)
			{
				if (step == 0)
				{
					// CURRENT_PL_DATA = Save.LOAD_FILE(...) IL_0036
					if (instruction.opcode == OpCodes.Call)
					{
						MethodInfo methodInfo = (MethodInfo)instruction.operand;

						if (methodInfo.Name == "LOAD_FILE")
						{
							step++;

							// if (!Lunatic.InternalCheckHeldData(this, int))
							yield return new CodeInstruction(OpCodes.Call, checkHeldData);
							yield return new CodeInstruction(OpCodes.Brtrue, label);
							continue;
						}
					}
				}
				else if (step == 1)
				{
					// // IL_003b
					step++;
					continue;
				}
				else if (step == 2)
				{
					// if (Hold_Data.HD != null && Hold_Data.HD.PLAYER_NAME != "") IL_0076
					if (instruction.opcode == OpCodes.Ldsfld)
					{
						FieldInfo fieldInfo = (FieldInfo)instruction.operand;

						if (fieldInfo.Name == "HD")
						{
							step++;
							instruction.labels.Add(label);
						}
					}
				}

				yield return instruction;
			}

			if (step != 3)
			{
				Logger.LogError("Transpiling CONTROL.Start failed - Step: " + step);
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Dialog), "SkipSpeech")]
		internal static IEnumerable<CodeInstruction> DialogSkipSpeechTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo setBlendShapeWeight = typeof(ModDialog).GetMethod("SetBlendShapeWeight");

			foreach (CodeInstruction instruction in instructions)
			{
				// Mouth.SetBlendShapeWeight(...) IL_0057
				if (instruction.opcode == OpCodes.Callvirt)
				{
					MethodInfo methodInfo = (MethodInfo)instruction.operand;

					if (methodInfo.Name == "SetBlendShapeWeight")
					{
						// ModDialog.SetBlendShapeWeight(...)
						yield return new CodeInstruction(OpCodes.Ldarg_0);
						yield return new CodeInstruction(OpCodes.Call, setBlendShapeWeight);
						continue;
					}
				}

				yield return instruction;
			}
		}

		// needs to be manually patched to affect enumerator coroutine
		//[HarmonyTranspiler]
		//[HarmonyPatch(typeof(Dialog), "AnimateText")]
		internal static IEnumerable<CodeInstruction> DialogAnimateTextTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo getBlendShapeWeight = typeof(ModDialog).GetMethod("GetBlendShapeWeight", BindingFlags.Public | BindingFlags.Static);
			MethodInfo setBlendShapeWeight = typeof(ModDialog).GetMethod("SetBlendShapeWeight", BindingFlags.Public | BindingFlags.Static);

			Debug.Assert(getBlendShapeWeight != null, "GetBlendShapeWeight is null");
			Debug.Assert(setBlendShapeWeight != null, "SetBlendShapeWeight is null");

			foreach (CodeInstruction instruction in instructions)
			{
				// Mouth.getBlendShapeWeight(...)
				if (instruction.opcode == OpCodes.Callvirt)
				{
					MethodInfo methodInfo = (MethodInfo)instruction.operand;

					if (methodInfo.Name == "GetBlendShapeWeight")
					{
						// ModDialog.GetBlendShapeWeight(...)
						yield return new CodeInstruction(OpCodes.Ldloc_1);
						yield return new CodeInstruction(OpCodes.Call, getBlendShapeWeight);

						continue;
					}
				}

				// Mouth.SetBlendShapeWeight(...)
				if (instruction.opcode == OpCodes.Callvirt)
				{
					MethodInfo methodInfo = (MethodInfo)instruction.operand;

					if (methodInfo.Name == "SetBlendShapeWeight")
					{
						// ModDialog.SetBlendShapeWeight(...)
						yield return new CodeInstruction(OpCodes.Ldloc_1);
						yield return new CodeInstruction(OpCodes.Call, setBlendShapeWeight);

						continue;
					}
				}

				yield return instruction;
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Save), "LOAD_FILE")]
		internal static IEnumerable<CodeInstruction> SaveLoadFileTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			int step = 0;
			MethodInfo onPlayerDataLoad = typeof(Lunatic).GetMethod("Internal_OnPlayerDataLoad", BindingFlags.Public | BindingFlags.Static);
			LocalBuilder playerData = generator.DeclareLocal(typeof(PlayerData), true);
			byte saveSlot = 0;

			Debug.Assert(onPlayerDataLoad != null, "onPlayerDataLoad is null");

			foreach (CodeInstruction instruction in instructions)
			{
				if (step == 0)
				{
					// PlayerData result = ...
					if (instruction.opcode == OpCodes.Ldarga_S)
					{
						step++;
						saveSlot = (byte)instruction.operand;
					}
				}
				else if (step == 1)
				{
					// PlayerData result = ...
					if (instruction.opcode == OpCodes.Isinst)
					{
						step++;
						yield return instruction;
						yield return new CodeInstruction(OpCodes.Stloc, playerData);
						continue;
					}
				}
				else if (step == 2)
				{
					// return result
					if (instruction.opcode == OpCodes.Ret)
					{
						step++;

						// Lunatic.Internal_OnPlayerDataLoad(result, Save_Slot)
						yield return new CodeInstruction(OpCodes.Ldloc, playerData);
						yield return new CodeInstruction(OpCodes.Ldarg_S, saveSlot);
						yield return new CodeInstruction(OpCodes.Call, onPlayerDataLoad);
						yield return new CodeInstruction(OpCodes.Ldloc, playerData);
					}
				}

				yield return instruction;
			}

			if (step != 3)
			{
				Logger.LogError("Transpiling Save.LOAD_FILE failed");
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Save), "SAVE_FILE")]
		internal static IEnumerable<CodeInstruction> SaveSaveFileTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			bool found = false;
			MethodInfo onPlayerDataSave = typeof(Lunatic).GetMethod("Internal_OnPlayerDataSave", BindingFlags.Public | BindingFlags.Static);
			
			foreach (CodeInstruction instruction in instructions)
			{
				// binaryFormatter.Serialize(fileStream, graph)
				if (!found && instruction.opcode == OpCodes.Ldloc_0)
				{
					found = true;

					// Lunatic.Internal_OnPlayerDataSave(graph, Save_Slot)
					yield return new CodeInstruction(OpCodes.Ldloc_1);
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, onPlayerDataSave);
				}

				yield return instruction;
			}

			if (!found)
			{
				Logger.LogError("Transpiling Save.SAVE_FILE failed");
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Item_Pickup_scr), "Start")]
		internal static IEnumerable<CodeInstruction> ItemPickupStartTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			bool found = false;
			MethodInfo onItemPickupStart = typeof(Lunatic).GetMethod("Internal_OnItemPickupStart", BindingFlags.Public | BindingFlags.Static);

			UnityEngine.Debug.Assert(onItemPickupStart != null, $"{nameof(onItemPickupStart)} is null");

			Label label = generator.DefineLabel();

			foreach (CodeInstruction instruction in instructions)
			{
				// switch (type)
				if (!found && instruction.opcode == OpCodes.Ldfld &&
					((FieldInfo)instruction.operand).Name == "type")
				{
					found = true;

					// if (Lunatic.Internal_OnItemPickupStart(this))
					//	return
					yield return new CodeInstruction(OpCodes.Call, onItemPickupStart);
					yield return new CodeInstruction(OpCodes.Brfalse, label);
					yield return new CodeInstruction(OpCodes.Ret);
					yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = { label } };
				}

				yield return instruction;
			}

			if (!found)
			{
				Logger.LogError("Transpiling Alki.OnEnable failed");
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Alki), "OnEnable")]
		internal static IEnumerable<CodeInstruction> AlkiOnEnableTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			int step = 0;
			MethodInfo checkRecipeIndex = GetMethodInfo<System.Func<Alki, int, bool>>(CheckZoneIndex);

			Label label = generator.DefineLabel();

			foreach (CodeInstruction instruction in instructions)
			{
				if (step == 0)
				{
					// start of for loop
					if (instruction.opcode == OpCodes.Br)
					{
						step++;

						yield return instruction;

					}
				}
				else if (step == 1)
				{
					step++;

					CodeInstruction dupe = new CodeInstruction(instruction);
					dupe.labels.Clear();

					instruction.opcode = OpCodes.Ldarg_0;
					instruction.operand = null;

					// calling function to save on needing to grab several fields
					// if (!CheckRecipeIndex(this, i))
					//	break;
					yield return instruction;
					yield return new CodeInstruction(OpCodes.Ldloc_2);
					yield return new CodeInstruction(OpCodes.Call, checkRecipeIndex);
					yield return new CodeInstruction(OpCodes.Brfalse, label);
					yield return dupe;

					continue;
				}
				else if (step == 2)
				{
					// end of loop
					if (instruction.opcode == OpCodes.Blt)
						step++;
				}
				else if (step == 3)
				{
					// set label on instruction that is right after the end of the loop
					step++;

					instruction.labels.Add(label);
				}

				yield return instruction;
			}

			if (step != 4)
			{
				Logger.LogError("Transpiling Alki.OnEnable failed - Step: " + step);
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Alki), "Set")]
		internal static IEnumerable<CodeInstruction> AlkiSetTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			int step = 0;
			Label label = default;
			FieldInfo con = typeof(Alki).GetField("CON", BindingFlags.Public | BindingFlags.Instance);
			FieldInfo data = typeof(CONTROL).GetField("CURRENT_PL_DATA", BindingFlags.Public | BindingFlags.Instance);
			FieldInfo mater = typeof(PlayerData).GetField("MATER", BindingFlags.Public | BindingFlags.Instance);

			Debug.Assert(con != null, "CON is null");
			Debug.Assert(data != null, "CURRENT_PL_DATA is null");
			Debug.Assert(mater != null, "MATER is null");

			foreach (CodeInstruction instruction in instructions)
			{
				// if (MATER[j] == "")
				if (step == 0) 
				{
					// brfalse IL_0085
					if (instruction.opcode == OpCodes.Brfalse)
					{
						step++;
						label = (Label)instruction.operand;
					}
				}
				else if (step == 1)
				{
					step++;

					// if (MATER[j] != null)
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldfld, con);
					yield return new CodeInstruction(OpCodes.Ldfld, data);
					yield return new CodeInstruction(OpCodes.Ldfld, mater);
					yield return new CodeInstruction(OpCodes.Ldloc_1);
					yield return new CodeInstruction(OpCodes.Ldelem_Ref);
					yield return new CodeInstruction(OpCodes.Brfalse, label);
				}

				yield return instruction;
			}

			if (step != 2)
			{
				Logger.LogError("Transpiling Alki.Set failed");
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Alki), "FORGE")]
		internal static IEnumerable<CodeInstruction> AlkiForgeTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			int step = 0;
			CodeInstruction branch = null;
			MethodInfo checkZoneIndex = GetMethodInfo<System.Func<Alki, int, bool>>(CheckZoneIndex);
			
			foreach (CodeInstruction instruction in instructions)
			{
				if (step == 0)
				{
					// zONE_.Substring(...) IL_02be
					if (instruction.opcode == OpCodes.Ldloc_S)
					{
						step++;

						// if (num2 < zONE_.Length)
						yield return new CodeInstruction(OpCodes.Ldarg_0); // Alki
						yield return new CodeInstruction(OpCodes.Ldloc_3);  // num2
						yield return new CodeInstruction(OpCodes.Call, checkZoneIndex);
						yield return branch = new CodeInstruction(OpCodes.Brfalse);
					}
				}
				else if (step == 1)
				{
					// if (zONE_ == ...) IL_0306
					if (instruction.opcode == OpCodes.Brfalse)
					{
						step++;

						branch.operand = instruction.operand;
					}
				}

				yield return instruction;
			}

			if (step != 2)
			{
				Logger.LogError("Transpiling Alki.FORGE failed - Step: " + step);
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Menus), "SortArray")]
		internal static IEnumerable<CodeInstruction> MenusSortArrayTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			bool found = false;
			MethodInfo sortWeapons = typeof(Lunatic).GetMethod("Internal_SortList", BindingFlags.Public | BindingFlags.Static);

			foreach (CodeInstruction instruction in instructions)
			{
				if (!found)
				{
					// list.Sort()
					if (instruction.opcode == OpCodes.Callvirt &&
						instruction.operand is MethodInfo methodInfo &&
						methodInfo.Name == "Sort")
					{
						found = true;

						// Lunatic.SortWeapons(list)
						yield return new CodeInstruction(OpCodes.Call, sortWeapons);

						continue;
					}
				}

				yield return instruction;
			}

			if (!found)
			{
				Logger.LogError("Transpiling Menus.SortArray failed");
				PrintILCode(instructions);
			}
		}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Menus), "LoadText")]
		internal static IEnumerable<CodeInstruction> MenusLoadTextTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
		{
			int step = 0;
			Label label = default;
			MethodInfo checkIngredientCounts = typeof(Lunatic).GetMethod("Internal_CheckIngredientCounts", BindingFlags.Public | BindingFlags.Static);
			FieldInfo eqSlot = typeof(Menus).GetField("EQ_SLOT", BindingFlags.NonPublic | BindingFlags.Instance);
			Label continueLabel = generator.DefineLabel();
			LocalBuilder num51 = null;

			Debug.Assert(checkIngredientCounts != null, "checkIngredientCounts is null");
			Debug.Assert(eqSlot != null, "eqSlot is null");

			foreach (CodeInstruction instruction in instructions)
			{
				if (step == 0)
				{
					// switch (text2load)
					if (instruction.opcode == OpCodes.Switch)
					{
						step++;

						// case 13 IL_27d7
						Label[] labels = (Label[])instruction.operand;
						label = labels[13];
					}
				}
				else if (step == 1)
				{
					// for (int n = 1...) IL_27d7
					if (instruction.labels.Contains(label))
						step++;
				}
				else if (step == 2)
				{
					// TextMeshProUGUI.text = "Nothing" IL_284e
					if (instruction.opcode == OpCodes.Ldstr &&
						(string)instruction.operand == "Nothing")
						step++;
				}
				else if (step == 3)
				{
					// for (int num51 = 0...) IL2859
					if (instruction.opcode == OpCodes.Stloc_S)
					{
						step++;
						num51 = (LocalBuilder)instruction.operand;
					}
				}
				else if (step == 4)
				{
					// for (int num51 = 0...) IL285b
					if (instruction.opcode == OpCodes.Br)
						step++;
				}
				else if (step == 5)
				{
					// Object.Instantiate(...) IL_2891
					if (instruction.opcode == OpCodes.Ldfld &&
						((FieldInfo)instruction.operand).Name == "ITEMS")
					{
						step++;

						// previous instruction is ldarg.0

						// if (!Lunatic.Internal_CheckIngredientCounts(this, num20, EQ_SLOT))
						//	continue
						yield return new CodeInstruction(OpCodes.Ldloc_S, num51);               // num51
						yield return new CodeInstruction(OpCodes.Ldarg_0);                      // this.
						yield return new CodeInstruction(OpCodes.Ldfld, eqSlot);                // EQ_SLOT
						yield return new CodeInstruction(OpCodes.Call, checkIngredientCounts);  // Lunatic.Internal_CheckIngredientCounts
						yield return new CodeInstruction(OpCodes.Brfalse, continueLabel);       // if false goto continueLabel
						yield return new CodeInstruction(OpCodes.Ldarg_0);                      // this (since we are using the previous instruction)
					}
				}
				else if (step == 6)
				{
					// TMP_Text.set_text(...) IL_2917
					if (instruction.opcode == OpCodes.Callvirt &&
						((MethodInfo)instruction.operand).Name == "set_text")
						step++;
				}
				else if (step == 7)
				{
					// ldloc.s 75 IL291c
					if (instruction.opcode == OpCodes.Ldloc_S &&
						(LocalBuilder)instruction.operand == num51)
					{
						step++;

						instruction.labels.Add(continueLabel);
					}
				}

				yield return instruction;
			}

			if (step != 8)
			{
				Logger.LogError("Transpiling Menus.LoadText failed - Step: " + step);
				PrintILCode(instructions);
			}
		}

		internal static bool CheckZoneIndex(Alki alki, int index)
		{
			return index < alki.CON.CURRENT_PL_DATA.ZONE_8.Length;
		}

		private static void PrintILCode(IEnumerable<CodeInstruction> instructions)
		{
			foreach (CodeInstruction instruction in instructions)
				Logger.LogInfo($"{instruction.opcode} - {instruction.operand}");
		}

		private static MethodInfo GetMethodInfo<T>(T del) where T : System.Delegate
		{
			return del.Method;
		}
	}
}