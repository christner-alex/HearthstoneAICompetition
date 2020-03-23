using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCoreAi.Agent.ExampleAgents;
using SabberStoneCoreAi.POGame;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Trainer
	{
		Random rnd;

		private Array classes;

		public Trainer()
		{
			rnd = new Random();

			classes = Enum.GetValues(typeof(CardClass));
		}

		public void RunTrainingLoop()
		{
			GameEvalNN network = new GameEvalNN();
			Scorer scorer = new Scorer(network);
			DLAgent agent1 = new DLAgent(scorer);
			DLAgent agent2 = new DLAgent(scorer);

			network.StartSession();
			network.Initialize();

			//loop
			//play training games
			TrainingGame(agent1, agent2);

			//run update

			//test on other agents

			network.EndSession();
		}

		public GameStats PlayGame(AbstractAgent player1, AbstractAgent player2, GameConfig gameConfig)
		{
			var gameHandler = new POGameHandler(gameConfig, player1, player2, repeatDraws: false);

			gameHandler.PlayGame();
			return gameHandler.getGameStats();
		}

		public (List<GameRecord.TransitionRecord>, List<GameRecord.TransitionRecord>) TrainingGame(DLAgent player1, DLAgent player2)
		{
			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)), //random classes
				Player2HeroClass = (CardClass)classes.GetValue(rnd.Next(2, 11)),
				FillDecks = true,
				Shuffle = true,
				Logging = false
			};

			GameStats gameStats = PlayGame(player1, player2, gameConfig);

			List<GameRecord.TransitionRecord> p1Records = player1.Record.ConstructTransitions(player1.scorer, gameStats.PlayerA_Wins > 0);
			List<GameRecord.TransitionRecord> p2Records = player2.Record.ConstructTransitions(player2.scorer, gameStats.PlayerB_Wins > 0);

			return (p1Records, p2Records);
		}

		public GameStats TestingGame(DLAgent dlAgent, AbstractAgent otherAgent, GameConfig gameConfig)
		{
			GameStats gameStats = PlayGame(dlAgent, otherAgent, gameConfig);

			return gameStats;
		}
	}
}
