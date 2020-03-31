using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	internal static class StateUtility
	{
		/// <summary> Returns N sorted simulated TySimResults for the given start state. </summary>
		public static List<SimResult> GetSimulatedBestTasks(int numTasks, POGame.POGame game,
			StateAnalyzer analyzer)
		{
			return GetSimulatedBestTasks(numTasks, game, game.CurrentPlayer.Options(), analyzer);
		}

		/// <summary> Returns N sorted simulated TySimResults for the given start state. </summary>
		public static List<SimResult> GetSimulatedBestTasks(int numTasks, POGame.POGame game,
			List<PlayerTask> options, StateAnalyzer analyzer)
		{
			return GetSortedBestTasks(numTasks, GetSimulatedGames(game, options, analyzer));
		}

		/// <summary> Returns the best 'numTasks' tasks. Note: will sort the given List of tasks! </summary>
		public static List<SimResult> GetSortedBestTasks(int numTasks, List<SimResult> taskStructs)
		{
			//take at least one task:
			if (numTasks <= 0)
				numTasks = 1;

			taskStructs.Sort((x, y) => y.value.CompareTo(x.value));
			return taskStructs.Take(numTasks).ToList();
		}

		public static SimResult GetSimulatedGame(POGame.POGame parent, PlayerTask task, StateAnalyzer analyzer)
		{
			var simulatedState = parent.Simulate(new List<PlayerTask> {task})[task];
			var stateValue = GetStateValue(parent, simulatedState, task, analyzer);
			return new SimResult(simulatedState, task, stateValue);
		}

		/// <summary> Returns a list of simulated games with the given parameters. </summary>
		public static List<SimResult> GetSimulatedGames(POGame.POGame parent, List<PlayerTask> options,
			StateAnalyzer analyzer)
		{
			var stateTaskStructs = new List<SimResult>();

			foreach (var t in options)
				stateTaskStructs.Add(GetSimulatedGame(parent, t, analyzer));

			return stateTaskStructs;
		}

		/// <summary> Estimates how good the given child state is. </summary>
		public static float GetStateValue(POGame.POGame parent, POGame.POGame child, PlayerTask task,
			StateAnalyzer analyzer)
		{
			var valueFactor = 1.0f;

			State myState = null;
			State enemyState = null;

			Controller player = null;
			Controller opponent = null;

			//it's a buggy state, mostly related to equipping/using weapons on heroes etc.
			//in this case use the old state and estimate the new state manually:
			if (child == null)
			{
				player = parent.CurrentPlayer;
				opponent = parent.CurrentOpponent;

				myState = State.FromSimulatedGame(parent, player, task);
				enemyState = State.FromSimulatedGame(parent, opponent, null);

				//if the correction failes, assume the task is x% better/worse:
				if (!State.CorrectBuggySimulation(myState, enemyState, parent, task))
					valueFactor = 1.25f;
			}

			else
			{
				player = child.CurrentPlayer;
				opponent = child.CurrentOpponent;

				//happens sometimes even with/without TURN_END, idk
				if (!analyzer.IsMyPlayer(player))
				{
					player = child.CurrentOpponent;
					opponent = child.CurrentPlayer;
				}

				myState = State.FromSimulatedGame(child, player, task);
				enemyState = State.FromSimulatedGame(child, opponent, null);
			}

			DebugUtils.Assert(analyzer.IsMyPlayer(player));
			DebugUtils.Assert(!analyzer.IsMyPlayer(opponent));
			return analyzer.GetStateValue(myState, enemyState, player, opponent, task) * valueFactor;
		}

		/// <summary> Rounds before neutralMana are punished, later it will be rewarded. </summary>
		public static float LateReward(int mana, int neutralMana, float reward)
		{
			return reward * (mana - neutralMana);
		}
	}
}
