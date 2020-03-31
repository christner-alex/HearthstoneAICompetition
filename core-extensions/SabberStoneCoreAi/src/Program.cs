#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using NumSharp;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCoreAi.Agent;
using SabberStoneCoreAi.Agent.DLAgent;
using SabberStoneCoreAi.Agent.ExampleAgents;
using SabberStoneCoreAi.POGame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Npgsql;

namespace SabberStoneCoreAi
{
	internal class Program
	{

		private static void Main()
		{
			//END TESTING

			Console.WriteLine("Setup gameConfig");

			List<Card> d;
			d = null;
			//d = new List<Card>() { Cards.FromId("EX1_277"), Cards.FromId("CS2_171"), Cards.FromId("CS2_106") };
			//d = Enumerable.Repeat(Cards.FromId("EX1_277"), 30).ToList(); //arcane missles
			//d = Enumerable.Repeat(Cards.FromId("CS2_171"), 30).ToList(); //stonetusk boar
			//d = Enumerable.Repeat(Cards.FromId("CS2_023"), 30).ToList(); //arcane intellect
			//d = Enumerable.Repeat(Cards.FromId("CS2_023"), 10).Concat(//arcane intellect
			//	Enumerable.Repeat(Cards.FromId("EX1_008"), 10)).Concat(//argent squire
			//	Enumerable.Repeat(Cards.FromId("CS2_029"), 10)).ToList();//fireball
			//d = Enumerable.Repeat(Cards.FromId("BOT_101"), 30).ToList(); //astral rift

			/*
			NDArray nd1 = np.array(1, 2, 3, 4, 5);
			NDArray nd2 = np.array(1, 2, 3, 4, 5);
			NDArray nd3 = np.array(1, 2, 3, 4, 5, 6);
			int[] arr1 = nd1.ToArray<int>();
			int[] arr2 = nd2.ToArray<int>();
			int[] arr3 = nd3.ToArray<int>();
			Console.WriteLine(arr1);
			Console.WriteLine(arr2);
			Console.WriteLine(nd1.Equals(nd2));
			Console.WriteLine(nd1.Equals(nd3));
			Console.WriteLine(arr1.Equals(arr2));
			Console.WriteLine(((IStructuralEquatable)arr1).GetHashCode(EqualityComparer<int>.Default));
			Console.WriteLine(((IStructuralEquatable)arr2).GetHashCode(EqualityComparer<int>.Default));
			Console.WriteLine(((IStructuralEquatable)arr3).GetHashCode(EqualityComparer<int>.Default));
			*/

			/*
			string connstring = "Host=localhost; Database=example_db; Username=example_user; password=example_password";
			NpgsqlConnection conn = new NpgsqlConnection(connstring);
			conn.Open();

			string query = "SELECT * FROM course";

			var cmd = new NpgsqlCommand(query, conn);

			NpgsqlDataReader rdr = cmd.ExecuteReader();

			while(rdr.Read())
			{
				Console.WriteLine("{0} {1} {2} {3} {4}", rdr.GetString(0), rdr.GetString(1), rdr.GetString(2), rdr.GetString(3), rdr.GetInt32(4));
			}

			rdr.Close();
			
			*/

			Trainer trainer = new Trainer();
			trainer.RunTrainingLoop(0,3);

			Console.ReadLine();
		}
	}
}
