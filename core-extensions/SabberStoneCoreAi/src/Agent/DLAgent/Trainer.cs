using NumSharp;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCoreAi.Agent.ExampleAgents;
using SabberStoneCoreAi.POGame;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Trainer
	{
		Random rnd;

		private Array classes;

		private GameEvalNN network;

		private const int numTreads = 1;
		private const int gamesPerThread = 1;

		public Trainer()
		{
			rnd = new Random();

			classes = Enum.GetValues(typeof(CardClass));

			network = new GameEvalNN();
		}

		public void RunTrainingLoop()
		{
			network.StartSession();
			network.Initialize();
			//network.LoadModel();

			try
			{
				//loop
				//play training games, sometimes play testing games in parallel
				Thread[] threads = new Thread[numTreads];
				for(int i=0; i<numTreads; i++)
				{
					threads[i] = new Thread(LoopTrainingGames);
					threads[i].Start(gamesPerThread);
				}

				foreach(Thread t in threads)
				{
					t.Join();
				}

				//run update
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			network.EndSession();

			//test on other agents
		}

		public GameStats PlayGame(AbstractAgent player1, AbstractAgent player2, GameConfig gameConfig)
		{
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			gameHandler.PlayGame();
			return gameHandler.getGameStats();
		}

		public (List<GameRecord.TransitionRecord>, List<GameRecord.TransitionRecord>) TrainingGame()
		{
			try
			{
				DLAgent agent1 = new DLAgent(network);
				DLAgent agent2 = new DLAgent(network);

				var gameConfig = new GameConfig()
				{
					StartPlayer = 1,
					Player1HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)), //random classes
					Player2HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)),
					FillDecks = true,
					Shuffle = true,
					Logging = false
				};

				GameStats gameStats = PlayGame(agent1, agent1, gameConfig);

				List<GameRecord.TransitionRecord> p1Records = agent1.Record.ConstructTransitions(agent1.scorer, gameStats.PlayerA_Wins > 0);
				List<GameRecord.TransitionRecord> p2Records = agent1.Record.ConstructTransitions(agent1.scorer, gameStats.PlayerB_Wins > 0);

				//save the transitions

				return (p1Records, p2Records);
			}
			catch(Exception ex)
			{
				//log the exception
			}

			return (null, null);

		}

		public void LoopTrainingGames(object itr)
		{
			for(int i=0; i<(int)itr; i++)
			{
				TrainingGame();
			}
		}

		public void TrainNetworkStep()
		{
			/*
			 * 

				NDArray p1Targets = scorer.CreateTargets(p1Records.ToArray());
				NDArray p2Targets = scorer.CreateTargets(p2Records.ToArray());

				GameRep[] p1Acts = (from r in p1Records select r.action).ToArray();
				GameRep[] p2Acts = (from r in p2Records select r.action).ToArray();

				network.TrainStep(p1Acts, p1Targets);
				network.TrainStep(p2Acts, p2Targets);

				network.SaveModel();
			*/

			//sample transitions

			//construct the targets

			//get the actions

			//train the network

			//save the model every so often
		}

		public GameStats TestingGame(DLAgent dlAgent, AbstractAgent otherAgent, GameConfig gameConfig)
		{
			GameStats gameStats = PlayGame(dlAgent, otherAgent, gameConfig);

			return gameStats;
		}
	}
}
