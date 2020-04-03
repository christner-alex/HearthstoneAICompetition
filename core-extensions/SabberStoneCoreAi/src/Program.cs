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
using System.IO;
using SabberStoneCoreAi.Competition.Agents;
using System.Text.RegularExpressions;

namespace SabberStoneCoreAi
{
	internal class Program
	{

		private static void Main()
		{
			//ReplayMemoryDB db = new ReplayMemoryDB();
			//db.Initialize();
			//GameRecord.TransitionRecord[] trans = db.Sample(3);
			//db.Close();

			List<Card> deck = new List<Card>();
			deck.Add(Cards.FromName("Arcane Missiles"));
			deck.Add(Cards.FromName("Stonetusk Boar"));
			deck.Add(Cards.FromName("Mind Control Tech"));
			deck.Add(Cards.FromName("Bloodfen Raptor"));
			deck.Add(Cards.FromName("Loot Hoarder"));
			deck.Add(Cards.FromName("Arcane Intellect"));
			deck.Add(Cards.FromName("Fireball"));

			foreach (Card c in deck)
			{
				string text = c.Text;
				if(text!=null)
				{
					text = text.ToLower();
					Console.WriteLine(text);
					Match draw_match = Regex.Match(text, "draw\\s(a|[1-9])\\scard");
					Match damage_match = Regex.Match(text, "deal\\s[$][1-9]*\\sdamage");
					if (draw_match.Success)
					{
						string amount = draw_match.Value.Split(" ")[1];
						int draw_amount = amount.Equals("a") ? 1 : Int32.Parse(amount);
						Console.WriteLine(draw_amount);
					}
					if (damage_match.Success)
					{
						Console.WriteLine(Int32.Parse(damage_match.Value.Split(" ")[1].Remove(0,1)));
					}
				}
				else
				{
					Console.WriteLine("no text");

				}
			}

			DLAgent me = new DLAgent(new Scorer());
			BotHeimbrodt opp = new BotHeimbrodt();

			var gameConfig = new GameConfig()
			{
				Player1HeroClass = CardClass.MAGE, //random classes
				Player2HeroClass = CardClass.HUNTER,
				FillDecks = true,
				Shuffle = true,
				Logging = false
			};

			var gameHandler = new POGameHandler(gameConfig, me, opp, repeatDraws: false);
			bool valid = gameHandler.PlayGame();
			GameStats stats = gameHandler.getGameStats();
			stats.printResults();

			List<GameRecord.TransitionRecord> records = me.Record.ConstructTransitions(stats.PlayerA_Wins > stats.PlayerB_Wins);

			//ReplayMemoryDB db = new ReplayMemoryDB();
			//db.Initialize();
			//db.Push(records);

			//GameRecord.TransitionRecord[] trans = db.Sample(3);
			//db.Close();

			//Trainer trainer = new Trainer();
			//trainer.Warmup(1, false);
			//trainer.Warmup(1, true);
			//trainer.Warmup(100, true);
			//trainer.RunTrainingLoop(1,3);

			Console.ReadLine();
		}
	}
}
