using System;
using System.Collections.Generic;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	internal class SimTree
	{
		private readonly List<TaskNode> _explorableNodes = new List<TaskNode>();

		private readonly Dictionary<PlayerTask, TaskNode> _nodesToEstimate = new Dictionary<PlayerTask, TaskNode>();
		private readonly List<TaskNode> _sortedNodes = new List<TaskNode>();
		private StateAnalyzer _analyzer;
		private POGame.POGame _rootGame;

		public void InitTree(StateAnalyzer analyzer, POGame.POGame root, List<PlayerTask> options)
		{
			_sortedNodes.Clear();
			_explorableNodes.Clear();
			_nodesToEstimate.Clear();

			_analyzer = analyzer;
			_rootGame = root;

			//var initialResults = TyStateUtility.GetSimulatedGames(root, options, _analyzer);

			foreach (var task in options)
			{
				var node = new TaskNode(this, _analyzer, task, 0.0f);

				//end turn is pretty straight forward, should not really be looked at later in the simulations, just simulate once and keep the value:
				if (task.PlayerTaskType == PlayerTaskType.END_TURN)
				{
					var sim = StateUtility.GetSimulatedGame(root, task, _analyzer);
					node.AddValue(sim.value);
				}
				else
				{
					_explorableNodes.Add(node);
					_sortedNodes.Add(node);
				}

				_nodesToEstimate.Add(task, node);
			}
		}

		public void SimulateEpisode(Random random, int curEpisode, bool shouldExploit)
		{
			TaskNode nodeToExplore = null;

			//exploiting:
			if (shouldExploit)
			{
				_sortedNodes.Sort((x, y) => y.TotalValue.CompareTo(x.TotalValue));
				//exploit only 50% best nodes:
				var count = (int) (_sortedNodes.Count * 0.5 + 0.5);
				nodeToExplore = _sortedNodes.GetUniformRandom(random, count);
			}

			//explore:
			else
			{
				nodeToExplore = _explorableNodes[curEpisode % _explorableNodes.Count];
			}

			//should not be possible:
			if (nodeToExplore == null)
				return;

			var task = nodeToExplore.Task;
			var result = StateUtility.GetSimulatedGame(_rootGame, task, _analyzer);
			nodeToExplore.Explore(result, random);
		}

		public TaskNode GetBestNode()
		{
			var nodes = new List<TaskNode>(_nodesToEstimate.Values);
			nodes.Sort((x, y) => y.GetAverage().CompareTo(x.GetAverage()));
			return nodes[0];
		}

		public PlayerTask GetBestTask()
		{
			return GetBestNode().Task;
		}
	}
}
