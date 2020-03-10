using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class ChanceNode : Node
	{
		/// <summary>
		/// MaxTrees whose roots represent possible outcomes of this node's Action
		/// from its Predecessor's state
		/// </summary>
		private Dictionary<GameRep,MaxTree> children_trees;

		/// <summary>
		/// The weight assigned to each of this node's children_trees,
		/// corresponding to the number of times the corresponding state has been
		/// observed by simulating this node's action from it's Predecessor's state
		/// </summary>
		private Dictionary<GameRep, int> weights;

		Random rnd;

		public ChanceNode(DeterministicNode p, PlayerTask a, MaxTree t) : base(p, a, t)
		{
			children_trees = new Dictionary<GameRep, MaxTree>();
			weights = new Dictionary<GameRep, int>();

			rnd = new Random();

			CheckRep();
		}

		/// <summary>
		/// Simulate a new result of taking this node's action a from the parent node's state.
		/// Make a new MaxTree whose root is the resulting state. Fill the Deterministic portion
		/// of it's tree. Finally, add the new Tree to this node's tree's chance_subtree list
		/// as well as to the chance_subtree lists of any MaxTree above it
		/// </summary>
		public void AddSubtrees(float runtime, int loops = 3)
		{
			CheckRep();

			for (int i = 0; i < loops; i++)
			{
				//simulate the result of this node's action
				Dictionary<PlayerTask, POGame.POGame> result = Predecessor.State.Simulate(new List<PlayerTask> { Action });
				GameRep k = new GameRep(result[Action], Action?.PlayerTaskType != PlayerTaskType.END_TURN);

				//if the resulting state has already been seen,
				//merely increase the weight of the already existing
				//corresponing MaxTree
				if (children_trees.ContainsKey(k))
				{
					weights[k]++;
				}
				//otehrwise, create a new MaxTree with the resulting state as the root,
				//add it thos this node's children trees,
				//and have all MaxTrees above this node add it to their chance_subtrees
				else
				{
					MaxTree child = new MaxTree(result[Action], Action, Tree);

					child.FillDeterministicTree(runtime / loops);

					children_trees.Add(k, child);
					weights.Add(k, 1);
				}
			}

			CheckRep();
		}

		/// <summary>
		/// Gets the subtree of this node whose root's State is state
		/// </summary>
		/// <param name="state">The state to search for.</param>
		/// <returns>The subtree with state if found. Null otherwise.</returns>
		public MaxTree FindSubtree(POGame.POGame state)
		{
			GameRep k = new GameRep(state, Action?.PlayerTaskType != PlayerTaskType.END_TURN);
			return FindSubtree(k);
		}
		/// <summary>
		/// Gets the subtree of this node whose root's StateRep is k
		/// </summary>
		/// <param name="k">The state representaion to search for.</param>
		/// <returns>The subtree with state if found. Null otherwise.</returns>
		public MaxTree FindSubtree(GameRep k)
		{
			MaxTree result = children_trees.ContainsKey(k) ? children_trees[k] : null;
			return result;
		}

		/// <summary>
		/// Calculate the score of this node: the weighted average score of this node's subtrees.
		/// </summary>
		/// <returns>This node's score. 0 if there are no subtrees</returns>
		public override float Score()
		{
			CheckRep();

			if (children_trees.Count == 0)
			{
				return 0;
			}

			float numerator = 0;
			foreach(GameRep k in children_trees.Keys)
			{
				(float, Node) s = children_trees[k].GetScore();
				numerator += s.Item1 * weights[k];
			}

			CheckRep();

			return numerator / weights.Values.Sum();
		}

		/// <summary>
		/// Get a random child tree of this node.
		/// </summary>
		/// <returns>A random child tree of this node. Null if node exist.</returns>
		public MaxTree RandomSubTree()
		{
			if(children_trees.Count==0)
			{
				return null;
			}

			return children_trees.Values.ToList()[rnd.Next(children_trees.Count)];
		}

		public override bool CheckRep()
		{
			if (!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			if (!(children_trees.Keys.Count == weights.Keys.Count && children_trees.Keys.All(weights.Keys.ToList().Contains)))
			{
				result = false;
				Console.WriteLine("ChanceNode: children trees list and weight list are not the same");
			}

			List<string> options = (from o in Predecessor.State.CurrentPlayer.Options() select o.FullPrint()).ToList();
			if (!options.Contains(Action.FullPrint()))
			{
				result = false;
				Console.WriteLine("ChanceNode: Action is not an option of the predecessor's state");
			}

			if (!weights.Values.All(v => v >= 1))
			{
				result = false;
				Console.WriteLine("ChanceNode: Not all the weights are >= 1");
			}

			if(Action == null || Predecessor == null || Tree == null)
			{
				result = false;
				Console.WriteLine("ChanceNode: Action, Predecessor, ot Tree is null");
			}

			foreach(KeyValuePair<GameRep, MaxTree> p in children_trees)
			{
				GameRep s = p.Key;
				MaxTree t = p.Value;

				if(!s.Equals(t.Root.StateRep))
				{
					result = false;
					Console.WriteLine("ChanceNode: Tree.Root.StateRep is not it's key");
				}

				if(t.Root.Action != Action)
				{
					result = false;
					Console.WriteLine("ChanceNode: Tree.Root.Action is not this node's Action");
				}
			}

			Debug.Assert(result);

			return result;
		}
	}
}
