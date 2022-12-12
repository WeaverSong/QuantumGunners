using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace QuantumGunners
{
	public class IlCursor
	{
		public IlCursor(IEnumerable<CodeInstruction> instructions)
		{
			this.instructions = new List<CodeInstruction>(instructions);
		}

		public List<CodeInstruction> instructions;
		public int index = 0;

		public bool JumpBefore(Func<CodeInstruction, bool> check)
		{
			for (var i = index; i < instructions.Count; i++)
			{
				if (check(instructions[i]))
				{
					index = i;
					return true;
				}
			}

			return false;
		}
		public bool JumpPast(Func<CodeInstruction, bool> check)
		{
			for (var i = index; i < instructions.Count; i++)
			{
				if (check(instructions[i]))
				{
					index = i + 1;
					return true;
				}
			}

			return false;
		}

		public void Emit(CodeInstruction instruction)
		{
			instructions.Insert(index, instruction);
			index++;
		}


		public CodeInstruction[] GetInstructions()
		{
			return instructions.ToArray();
		}
	}
}