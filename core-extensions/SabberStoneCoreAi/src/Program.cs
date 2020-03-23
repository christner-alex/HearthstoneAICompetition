﻿#region copyright
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

			Trainer trainer = new Trainer();
			trainer.RunTrainingLoop();

			Console.ReadLine();
		}
	}
}
