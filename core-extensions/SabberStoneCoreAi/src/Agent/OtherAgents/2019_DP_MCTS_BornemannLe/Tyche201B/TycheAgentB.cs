using System;
using System.Collections.Generic;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCoreAi.Agent;
using SabberStoneCoreAi.Meta;


namespace SabberStoneCoreAi.Tyche2
{
	internal class TycheAgentB : AbstractAgent
	{
		public enum Algorithm
		{
			Greedy,
			SearchTree
		}

		//x% of episodes below this value, are used for exploration, the remaining are used for exploitation:
		private const double EXPLORE_TRESHOLD = 0.75;
		private const int DEFAULT_NUM_EPISODES_MULTIPLIER = 200;
		private const int LEARNING_NUM_EPISODES_MULTIPLIER = 20;

		private readonly StateAnalyzer _analyzer;
		private readonly int _defaultEpisodeMultiplier;
		private readonly bool _heroBasedWeights;

		private readonly Random _random;
		private readonly SimTree _simTree;
		private int _curEpisodeMultiplier;
		private bool _hasInitialized;

		private bool _isTurnBegin = true;

		private double _turnTimeStart;
		public bool AdjustEpisodeMultiplier;

		public Algorithm algoUsed = Algorithm.SearchTree;
		public bool PrintTurnTime = false;

		public TycheAgentB()
			: this(StateWeights.GetDefault(), true, DEFAULT_NUM_EPISODES_MULTIPLIER, true)
		{
		}

		private TycheAgentB(StateWeights weights, bool heroBasedWeights, int episodeMultiplier,
			bool adjustEpisodeMultiplier)
		{
			_defaultEpisodeMultiplier = episodeMultiplier;
			_curEpisodeMultiplier = episodeMultiplier;
			_heroBasedWeights = heroBasedWeights;

			_analyzer = new StateAnalyzer(weights);
			_simTree = new SimTree();
			_random = new Random();

			AdjustEpisodeMultiplier = adjustEpisodeMultiplier;
		}

		public List<Card> UserCreatedDeck => GetUserCreatedDeck();
		public int PlayerId { get; private set; }

		public static List<Card> GetUserCreatedDeck()
		{
			return Decks.MidrangeJadeShaman;
		}

		public override PlayerTask GetMove(POGame.POGame poGame)
		{
			if (!_hasInitialized)
				CustomInit(poGame);

			if (_isTurnBegin)
				OnMyTurnBegin(poGame);

			var options = poGame.CurrentPlayer.Options();

			var chosenTask = ChooseTask(poGame, options);

			//should not happen, but if, just return anything:
			if (chosenTask == null)
			{
				if (TycheAgentConstants.LOG_UNKNOWN_CORRECTIONS) DebugUtils.LogError("Choosen task was null!");

				chosenTask = options.GetUniformRandom(_random);
			}

			if (chosenTask.PlayerTaskType == PlayerTaskType.END_TURN)
				OnMyTurnEnd();

			return chosenTask;
		}

		private PlayerTask ChooseTask(POGame.POGame poGame, List<PlayerTask> options)
		{
			if (options.Count == 1)
				return options[0];

			switch (algoUsed)
			{
				case Algorithm.SearchTree:
					return GetSimulationTreeTask(poGame, options);
				case Algorithm.Greedy:
					return GetGreedyBestTask(poGame, options);
				default:
					return null;
			}
		}

		private PlayerTask GetSimulationTreeTask(POGame.POGame poGame, List<PlayerTask> options)
		{
			var time = Utils.GetSecondsSinceStart() - _turnTimeStart;

			if (time >= TycheAgentConstants.MAX_TURN_TIME)
			{
				DebugUtils.LogError("Turn takes too long, fall back to greedy.");
				return GetGreedyBestTask(poGame, options);
			}

			_simTree.InitTree(_analyzer, poGame, options);

			//-1 because TurnEnd won't be looked at:
			var optionCount = options.Count - 1;
			var numEpisodes = optionCount * _curEpisodeMultiplier;

			var simStart = Utils.GetSecondsSinceStart();

			for (var i = 0; i < numEpisodes; i++)
			{
				if (!IsAllowedToSimulate(simStart, i, numEpisodes, optionCount))
					break;

				var shouldExploit = i / (double) numEpisodes > EXPLORE_TRESHOLD;
				_simTree.SimulateEpisode(_random, i, shouldExploit);
			}

			var bestNode = _simTree.GetBestNode();
			return bestNode.Task;
		}

		private PlayerTask GetGreedyBestTask(POGame.POGame poGame, List<PlayerTask> options)
		{
			var bestTasks = StateUtility.GetSimulatedBestTasks(1, poGame, options, _analyzer);
			return bestTasks[0].task;
		}

		/// <summary> False if there is not enough time left to do simulations. </summary>
		private bool IsAllowedToSimulate(double startTime, int curEpisode, int maxEpisode, int options)
		{
			var time = Utils.GetSecondsSinceStart() - startTime;

			if (time >= TycheAgentConstants.MAX_SIMULATION_TIME)
			{
				DebugUtils.LogWarning("Stopped simulations after " + time.ToString("0.000") + "s and " + curEpisode +
				                      " of " + maxEpisode + " episodes. Having " + options + " options.");
				return false;
			}

			return true;
		}

		private void OnMyTurnBegin(POGame.POGame state)
		{
			_isTurnBegin = false;
			_turnTimeStart = Utils.GetSecondsSinceStart();
		}

		private void OnMyTurnEnd()
		{
			_isTurnBegin = true;

			var timeNeeded = Utils.GetSecondsSinceStart() - _turnTimeStart;

			if (AdjustEpisodeMultiplier && algoUsed == Algorithm.SearchTree)
			{
				const double MAX_DIFF = 16.0;
				var diff = Math.Min(TycheAgentConstants.DECREASE_SIMULATION_TIME - timeNeeded, MAX_DIFF);
				var factor = 0.05;

				//reduce more if above the time limit:
				if (diff <= 0.0f)
					factor = 0.4;

				//simulate at max this value * _defaultEpisodeMultiplier:
				const int MAX_EPISODE_MULTIPLIER = 4;
				_curEpisodeMultiplier = Math.Clamp(
					_curEpisodeMultiplier + (int) (factor * diff * _defaultEpisodeMultiplier),
					_defaultEpisodeMultiplier,
					_defaultEpisodeMultiplier * MAX_EPISODE_MULTIPLIER);
			}

			if (PrintTurnTime) DebugUtils.LogInfo("Turn took " + timeNeeded.ToString("0.000") + "s");

			if (timeNeeded >= TycheAgentConstants.MAX_TURN_TIME)
				DebugUtils.LogWarning("Turn took " + timeNeeded.ToString("0.000") + "s");
		}

		/// <summary>
		///     Called the first round (might be second round game wise) this agents is able to see the game and his
		///     opponent.
		/// </summary>
		private void CustomInit(POGame.POGame initialState)
		{
			_hasInitialized = true;

			PlayerId = initialState.CurrentPlayer.PlayerId;
			_analyzer.OwnPlayerId = PlayerId;

			if (_heroBasedWeights)
				_analyzer.Weights = StateWeights.GetHeroBased(initialState.CurrentPlayer.HeroClass,
					initialState.CurrentOpponent.HeroClass);
		}

		public override void InitializeGame()
		{
			_hasInitialized = false;
		}

		public static TycheAgentB GetLearningAgent(StateWeights weights)
		{
			return new TycheAgentB(weights, false, LEARNING_NUM_EPISODES_MULTIPLIER, false);
		}

		public static TycheAgentB GetSearchTreeAgent(int episodeMultiplier)
		{
			return new TycheAgentB(StateWeights.GetDefault(), true, episodeMultiplier, true);
		}

		public static TycheAgentB GetTrainingAgent(float biasFactor = -1.0f, bool useSecrets = false)
		{
			const bool ADJUST_EPISODES = false;
			const bool HERO_BASED_WEIGHTS = false;

			var weights = StateWeights.GetDefault();

			if (biasFactor >= 0.0f)
				weights.SetWeight(StateWeights.WeightType.BiasFactor, biasFactor);

			var agent = new TycheAgentB(weights, HERO_BASED_WEIGHTS, 0, ADJUST_EPISODES);
			agent.algoUsed = Algorithm.Greedy;
			agent._analyzer.EstimateSecretsAndSpells = useSecrets;

			return agent;
		}

		public override void InitializeAgent()
		{
		}

		public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}
	}
}
