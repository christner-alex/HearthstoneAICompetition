using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	public static class Utils
	{
		public static double GetSecondsSinceStart()
		{
			return Environment.TickCount / 1000.0;
		}

		public static float Lerp(float a, float b, float t)
		{
			return (1.0f - t) * a + t * b;
		}

		public static float InverseLerp(float value, float min, float max)
		{
			return (value - min) / (max - min);
		}

		public static float RandFloat(this Random r)
		{
			return (float) r.NextDouble();
		}

		public static float RandFloat(this Random r, float min, float max)
		{
			var randFloat = (float) r.NextDouble();
			return randFloat * (max - min) + min;
		}

		public static T GetUniformRandom<T>(this List<T> list)
		{
			return GetUniformRandom(list, new Random());
		}

		public static T GetUniformRandom<T>(this List<T> list, Random random)
		{
			return list[random.Next(list.Count)];
		}

		public static T GetUniformRandom<T>(this List<T> list, Random random, int count)
		{
			return list[random.Next(count)];
		}

		public static T PopRandElement<T>(this List<T> list, Random random)
		{
			var id = random.Next(list.Count);
			var element = list[id];
			list.RemoveAt(id);
			return element;
		}

		public static string GetTypeNames<T>(this List<T> list, string separator = ", ")
		{
			var result = "";

			for (var i = 0; i < list.Count; i++)
			{
				var curSeparator = separator;

				if (i == list.Count - 1)
					curSeparator = "";

				result += list[i].GetType().Name + curSeparator;
			}

			return result;
		}

		/// <summary> BaseMana + TemporaryMana </summary>
		public static int GetAvailableMana(this Controller c)
		{
			return c.BaseMana + c.TemporaryMana - c.OverloadLocked;
		}

		/// <summary> BaseMana available in a turn. </summary>
		public static int GetBaseManaInTurn(int turn)
		{
			return Math.Min((turn + 1) / 2, 10);
		}

		public static Minion TryGetMinion(this PlayerTask task)
		{
			if (task != null && task.HasSource && task.Source is Minion)
				return task.Source as Minion;

			return null;
		}

		public static Spell TryGetSpell(this PlayerTask task)
		{
			if (task != null && task.HasSource && task.Source is Spell)
				return task.Source as Spell;

			return null;
		}

		public static Spell TryGetSecret(this PlayerTask task)
		{
			var spell = TryGetSpell(task);

			if (spell != null && spell.IsSecret)
				return spell;

			return null;
		}
	}
}
