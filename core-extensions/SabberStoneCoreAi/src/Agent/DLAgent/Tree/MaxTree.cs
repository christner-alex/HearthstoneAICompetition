using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using NumSharp;
using Newtonsoft.Json;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class MaxTree
	{
		/// <summary>
		/// The nodes in this tree representing the state resulting from taking a deterministic action from it's predecessor.
		/// </summary>
		public Dictionary<GameRep, DeterministicNode> DeterministicNodes { get; }

		/// <summary>
		/// The nodes in this tree representing the possible states resulting from taking a stochastic action from it's predecessor.
		/// </summary>
		public List<ChanceNode> ChanceNodes { get; }

		/// <summary>
		/// All Maxtrees descended from this MaxTree.
		/// </summary>
		private List<MaxTree> chance_subtrees;

		/// <summary>
		/// A queue of nodes that should be followed according to the tree.
		/// </summary>
		private Queue<Node> taskqueue;

		/// <summary>
		/// The last node in the most recently calculated taskqueue. Null of none have been calculated yet.
		/// </summary>
		private Node taskTerminal;

		/// <summary>
		/// The MaxTree that this tree is a direct descendant of.
		/// If this tree is the root tree, this is null
		/// </summary>
		public readonly MaxTree ParentTree;

		/// <summary>
		/// The node contraining the gamestate that represents the head of this tree.
		/// </summary>
		public readonly DeterministicNode Root;

		/// <summary>
		/// The first node found in this tree in which the player wins.
		/// Null if one hasn't been found.
		/// </summary>
		private DeterministicNode lethal_node;

		/// <summary>
		/// True if a lethal node has been found by this tree. False otherwise.
		/// </summary>
		public bool FoundLethal => lethal_node != null;

		/// <summary>
		/// A list of nodes to be searched in the FillDeterministicTree function.
		/// </summary>
		private Stack<DeterministicNode> expansion_list;

		/// <summary>
		/// The number of times to simulate a State Action pair to determine whether it is Deterministic or Stochastic.
		/// </summary>
		public int deterministic_simulation_check;

		/// <summary>
		/// The higher tree_prob, the greater the chance a lower subtree will be expanded each ExpandChanceNode call
		/// </summary>
		public float child_tree_prob;

		private Random rnd;

		/// <summary>
		/// The agent this tree belongs to.
		/// </summary>
		public readonly DLAgent Agent;

		/// <summary>
		/// Creates a new MaxTree object to represent the results of actions from the root state.
		/// </summary>
		/// <param name="root_game"> The game state this tree starts from</param>
		/// <param name="root_action"> The PlayerTask which resulted in the root state. Null if no such action exists (like with the start of a turn)</param>
		/// <param name="parent_tree"> The MaxTree that this tree directly descends from. Null if no such action exists (like with the start of a turn)</param>
		/// <param name="det"> The number of times a state/action pair will be simulated to check if the results are deterministic</param>
		/// <param name="tree_prob"> A probability. The higher tree_prob, the greater the chance a lower subtree will be expanded each expansion</param>
		public MaxTree(POGame.POGame root_game, PlayerTask root_action=null, MaxTree parent_tree=null, DLAgent agent=null, int det = 3, float tree_prob = 0.5f)
		{
			Agent = parent_tree != null ? parent_tree.Agent : agent;

			Root = new DeterministicNode(root_game, null, root_action, this);

			DeterministicNodes = new Dictionary<GameRep, DeterministicNode>();
			DeterministicNodes.Add(Root.StateRep, Root);
			ChanceNodes = new List<ChanceNode>();

			expansion_list = new Stack<DeterministicNode>();
			expansion_list.Push(Root);

			taskqueue = new Queue<Node>();
			taskTerminal = null;

			chance_subtrees = new List<MaxTree>();

			this.deterministic_simulation_check = det;
			child_tree_prob = tree_prob;

			rnd = new Random();

			lethal_node = null;
			
			//put this tree in the subtree of any predecessor trees
			ParentTree = parent_tree;
			if (ParentTree != null)
			{
				ParentTree.PercolateUpMaxTree(this);
			}

			//if the root is a loss, a win, or an end turn
			//then there are no nodes to expand
			if (Root.IsLethal)
			{
				lethal_node = Root;
			}
			if (Root.IsLoss || Root.IsLethal)
			{
				expansion_list.Clear();
			}

			CheckRep();
		}

		/// <summary>
		/// Returns the next move of the calculated task queue if poGame is what the tree expects.
		/// </summary>
		/// <param name="poGame">The game which the agent expects the next move to be for.</param>
		/// <returns>The PlayerTask the agent should take if poGame is at the front of the task queue. Null otherwise.</returns>
		public PlayerTask GetNextMove(POGame.POGame poGame)
		{
			CheckRep();

			PlayerTask result = null;
			GameRep input_rep = new GameRep(poGame,Agent.Record);

			//if the poGame state is the same as the terminal node's...
			if(taskTerminal != null && taskqueue.Count == 0
				&& taskTerminal.GetType() == typeof(DeterministicNode)
				&& ((DeterministicNode)taskTerminal).StateRep.Equals(input_rep))
			{
				//the return the end turn action
				result = EndTurnTask.Any(poGame.CurrentPlayer);
			}
			else if (poGame != null && taskqueue.Count != 0)
			{
				GameRep n_rep = taskqueue.Peek().Predecessor.StateRep;
				PlayerTask act = taskqueue.Peek().Action;
				
				//and the current front move is a valid one...
				if (act != null && n_rep.Equals(input_rep) && poGame.Simulate(new List<PlayerTask>() { act }).Values.Last() != null)
				{
					//return that move
					Node n = taskqueue.Dequeue();
					result = n.Action;
				}
			}

			CheckRep();

			return result;
		}

		/// <summary>
		/// Fill the deterministic tree,
		/// expand stochastic nodes randomly,
		/// then calculate the mode queue.
		/// </summary>
		/// <param name="runtime">The maximum time to spend in this method</param>
        public void Run(float runtime)
		{
			CheckRep();

			float remaining = runtime;

			int chance_expansions = 0;

			Stopwatch watch = new Stopwatch();
			watch.Start();

			do
			{
				//expand the deterministic tree for up to half the remiaining time
				FillDeterministicTree(remaining / 4);

				//get the remaining time
				remaining -= (float)watch.Elapsed.TotalSeconds;
				watch.Restart();

				float time_per_node = Math.Max(0.25f * remaining / (ChanceNodes.Count + 1), 1f);
				//time_per_node = Math.Max(time_per_node, (time_per_node + fill_time) / 2);
				 while (!FoundLethal //break if there is a lethal node (no need to search furnther)
					&& ChanceNodes.Count > 0 //or there are no chance nodes (nothing to search)
					&& watch.Elapsed.TotalSeconds < remaining //or if we are out of time
					//or there are far more expansions than there are trees (no new info being created)
					&& chance_expansions < chance_subtrees.Count * Math.Log(chance_subtrees.Count+1) + ChanceNodes.Count
					&& chance_subtrees.Count < 10 * ChanceNodes.Count //or there are far more subtrees than chance nodes
					)
				{
					ExpandChanceNode(time_per_node);
					chance_expansions++;
				}

				//get the remaining time
				remaining -= (float)watch.Elapsed.TotalSeconds;
				watch.Restart();

			} while (!FoundLethal && remaining > 0f && expansion_list.Count > 0);
			//continue looping while lethal hasn't been found, there is still time left, and there are deterministic nodes left to expand

			watch.Stop();

			//calculate the mode queue given the current tree.
			CalcMoveQueue();

			CheckRep();
		}

		/// <summary>
		/// Get the chance subtree with the given state that is descended from the last Chance Node in the task queue.
		/// </summary>
		/// <param name="state">The state of the chance</param>
		/// <returns>The MaxTree chance subtree in the terminal chance node.
		/// If the last node isn't a Chance Node or such a subtree doesn't exist, returns null</returns>
		public MaxTree FindSubtree(POGame.POGame state)
		{
			if (taskTerminal == null || taskTerminal.GetType() != typeof(ChanceNode))
			{
				return null;
			}

			MaxTree result = ((ChanceNode)taskTerminal).FindSubtree(state);
			return result;
		}

		/// <summary>
		/// Fill in the deterministic aspects of the MaxTree.
		/// Terminal nodes will be end of turn states or any actions with
		/// stochastic outcomes.
		/// </summary>
		/// ///<param name="runtime">The maximum time to spend in this method</param>
		/// ///<param name="watch">The watch timing this method</param>
		public void FillDeterministicTree(float runtime)
		{
			Stopwatch watch = new Stopwatch();
			watch.Start();

			CheckRep();

			while (expansion_list.Count > 0 //while there are still nodes to expand
				&& watch.Elapsed.TotalSeconds < runtime //and there is more time
				&& !FoundLethal) //and we haven't found a lethal node
			{
				//select an unexpanded node
				DeterministicNode current = expansion_list.Pop();

				//find all the successor states of the current node, except for those that already exist in 
				(Dictionary<GameRep, DeterministicNode>, List<ChanceNode>, DeterministicNode) new_nodes = current.FindChildren();

				//for each newly discovered deterministic node that is a successor to the current node...
				foreach (KeyValuePair<GameRep, DeterministicNode> n in new_nodes.Item1)
				{
					//add it to the deterministic nodes of this tree
					DeterministicNodes.Add(n.Key.Copy(), n.Value);

					//if it isn't an endturn or loss node, add it to the stack
					if(!n.Value.IsLoss && !n.Value.IsLethal)
					{
						expansion_list.Push(n.Value);
					}
				}

				//add the chance nodes to the Chance Node list
				ChanceNodes.AddRange(new_nodes.Item2);

				//if a node that gets you lethal is found,
				//save it and stop searching
				if(new_nodes.Item3 != null)
				{
					lethal_node = new_nodes.Item3;
					//break;
				}
			}

			CheckRep();

			watch.Stop();
		}

		/// <summary>
		/// Looping over this tree's ChanceNodes until a terminal condition is reached,
		/// create a new MaxTree for a random outcome of the chance node to simulate
		/// the best move after the chance.
		/// </summary>
		/// ///<param name="runtime">The maximum time to spend expanding a node</param>
		public void ExpandChanceNode(float runtime)
		{
			CheckRep();

			//choose a random MaxTree node in the tree, such that ones close to the top have higher
			//chance to be picked
			MaxTree current = this;
			while (current.ChanceNodes.Count > 0 && rnd.NextDouble() <= child_tree_prob)
			{
				ChanceNode random_node = ChanceNodes[rnd.Next(ChanceNodes.Count)];
				MaxTree next = random_node.RandomSubTree();
				if (next == null)
				{
					break;
				}
				current = next;
			}

			//if the chosen tree has no chance nodes, return
			List<ChanceNode> leaves = current.ChanceNodes;
			if (leaves.Count <= 0)
			{
				return;
			}

			//add a new subtree to one of the Chance nodes in the chosen tree
			leaves[rnd.Next(leaves.Count)].AddSubtrees(runtime, current==this ? 3 : 1);

			CheckRep();
		}

		/// <summary>
		/// Add the given MaxTree to this tree's chance_subtree's list
		/// as well as to this tree's parent MaxTree if it exists.
		/// </summary>
		/// <param name="k">The string representation of the ch_tr root state</param>
		/// <param name="ch_tr">The MaxTree to add</param>
		private void PercolateUpMaxTree(MaxTree ch_tr)
		{
			chance_subtrees.Add(ch_tr);
			if (ParentTree != null)
			{
				ParentTree.PercolateUpMaxTree(ch_tr);
			}
		}

		/// <summary>
		/// Calculate the queue of PlayerTasks needed to be taken in order
		/// to get from this tree's root state to the best node
		/// found by the search.
		/// </summary>
		private void CalcMoveQueue()
		{
			//if a lethal node exists in this tree,
			//start from that
			Node current = lethal_node;

			//if one hasn't been found, start from the endturn or chance
			//node in this tree with the highest score
			if (!FoundLethal)
			{
				(float, Node) n = GetScore();
				current = n.Item2;
			}
			
			if(current == null)
			{
				taskTerminal = Root;
				return;
			}

			LinkedList<Node> l = new LinkedList<Node>();

			//Iteratively backtrack from that node to its parent
			//keeping track of the actions along the way
			//until you reach the root
			while (current != null && Root != current)
			{
				l.AddFirst(current);
				current = current.Predecessor;
			}

			taskqueue = new Queue<Node>(l);
			taskTerminal = l.Count > 0 ? l.Last() : Root;
		}

		/// <summary>
		/// Return the Score of this tree, which is the maximum score value
		/// of its Deterministic endturn node and ChanceNodes.
		/// Return the corresponding node as well.
		/// If a lethal node has been found, it will be that node.
		/// If no lethal, end turn, or chance nodes have been found, then return 0 score and null state.
		/// Otherwise, return the end turn or chance node with the highest score and its state.
		/// </summary>
		/// <returns>The score of this tree as well as the Node with that score. If a lethal node </returns>
		public (float, Node) GetScore()
		{
			CheckRep();

			//if a lethal node has been found
			//return it and its score
			if (FoundLethal)
			{
				return (Agent.scorer.WinScore, lethal_node);
			}

			//if no end turn or chance node exist, return a score of 0 and null for the state
			else if(ChanceNodes.Count == 0 && DeterministicNodes.Count == 0)
			{
				return (0, null);
			}

			//get the best deterministic node and score
			DeterministicNode[] DetNodes = DeterministicNodes.Values.ToArray();
			NDArray DetScores = Agent.scorer.Q(Agent.StartTurnRep, (from d in DetNodes select d.StateRep).ToArray(), true);
			float bestDetScore = DetScores.max().GetValue<float>(0);
			int bestDetIndex = DetScores.argmax();
			Node bestDetNode = DetNodes[bestDetIndex];

			//if there are chance nodes, get the best of them
			float bestChanceScore = float.MinValue;
			Node bestChanceNode = null;
			if (ChanceNodes.Count > 0)
			{
				List<float> ChanceScores = (from c in ChanceNodes select c.Score()).ToList();
				bestChanceScore = ChanceScores.Max();
				int bestChanceIndex = ChanceScores.IndexOf(bestChanceScore);
				bestChanceNode = ChanceNodes[bestChanceIndex];
			}

			CheckRep();
			return bestDetScore >= bestChanceScore ? (bestDetScore, bestDetNode) : (bestChanceScore, bestChanceNode);

			/*
			//otherwise, return the end turn or chance node with the highest score
			Node bestnode = null;
			float bestscore = float.MinValue;
			foreach (Node n in ChanceNodes.Cast<Node>().Concat(DeterministicNodes.Values.Cast<Node>()))
			{
				float s = n.Score();
				if (s > bestscore)
				{
					bestscore = s;
					bestnode = n;
				}
			}

			CheckRep();
			return (bestscore, bestnode);
			*/
		}

		public class SparseTree
		{
			public GameRep Root;
			public GameRep[] DetActions;
			public SparseTree[][] ChanceActions;

			public SparseTree(MaxTree tree, GameRep root)
			{
				Root = root.Copy();

				DetActions = (from rep in tree.DeterministicNodes.Keys select rep.Copy()).ToArray();

				ChanceActions = (from n in tree.ChanceNodes select (from t in n.ChildrenTrees.Values select new SparseTree(t, root)).ToArray()).ToArray();
			}

			[JsonConstructor]
			public SparseTree(GameRep root, GameRep[] detActions, SparseTree[][] chanceActions)
			{
				Root = root;
				DetActions = detActions;
				ChanceActions = chanceActions;
			}

			public float ScoreDets(Scorer scorer, bool use_online)
			{
				return DetActions.Length > 0 ? scorer.Q(Root, DetActions.ToArray(), use_online).max().GetValue<float>(0) : float.MinValue;
			}

			public List<float> ScoreChance(Scorer scorer, bool use_online)
			{
				var chanceScores = (from t in ChanceActions where t.Length > 0 select t.Average(x => x.Score(scorer, use_online))).ToList();
				return chanceScores.Count == 0 ? new List<float>() { float.MinValue } : chanceScores;
			}

			public float Score(Scorer scorer, bool use_online)
			{
				float bestDetScore = ScoreDets(scorer, use_online);

				float bestChanceScore = ScoreChance(scorer, use_online).Max();

				return Math.Max(bestDetScore, bestChanceScore);
			}

			/// <summary>
			/// Calculates the DoubleDQN score for this Sparse tree, which is the target score of the action with the highest online score
			/// </summary>
			/// <param name="scorer"></param>
			/// <returns></returns>
			public float DoubleQScore(Scorer scorer)
			{
				//get the best deterministic node and score
				NDArray DetScores = ScoreDets(scorer, true);
				float bestDetScore = DetScores.max().GetValue<float>(0);

				//if there are chance nodes, get the best of them
				float bestChanceScore = float.MinValue;
				List<float> ChanceScores = new List<float>() { float.MinValue };
				if (ChanceActions.Length > 0)
				{
					ChanceScores = ScoreChance(scorer, true);
					bestChanceScore = ChanceScores.Max();
				}

				//if the online score is best for a deterministic node...
				if(bestDetScore >= bestChanceScore)
				{
					//return the target score of that node
					GameRep bestDetNode = DetActions[DetScores.argmax()];
					return scorer.Q(Root, bestDetNode, false);
				}
				//if the online score is best for a chance node...
				else
				{
					//return the target score for that node
					var bestChanceNode = ChanceActions[ChanceScores.IndexOf(bestChanceScore)];
					return (from t in bestChanceNode select t.Score(scorer, false)).Average();
				}
			}
		}

		public SparseTree CreateSparseTree()
		{
			return new SparseTree(this,this.Root.StateRep);
		}

		public bool CheckRep()
		{
			if(!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			if(Root==null)
			{
				Console.WriteLine("MaxTree: Root is null");
				result = false;
			}

			if(FoundLethal && !lethal_node.IsLethal)
			{
				Console.WriteLine("MaxTree: The lethal node is not lethal");
				result = false;
			}

			if (FoundLethal && !DeterministicNodes.Values.Contains(lethal_node))
			{
				Console.WriteLine("MaxTree: The lethal node is not in its tree's DeterministicNodes");
				result = false;
			}

			if (ParentTree != null)
			{
				if(!ParentTree.chance_subtrees.Contains(this))
				{
					Console.WriteLine("MaxTree: The parent tree does not contain this tree");
					result = false;
				}

				if(!chance_subtrees.All(ParentTree.chance_subtrees.Contains))
				{
					Console.WriteLine("MaxTree: The parent's subtree does not contain this tree's subtrees");
					result = false;
				}
			}

			if(DeterministicNodes.Count == 0)
			{
				Console.WriteLine("MaxTree: There are no deterministic nodes");
				result = false;
			}

			if (Root.Predecessor != null)
			{
				Console.WriteLine("MaxTree: The root node's predecessor is not null");
				result = false;
			}

			foreach(KeyValuePair<GameRep,DeterministicNode> p in DeterministicNodes)
			{
				DeterministicNode n = p.Value;
				GameRep s = p.Key;

				if(n.Tree != this)
				{
					Console.WriteLine("MaxTree: A deterministic node's tree is not this");
					result = false;
				}

				if(!n.StateRep.Equals(s))
				{
					Console.WriteLine("MaxTree: A deterministic node's state rep is not it's key");
					result = false;
				}
			}

			foreach (ChanceNode n in ChanceNodes)
			{
				if (n.Tree != this)
				{
					Console.WriteLine("MaxTree: A chance node's tree is not this");
					result = false;
				}
			}

			if(taskqueue.Count>0)
			{
				List<Node> tasklist = new List<Node>(taskqueue);

				for(int i=1; i<tasklist.Count; i++)
				{
					Node n = tasklist[i];

					if (tasklist.Count >= 2 && n.Predecessor != tasklist[i-1])
					{
						Console.WriteLine("MaxTree: The predecessor of a node in the taskqueue is not the previous node");
						result = false;
					}

					if(i==tasklist.Count-1)
					{
						if(n.GetType() == typeof(ChanceNode) && !ChanceNodes.Contains((ChanceNode)n))
						{
							Console.WriteLine("MaxTree: The last node in the queue is a chance node but isn't in the chance nodes list");
							result = false;
						}
					}
					else
					{
						if (n.GetType() != typeof(DeterministicNode))
						{
							Console.WriteLine("MaxTree: The non-last node in the queue is not a deterministic node");
							result = false;
						}
					}
				}
			}

			if(chance_subtrees.Count > 0 && ChanceNodes.Count == 0)
			{
				Console.WriteLine("MaxTree: There are chance subtrees but no chance nodes");
				result = false;
			}

			Debug.Assert(result);

			return result;
		}
	}
}
