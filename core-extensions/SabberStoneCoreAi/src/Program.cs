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
			/*
			string test_str = "hello there hello there";
			Match test_match = Regex.Match(test_str, "ello*");

			List<Card> deck = new List<Card>();
			//deck.Add(Cards.FromName("Arcane Missiles"));
			//deck.Add(Cards.FromName("Stonetusk Boar"));
			//deck.Add(Cards.FromName("Mind Control Tech"));
			//deck.Add(Cards.FromName("Bloodfen Raptor"));
			deck.Add(Cards.FromName("Loot Hoarder"));
			deck.Add(Cards.FromName("Arcane Intellect"));
			deck.Add(Cards.FromName("Fireball"));
			deck.Add(Cards.FromName("Pyroblast"));
			deck.Add(Cards.FromName("Lightning Storm"));

			foreach (Card c in deck)
			{
				string text = c.Text;
				if(text!=null)
				{
					text = text.ToLower();
					//text = "deal two damage";
					Console.WriteLine(text);
					Match draw_match = Regex.Match(text, "draw\\s.*\\scard");
					Match damage_match = Regex.Match(text, "deal\\s.*\\sdamage");
					if (draw_match.Success)
					{
						int draw_amount = 0;
						string amount = draw_match.Value.Split(" ")[1];
						if (amount.Equals("a")) draw_amount = 1;
						else if(amount.Any(char.IsDigit)) draw_amount = Int32.Parse(Regex.Match(amount, "[0-9]+").Value);
						Console.WriteLine(draw_amount);
					}
					if (damage_match.Success)
					{
						string middle = damage_match.Value.Split(" ")[1];
						int damage = 0;
						if (middle.Equals("a")) damage = 1;
						else if (middle.Any(char.IsDigit)) damage = Int32.Parse(Regex.Match(middle, "[0-9]+").Value);
						Console.WriteLine(middle);
						Console.WriteLine(damage);
					}
				}
				else
				{
					Console.WriteLine("no text");

				}
			}
			*/

			/*
			ReplayMemoryDB db2 = new ReplayMemoryDB();
			db2.Load();
			GameRecord.TransitionRecord[] trans2 = db2.Sample(3);
			db2.Close();

			GameEvalDQN network = new GameEvalDQN();
			network.StartSession();
			network.Initialize();

			DLAgent me = new DLAgent(new Scorer(network), turn_secs: 200f);
			DLAgent opp = new DLAgent(new Scorer(network), turn_secs: 200f);

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

			network.EndSession();

			//List<GameRecord.TransitionRecord> records = me.Record.ConstructTransitions(stats.PlayerA_Wins > stats.PlayerB_Wins);
			List<GameRecord.TransitionRecord> records = GameRecord.ConstructTransitions(me.Record, opp.Record, stats.PlayerA_Wins > stats.PlayerB_Wins);

			ReplayMemoryDB db = new ReplayMemoryDB();
			//db.Load();
			db.Push(records);

			GameRecord.TransitionRecord[] trans = db.Sample(3);
			db.Close();
			*/

			Trainer trainer = new Trainer();
			//trainer.Warmup(6, false);
			//trainer.Warmup(6, true);
			trainer.Warmup(300, true);
			//trainer.RunTrainingLoop(1,3);

			Console.ReadLine();
		}
	}
}
