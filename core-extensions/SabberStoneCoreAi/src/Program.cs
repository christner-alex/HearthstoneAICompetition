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

			List<Card> d = new List<Card>() { Cards.FromId("EX1_277"), Cards.FromId("CS2_171"), Cards.FromId("CS2_106") };

			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1HeroClass = CardClass.MAGE,
				Player2HeroClass = CardClass.HUNTER,
				FillDecks = false,
				Shuffle = true,
				Logging = false,
				Player1Deck = d
			};

			Console.WriteLine("Setup POGameHandler");
			AbstractAgent player1 = new DLAgent();
			AbstractAgent player2 = new FaceHunter();
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			Console.WriteLine("Simulate Games");
			//gameHandler.PlayGame();
			gameHandler.PlayGames(nr_of_games: 1, addResultToGameStats: true, debug: false);
			GameStats gameStats = gameHandler.getGameStats();

			gameStats.printResults();

			Console.WriteLine("Test successful");
			Console.ReadLine();
		}
	}
}
