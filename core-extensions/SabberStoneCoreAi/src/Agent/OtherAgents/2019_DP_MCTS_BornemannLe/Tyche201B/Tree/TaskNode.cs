using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	/// <summary> A unique node for a given PlayerTask. </summary>
	internal class TaskNode
	{
		private readonly StateAnalyzer _analyzer;

		private SimTree _tree;

		public TaskNode(SimTree tree, StateAnalyzer analyzer, PlayerTask task, float totalValue)
		{
			_tree = tree;
			_analyzer = analyzer;
			Task = task;
			TotalValue = totalValue;
			Visits = 0;
		}

		public PlayerTask Task { get; }

		public float TotalValue { get; private set; }

		public int Visits { get; private set; }

		public void Explore(SimResult simResult, Random random)
		{
			while (true)
			{
				if (simResult.IsBuggy)
				{
					AddValue(simResult.value);
					return;
				}

				var game = simResult.state;
				var options = game.CurrentPlayer.Options();
				var task = options.GetUniformRandom(random);
				var childState = StateUtility.GetSimulatedGame(game, task, _analyzer);

				if (childState.task.PlayerTaskType != PlayerTaskType.END_TURN)
				{
					simResult = childState;
					continue;
				}

				AddValue(simResult.value);
				break;
			}
		}

		public void AddValue(float value)
		{
			TotalValue += value;
			Visits++;
		}

		public float GetAverage()
		{
			return TotalValue / Visits;
		}
	}
}
