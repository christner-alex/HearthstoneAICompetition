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
using System.Collections.Concurrent;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Trainer
	{
		Random rnd;

		private Array classes;

		private GameEvalNN network;

		private BlockingCollection<GameRecord.TransitionRecord> transitions;

		private const int numTreads = 5;
		private const int gamesPerThread = 1;

		private const int trainItr = 1;
		private const int saveItr = 5;
		private const int copyItr = 50;
		private const int testItr = 100;
		private const int warmupLoops = 1000;

		private const int batchSize = 50;
		private const int numTrainLoops = 10;

		private const int memoryBufferSize = 1000;

		private const float max_eps = 1.0f;
		private const float min_eps = 0.1f;
		private const int epsDecaySteps = 100000;
		private float currentEps = 1.0f;

		public float Gamma { get; set; }

		public Trainer(float gamma = 0.99f)
		{
			rnd = new Random();

			classes = Enum.GetValues(typeof(CardClass));

			network = new GameEvalNN();

			transitions = new BlockingCollection<GameRecord.TransitionRecord>();

			Gamma = gamma;
		}

		public void RunTrainingLoop()
		{
			network.StartSession();
			//network.Initialize();
			network.LoadModel();

			int it = 0;

			try
			{
				while(true)
				{
					currentEps = Math.Max(min_eps, max_eps - (max_eps - min_eps) * it / epsDecaySteps);

					//play training games
					Thread[] threads = new Thread[numTreads];
					for (int i = 0; i < numTreads; i++)
					{
						threads[i] = new Thread(LoopTrainingGames);
						threads[i].Start();
					}

					foreach (Thread t in threads)
					{
						t.Join();
					}

					//if warmup has not been completed, play more training games
					if(it < warmupLoops)
					{
						continue;
					}

					//run update
					if(it % trainItr == 0)
					{
						TrainNetwork();
					}

					//at regular intervals, save the model
					if (it % saveItr == 0)
					{
						network.SaveModel(it);
					}

					//at regular intervals, copy the ops
					if (it % copyItr == 0)
					{
						network.CopyOnlineToTarget();
					}

					//at regular intervals, test the model
					if (it % testItr == 0)
					{

					}

					it++;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			transitions.Dispose();
			network.EndSession();

			//test on other agents
		}

		public GameStats PlayGame(AbstractAgent player1, AbstractAgent player2, GameConfig gameConfig)
		{
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			gameHandler.PlayGame();
			return gameHandler.getGameStats();
		}

		private (List<GameRecord.TransitionRecord>, List<GameRecord.TransitionRecord>) TrainingGame()
		{
			try
			{
				DLAgent agent1 = new DLAgent(new Scorer(network, Gamma));
				DLAgent agent2 = new DLAgent(new Scorer(network, Gamma));

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

				//save the transitions
				List<GameRecord.TransitionRecord> p1Records = agent1.Record.ConstructTransitions(agent1.scorer, gameStats.PlayerA_Wins > 0);
				List<GameRecord.TransitionRecord> p2Records = agent1.Record.ConstructTransitions(agent1.scorer, gameStats.PlayerB_Wins > 0);
				foreach (GameRecord.TransitionRecord rec in p1Records.Concat(p2Records))
				{
					transitions.Add(rec);
				}

				return (p1Records, p2Records);
			}
			catch(Exception ex)
			{
				//log the exception
			}

			return (null, null);

		}

		private void LoopTrainingGames()
		{
			for(int i=0; i<gamesPerThread; i++)
			{
				TrainingGame();
			}
		}

		private void TrainNetwork()
		{
			//sample transitions

			//construct the targets
			Scorer scorer = new Scorer(network, Gamma);
			NDArray targets = scorer.CreateTargets(transitions.ToArray());

			//get the actions
			GameRep[] actions = (from t in transitions select t.action).ToArray();

			//train the network
			network.TrainStep(actions, targets);
		}

		public GameStats TestingGame(DLAgent dlAgent, AbstractAgent otherAgent, GameConfig gameConfig)
		{
			GameStats gameStats = PlayGame(dlAgent, otherAgent, gameConfig);

			return gameStats;
		}
	}
}
