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
using System.IO;
using SabberStoneCoreAi.Meta;
using SabberStoneCoreAi.Competition.Agents;
using SabberStoneCoreAi.Tyche2;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Trainer
	{
		Random rnd;

		private Array classes;

		private GameEvalDQN network;

		private ReplayMemoryDB replayMemory;

		private const int numDataThreads = 1;

		private const int trainItr = 1;
		private const int saveItr = 5;
		private const int copyItr = 25;
		private const int testItr = 50;

		private const int batchSize = 50;
		private const int numTrainLoops = 5;

		private const float max_eps = 0.5f;
		private const float min_eps = 0.01f;
		private const int epsDecaySteps = 1000;
		private float currentEps = 0.5f;

		private const int numTestGamesPerBot = 20;
		private const int numTestThreads = 1;
		private List<TestingParams> testingParams;

		private bool stop;

		private const string EXCEPTION_LOG_FILENAME = "exception_log.txt";
		private const string BENCHMARK_LOG_FILENAME = "benchmark_log.txt";
		private const string TRAINING_LOG_FILENAME = "training_log.txt";
		private Mutex logMutex;

		public Trainer()
		{
			rnd = new Random();

			classes = Enum.GetValues(typeof(CardClass));

			network = new GameEvalDQN();

			replayMemory = new ReplayMemoryDB();

			stop = false;

			List<(List<Card>, CardClass)> testDecks = new List<(List<Card>,CardClass)>(){
				(Decks.MidrangeJadeShaman, CardClass.SHAMAN),
				(Decks.AggroPirateWarrior, CardClass.WARRIOR),
				(Decks.RenoKazakusMage, CardClass.MAGE)
			};

			List<(Func<AbstractAgent>, string)> testAgents = new List<(Func<AbstractAgent>, string)>(){
				(() => new BotHeimbrodt(), "DynamicLookahead"),
				(() => new AlvaroAgent(), "MCTS_Alvaro"),
				(() => new BestChildAgent(), "GreedyLookahead"),
				(() => new TycheAgentB(), "MCTS_Tyche")
			};

			//generate the testing game parameters
			testingParams = new List<TestingParams>(testAgents.Count * testDecks.Count * testDecks.Count);
			foreach ((Func<AbstractAgent>, string) makeAgent in testAgents)
			{
				foreach ((List<Card>, CardClass) pDeck in testDecks)
				{
					foreach ((List<Card>, CardClass) oDeck in testDecks)
					{
						GameConfig conf = new GameConfig()
						{
							Player1HeroClass = pDeck.Item2,
							Player2HeroClass = oDeck.Item2,
							Player1Deck = pDeck.Item1,
							Player2Deck = oDeck.Item1,
							Shuffle = true,
							Logging = false
						};
						TestingParams p = new TestingParams(makeAgent.Item1, conf, $"DLAgent:{Enum.GetName(typeof(CardClass), pDeck.Item2)}", $"{makeAgent.Item2}:{Enum.GetName(typeof(CardClass), oDeck.Item2)}");
						testingParams.Add(p);
					}
				}
			}

			logMutex = new Mutex();
		}

		private void StopIO()
		{
			while (true)
			{
				string input = Console.ReadLine();
				if (input.Equals("stop"))
				{
					stop = true;
					break;
				}
			}
		}

		/// <summary>
		/// Initialize the Network and the ReplayMemory
		/// </summary>
		/// <param name="load">If true, the network and the replay memory will be loaded from previous iterations. If false, they will be initialized from scratch.</param>
		private void InitializeObjects(bool load)
		{
			network.StartSession();

			if (!load)
			{
				//if starting from scratch, initialize the model randomly
				network.Initialize();
			}
			else
			{
				//otherwise, load the corresponding checkpoint
				network.LoadModel();

				//reload the replay buffer
				replayMemory.Initialize();
			}
		}

		/// <summary>
		/// Save the model, end the network session, and close the replay memory
		/// </summary>
		/// <param name="it">The iteration checkpoint to save the model as</param>
		private void Finalize(int it)
		{
			network.SaveModel(it);
			network.EndSession();
			replayMemory.Close();
		}

		/// <summary>
		/// Populate the replay buffer with numGames games
		/// </summary>
		/// <param name="numGames">The number of games to play and add to the replay buffer</param>
		/// <param name="load">Whether to load a previously saved model and replay buffer</param>
		public void Warmup(int numGames, bool load)
		{
			InitializeObjects(load);

			network.CopyOnlineToTarget(); //temporary

			try
			{
				Thread StopThread = new Thread(StopIO);
				StopThread.Start();

				Thread[] threads = new Thread[numDataThreads];
				int numGamesPerThread = (int)(numGames / numDataThreads);
				if(numGamesPerThread<=0)
				{
					throw new ArgumentException();
				}

				for (int i = 0; i < numDataThreads; i++)
				{
					threads[i] = new Thread(LoopTrainingGames);
					threads[i].Start(numGamesPerThread);
				}

				//wait for each training thread to join
				foreach (Thread t in threads)
				{
					t.Join();
					Console.WriteLine("Thread Joined");
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine("Trainer Error");
				LogException(ex);
			}
			finally
			{
				Finalize(0);
			}
		}

		public void RunTrainingLoop(int startIter, int stopItr = int.MaxValue)
		{
			int it = Math.Max(1, startIter);

			//initialize the objects, making sure to load a model and replay memory
			//already created from warmups
			InitializeObjects(true);

			try
			{
				Thread StopThread = new Thread(StopIO);
				StopThread.Start();

				while (it < stopItr)
				{
					//update the epsilon exploration parameter
					currentEps = Math.Max(min_eps, max_eps - (max_eps - min_eps) * it / epsDecaySteps);

					//play training games
					Console.WriteLine("============================");
					Console.WriteLine("Iteration {0}", it);
					Console.WriteLine("Playing Training Games");

					//stop if the io thread is done
					if (stop) break;

					//launch threads to play training games
					Thread[] threads = new Thread[numDataThreads];
					for (int i = 0; i < numDataThreads; i++)
					{
						threads[i] = new Thread(LoopTrainingGames);
						threads[i].Start(1);
					}

					//wait for each training thread to join
					foreach (Thread t in threads)
					{
						t.Join();
					}

					//run update
					if(it % trainItr == 0)
					{
						Console.WriteLine("Training Network");
						TrainNetwork(it);
					}

					//at regular intervals, copy the ops
					if (it % copyItr == 0)
					{
						Console.WriteLine("Copying online to target");
						network.CopyOnlineToTarget();
					}

					//at regular intervals, save the model
					if (it % saveItr == 0)
					{
						Console.WriteLine("Saving Model");
						network.SaveModel(it);
					}

					//at regular intervals, test the model on other agents
					if (it % testItr == 0)
					{
						Console.WriteLine("Testing against other bots");
						logMutex.WaitOne();
						using (StreamWriter w = File.AppendText(BENCHMARK_LOG_FILENAME))
						{
							w.WriteLine();
							w.WriteLine("====================================================");
							w.WriteLine();
							w.WriteLine("Training Iteration {0}", it);
							w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
						}
						logMutex.ReleaseMutex();

						//create threads for testing
						Thread[] testThreads = new Thread[numTestThreads];
						for(int j=0; j<numTestThreads; j++)
						{
							testThreads[j] = new Thread(TestingLoop);
							testThreads[j].Start(j);
						}

						//wait for each testing thread to join
						foreach (Thread t in testThreads)
						{
							t.Join();
						}
					}

					it++;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Trainer Error");
				LogException(ex);
			}
			finally
			{
				Console.WriteLine("Final Iteration: {0}", it);
				Finalize(it);
			}
		}

		public GameStats PlayGame(AbstractAgent player1, AbstractAgent player2, GameConfig gameConfig)
		{
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);
			bool valid = gameHandler.PlayGame();
			return valid ? gameHandler.getGameStats() : null;
		}

		private void TrainingGame()
		{
			try
			{
				DLAgent agent1 = new DLAgent(new Scorer(network), eps: currentEps);
				DLAgent agent2 = new DLAgent(new Scorer(network), eps: currentEps);

				var gameConfig = new GameConfig()
				{
					Player1HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)), //random classes
					Player2HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)),
					FillDecks = true,
					Shuffle = true,
					Logging = false
				};

				GameStats gameStats = PlayGame(agent1, agent2, gameConfig);

				//discard drawn games or games with exceptions
				if(gameStats == null || gameStats.PlayerA_Wins == gameStats.PlayerB_Wins || gameStats.PlayerA_Exceptions > 0 || gameStats.PlayerB_Exceptions > 0)
				{
					Console.WriteLine("Discarding game");
					return;
				}

				//save the transitions
				List<GameRecord.TransitionRecord> p1Records = agent1.Record.ConstructTransitions(agent1.scorer, gameStats.PlayerA_Wins > gameStats.PlayerB_Wins);
				List<GameRecord.TransitionRecord> p2Records = agent2.Record.ConstructTransitions(agent2.scorer, gameStats.PlayerB_Wins > gameStats.PlayerA_Wins);
				replayMemory.Push(p1Records);
				replayMemory.Push(p2Records);
			}
			catch(Exception ex)
			{
				Console.WriteLine("Agent Error");
				LogException(ex);
			}

		}

		private void LoopTrainingGames(object gamesPerTrainThread)
		{
			Console.WriteLine($"{(int)gamesPerTrainThread} GameStats per thread");
			for (int i=0; i<(int)gamesPerTrainThread; i++)
			{
				TrainingGame();
				Console.WriteLine("Finished Game");

				if (stop) break;
			}
		}

		private void TrainNetwork(int it)
		{
			Scorer scorer = new Scorer(network);

			for (int l = 0; l < numTrainLoops; l++)
			{
				//sample transitions
				GameRecord.TransitionRecord[] transitions = replayMemory.Sample(batchSize);

				//construct the targets
				NDArray targets = scorer.CreateTargets(transitions);

				//get the actions
				GameRep[] actions = (from t in transitions select t.action).ToArray();

				//train the network
				float loss = network.TrainStep(actions, targets);

				string output = $"Iteration {it}, Batch {l+1}: Loss = {loss}";
				Console.WriteLine(output);

				logMutex.WaitOne();
				using (StreamWriter w = File.AppendText(TRAINING_LOG_FILENAME))
				{
					w.WriteLine(output);
					w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
				}
				logMutex.ReleaseMutex();
			}
		}

		private void TestingLoop(object obj)
		{
			for(int i=(int)obj; i<testingParams.Count; i+=numTestThreads)
			{
				TestingParams p = testingParams[i];

				int wins = 0;
				int games = 0;
				float winrate = 0f;

				try
				{
					//run games
					for (int g = 0; g < numTestGamesPerBot; g++)
					{
						GameStats gameStats = PlayGame(new DLAgent(new Scorer(network)), p.opponent(), p.gameConfig);
						if (gameStats != null && gameStats.PlayerA_Exceptions == 0 && gameStats.PlayerB_Exceptions == 0)
						{
							games++;
							if (gameStats.PlayerA_Wins > 0) wins++;
						}
					}

					if (games > 0)
					{
						winrate = wins / games;

						string result = $"{p.playerName} vs {p.opponentName}: Winrate={winrate}%, {wins}/{games}";
						Console.WriteLine(result);
						logMutex.WaitOne();
						using (StreamWriter w = File.AppendText(BENCHMARK_LOG_FILENAME))
						{
							w.WriteLine(result);
						}
						logMutex.ReleaseMutex();
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Testing Error");
					LogException(ex);
				}
			}
		}

		private struct TestingParams
		{
			public Func<AbstractAgent> opponent;
			public GameConfig gameConfig;
			public string playerName;
			public string opponentName;

			public TestingParams(Func<AbstractAgent> opp, GameConfig conf, string play_name, string opp_name)
			{
				opponent = opp;
				gameConfig = conf;
				playerName = play_name;
				opponentName = opp_name;
			}
		}

		private void LogException(Exception ex)
		{
			logMutex.WaitOne();

			using (StreamWriter w = File.AppendText(EXCEPTION_LOG_FILENAME))
			{
				w.WriteLine("====================================================");
				w.WriteLine();
				w.WriteLine($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}");
				w.WriteLine(ex.GetType());
				w.WriteLine(ex.Message);
				w.WriteLine(ex.StackTrace);
				w.WriteLine();
			}

			logMutex.ReleaseMutex();
		}
	}
}
