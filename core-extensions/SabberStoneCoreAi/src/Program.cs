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

namespace SabberStoneCoreAi
{
	internal class Program
	{

		private static void Main()
		{
			//END TESTING

			Console.WriteLine("Setup gameConfig");

			List<Card> d;
			//d = new List<Card>() { Cards.FromId("EX1_277"), Cards.FromId("CS2_171"), Cards.FromId("CS2_106") };
			d = Enumerable.Repeat(Cards.FromId("EX1_277"), 30).ToList(); //arcane missles
			d = Enumerable.Repeat(Cards.FromId("CS2_171"), 30).ToList(); //stonetusk boar

			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1HeroClass = CardClass.MAGE,
				Player2HeroClass = CardClass.HUNTER,
				FillDecks = true,
				Shuffle = true,
				Logging = false,
				Player1Deck = d
			};

			Console.WriteLine("Setup POGameHandler");
			DLAgent player1 = new DLAgent(0.0f);
			AbstractAgent player2 = new FaceHunter();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");
			//gameHandler.PlayGame();
			gameHandler.PlayGame();
			GameStats gameStats = gameHandler.getGameStats();

			//finalize the records
			List<GameRecord.TransitionRecord> records = player1.GetRecords().ConstructTransitions(player1.scorer, gameStats.PlayerA_Wins > 0);

			GameRecord.TransitionRecord r = records[0];

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}
	}
}
