using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace LunaticBridge
{
	[HarmonyPatch]
	public static class LunaticPatch40
	{
		public static ManualLogSource Logger;

		//[HarmonyTranspiler]
		//[HarmonyPatch(typeof(Alki), "Reset")]
		//internal static IEnumerable<CodeInstruction> AlkiResetTranspiler(IEnumerable<CodeInstruction> instructions)
		//{
		//	bool found = false;

		//	FieldInfo recipesFI = typeof(Alki).GetField("Recipes");

		//	foreach (CodeInstruction instruction in instructions)
		//	{
		//		// safe because ldc_I4 implies an int operand
		//		// Alki.ValidRecipes = new Alki.Recipe[128] (partial)
		//		if (!found && instruction.opcode == OpCodes.Ldc_I4 && (int)instruction.operand == 128)
		//		{
		//			// Alki.Recipes.Length
		//			yield return new CodeInstruction(OpCodes.Ldarg_0);
		//			yield return new CodeInstruction(OpCodes.Ldfld, recipesFI);
		//			yield return new CodeInstruction(OpCodes.Ldlen);
		//			yield return new CodeInstruction(OpCodes.Conv_I4);

		//			found = true;
		//			continue;
		//		}

		//		yield return instruction;
		//	}

		//	if (!found)
		//	{
		//		Logger.LogError("Transpiling Alki.Reset failed");
		//		PrintILCode(instructions);
		//	}
		//}

		[HarmonyTranspiler]
		[HarmonyPatch(typeof(Save), "LOAD_FILE")]
		internal static IEnumerable<CodeInstruction> SaveLoadFileTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			bool found = false;
			MethodInfo onPlayerDataLoad = typeof(Lunatic).GetMethod("Internal_OnPlayerDataLoad", BindingFlags.Public | BindingFlags.Static);

			foreach (CodeInstruction instruction in instructions)
			{
				// return result
				if (!found && instruction.opcode == OpCodes.Ret)
				{
					found = true;

					// Lunatic.Internal_OnPlayerDataSave returns the same player data to preserve the stack value
					// as the player data is never assigned to a local slot in Save.LOAD_FILE
					// return Lunatic.Internal_OnPlayerDataSave(result)
					yield return new CodeInstruction(OpCodes.Call, onPlayerDataLoad);
				}

				yield return instruction;
			}

			if (!found)
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

					// Lunatic.Internal_OnPlayerDataSave(graph)
					yield return new CodeInstruction(OpCodes.Ldloc_1);
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

			Debug.Assert(onItemPickupStart != null, $"{nameof(onItemPickupStart)} is null");

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
					// zONE_.Substring(...)
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
			MethodInfo sortWeapons = typeof(Lunatic).GetMethod("SortWeapons", BindingFlags.Public | BindingFlags.Static);

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