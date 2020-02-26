using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Diagnostics;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class MaxTree
	{
		/// <summary>
		/// The nodes in this tree representing the state resulting from taking a deterministic action from it's predecessor.
		/// </summary>
		public Dictionary<string, DeterministicNode> DeterministicNodes { get; }

		/// <summary>
		/// The nodes in this tree representing the possible states resulting from taking a stochastic action from it's predecessor.
		/// </summary>
		public List<ChanceNode> ChanceNodes { get; }

		/// <summary>
		/// The nodes in this tree representing the result of taking an end-turn action from it's predecessor.
		/// </summary>
		public Dictionary<string, DeterministicNode> EndTurnNodes { get; }

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
		private Node queueTerminal;

		/// <summary>
		/// The MaxTree that this tree is a direct descendant of.
		/// If this tree is the root tree, this is null
		/// </summary>
		public MaxTree ParentTree { get; private set; }

		/// <summary>
		/// The node contraining the gamestate that represents the head of this tree.
		/// </summary>
		public DeterministicNode Root { get; private set; }

		/// <summary>
		/// The first node found in this tree in which the player wins.
		/// Null if one hasn't been found.
		/// </summary>
		private DeterministicNode lethal_node;

		/// <summary>
		/// A list of nodes to be searched in the FillDeterministicTree function.
		/// </summary>
		private List<DeterministicNode> expansion_list;

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
		/// <param name="rootgame"> The game state this tree starts from</param>
		/// <param name="rootaction"> The PlayerTask which resulted in the root state. Null if no such action exists (like with the start of a turn)</param>
		/// <param name="parent_tree"> The MaxTree that this tree directly descends from. Null if no such action exists (like with the start of a turn)</param>
		/// <param name="det"> The number of times a state/action pair will be simulated to check if the results are deterministic</param>
		/// <param name="tree_prob"> A probability. The higher tree_prob, the greater the chance a lower subtree will be expanded each expansion</param>
		public MaxTree(POGame.POGame rootgame, PlayerTask rootaction=null, MaxTree parent_tree=null, DLAgent agent=null, int det = 3, float tree_prob = 0.5f)
		{
			Root = new DeterministicNode(rootgame, null, rootaction, this);

			DeterministicNodes = new Dictionary<string, DeterministicNode>();
			DeterministicNodes.Add(Root.StateRep, Root);
			ChanceNodes = new List<ChanceNode>();
			EndTurnNodes = new Dictionary<string, DeterministicNode>();

			expansion_list = new List<DeterministicNode>();
			expansion_list.Add(Root);

			taskqueue = new Queue<Node>();
			queueTerminal = null;

			chance_subtrees = new List<MaxTree>();

			this.deterministic_simulation_check = det;
			child_tree_prob = tree_prob;

			rnd = new Random();

			lethal_node = null;

			Agent = parent_tree != null ? parent_tree.Agent : agent;
			
			//put this tree in the subtree of any predecessor trees
			ParentTree = parent_tree;
			if (ParentTree != null)
			{
				ParentTree.PercolateUpMaxTree(this);
			}

			//if the root is a loss, a win, or an end turn
			//then there are no nodes to expand
			bool done = false;
			if (Root.IsLoss)
			{
				done = true;
			}
			if (Root.IsLethal)
			{
				lethal_node = Root;
				done = true;
			}
			if (Root.IsEndTurn)
			{
				EndTurnNodes.Add(Root.StateRep, Root);
				done = true;
			}
			if (done)
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

			(PlayerTask,POGame.POGame) result = (null,null);

			if(poGame != null && taskqueue.Count != 0)
			{
				string n_rep = taskqueue.Peek().Predecessor.StateRep;
				PlayerTask act = taskqueue.Peek().Action;
				string input_rep = GameToRep.Convert(poGame);

				//and the current front move is a valid one...
				if (act != null && n_rep == input_rep && poGame.Simulate(new List<PlayerTask>() { act }).Values.Last() != null)
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

			Stopwatch watch = new Stopwatch();
			watch.Start();

			//expand the deterministic tree for up to runtime seconds
			FillDeterministicTree(runtime, watch);

			//get the remaining time and
			//split the time between the chance nodes
			float fill_time = (float)watch.Elapsed.TotalSeconds;
			float remaining = (runtime - fill_time);
			float time_per_node = remaining / (ChanceNodes.Count + 1);
			time_per_node = Math.Max(time_per_node, (time_per_node + fill_time)/2);

			//expand chance nodes for the remaining time.
			int loops = 0;
			watch.Restart();
			while (lethal_node == null //break if there is a lethal node (no need to search furnther)
				&& ChanceNodes.Count > 0 //or there are no chance nodes (nothing to search)
				&& watch.Elapsed.TotalSeconds < remaining //or if we are out of time
				&& loops < Math.Pow(chance_subtrees.Count, 2) + ChanceNodes.Count) //or if there are far more loops than subtrees (not much new info being created)
			{
				ExpandChanceNode(time_per_node);
				loops++;
			}

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
			if (queueTerminal == null || queueTerminal.GetType() != typeof(ChanceNode))
			{
				return null;
			}

			MaxTree result = ((ChanceNode)queueTerminal).FindSubtree(state);
			return result;
		}

		/// <summary>
		/// Fill in the deterministic aspects of the MaxTree.
		/// Terminal nodes will be end of turn states or any actions with
		/// stochastic outcomes.
		/// </summary>
		/// ///<param name="runtime">The maximum time to spend in this method</param>
		/// ///<param name="watch">The watch timing this method</param>
		public void FillDeterministicTree(float runtime, Stopwatch watch)
		{
			CheckRep();

			//while the queue is not empty, there is still time, and lethal hasn't been found
			while (expansion_list.Count > 0 //while there are still nodes to expand
				&& (watch.Elapsed.TotalSeconds < runtime || EndTurnNodes.Count == 0) //and there is either more time or no End Turn Nodes found
				&& lethal_node == null) //and we haven't found a lethal node
			{
				//select an unexpanded node at random
				int ind = rnd.Next(expansion_list.Count);
				DeterministicNode current = expansion_list[ind];
				expansion_list.RemoveAt(ind);

				//find all the successor states of the current node, except for those that already exist in 
				(Dictionary<string, DeterministicNode>, List<ChanceNode>, DeterministicNode) new_nodes = current.FindChildren();

				//for each newly discovered deterministic node that is a successor to the current node...
				foreach (KeyValuePair<string, DeterministicNode> n in new_nodes.Item1)
				{
					//add it to the deterministic nodes of this tree
					DeterministicNodes.Add(n.Key, n.Value);

					//if its an endturn node...
					if (n.Value.IsEndTurn)
					{
						//add it to the end turn list
						EndTurnNodes.Add(n.Key, n.Value);
					}

					//if it isn't an endturn or loss node, add it to the stack
					if(!n.Value.IsEndTurn && !n.Value.IsLoss)
					{
						expansion_list.Add(n.Value);
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

			//choose a random Chance node in the tree, such that ones close to the top have higher
			//chance to be picked
			MaxTree current = this;
			while (current.chance_subtrees.Count > 0 && rnd.NextDouble() <= child_tree_prob)
			{
				current = chance_subtrees[rnd.Next(chance_subtrees.Count)];
			}

			//if the chosen tree has no chance nodes, return
			List<ChanceNode> leaves = current.ChanceNodes;
			if (leaves.Count <= 0)
			{
				return;
			}

			//add a new subtree to one of the Chance nodes in the chosen tree
			leaves[rnd.Next(leaves.Count)].AddSubtree(runtime);

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
			if (lethal_node == null)
			{
				(float, Node) n = GetScore();
				current = n.Item2;
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
			queueTerminal = l.Last();
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
			if (lethal_node != null)
			{
				return (lethal_node.Score(), lethal_node);
			}

			//if no end turn or chance node exist, return a score of 0 and null for the state
			else if(ChanceNodes.Count == 0 && EndTurnNodes.Count == 0)
			{
				return (0, null);
			}

			//otherwise, return the end turn or chance node with the highest score
			Node bestnode = null;
			float bestscore = Single.NegativeInfinity;
			foreach (Node n in EndTurnNodes.Cast<Node>().Concat(ChanceNodes.Cast<Node>()))
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
		}

		public bool CheckRep()
		{
			if(!Parameters.doCheckRep || !Parameters.MaxTreePrintDebug)
			{
				return true;
			}

			bool result = true;

			if(Root==null)
			{
				Console.WriteLine("MaxTree: Root is null");
				result = false;
			}

			if(lethal_node != null && !lethal_node.IsLethal)
			{
				Console.WriteLine("MaxTree: The lethal node is not lethal");
				result = false;
			}

			if (lethal_node != null && !DeterministicNodes.Values.Contains(lethal_node))
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

			if(DeterministicNodes.Count > 0 && EndTurnNodes.Count > 0 && !EndTurnNodes.Values.All(DeterministicNodes.ContainsValue))
			{
				Console.WriteLine("MaxTree: Not all end turn nodes are in the deterministic list");
				result = false;
			}

			if (Root.Predecessor != null)
			{
				Console.WriteLine("MaxTree: The root node's predecessor is not null");
				result = false;
			}

			foreach(KeyValuePair<string,DeterministicNode> p in DeterministicNodes)
			{
				DeterministicNode n = p.Value;
				string s = p.Key;

				if(n.Tree != this)
				{
					Console.WriteLine("MaxTree: A deterministic node's tree is not this");
					result = false;
				}

				if(n.StateRep != s)
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

			foreach (KeyValuePair<string, DeterministicNode> p in EndTurnNodes)
			{
				DeterministicNode n = p.Value;
				string s = p.Key;

				if (n.Tree != this)
				{
					Console.WriteLine("MaxTree: An endturn node's tree is not this");
					result = false;
				}

				if (n.StateRep != s)
				{
					Console.WriteLine("MaxTree: An endturn node's state rep is not it's key");
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
							Console.WriteLine("MaxTree: The last node in the queue is an end turn node but isn't in the endturn nodes");
							result = false;
						}

						if (n.GetType() == typeof(DeterministicNode) && !EndTurnNodes.ContainsValue((DeterministicNode)n))
						{
							Console.WriteLine("MaxTree: The last node in the queue an endturn node and turn node but isn't in the endturn nodes");
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

			return result;
		}
	}
}
