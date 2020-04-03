using Newtonsoft.Json;
using NumSharp;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Priority_Queue;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameSearchTree
	{
		/// <summary>
		/// The agent this tree belongs to.
		/// </summary>
		public DLAgent Agent { get; }

		/// <summary>
		/// A dictionary pairing gameNodes accessed by their GameRep
		/// </summary>
		public Dictionary<GameRep, GameNode> Nodes;

		/// <summary>
		/// A queue of nodes that should be followed according to the tree.
		/// </summary>
		private Queue<GameNode> taskqueue;

		/// <summary>
		/// The last node in the most recently calculated task queue. Null if none have been calculated.
		/// </summary>
		private GameNode taskTerminal;

		/// <summary>
		/// The first node found in this tree in which the player wins.
		/// Null if one hasn't been found.
		/// </summary>
		private GameNode lethal_node;

		/// <summary>
		/// The node contraining the gamestate that represents the head of this tree.
		/// </summary>
		public readonly GameNode Root;

		/// <summary>
		/// The game rep representing the state this turn started on.
		/// </summary>
		public readonly GameRep StartTurnState;

		/// <summary>
		/// A list of nodes to be searched in the Run function
		/// </summary>
		private SimplePriorityQueue<GameNode> expansion_queue;

		public GameSearchTree(POGame.POGame root_game, DLAgent agent)
		{
			Agent = agent;
			StartTurnState = agent.StartTurnRep.Copy();
			Nodes = new Dictionary<GameRep, GameNode>();
			taskqueue = new Queue<GameNode>();
			taskTerminal = null;
			lethal_node = null;
			expansion_queue = new SimplePriorityQueue<GameNode>();

			Root = new GameNode(null, null, this, root_game);

			Nodes.Add(Root.StateRep, Root);


			if (Root.IsLethal)
			{
				lethal_node = Root;
			}
			else if(!Root.IsLoss)
			{
				expansion_queue.Enqueue(Root, Root.Priority);
			}
		}

		/// <summary>
		/// True if a lethal node has been found by this tree. False otherwise.
		/// </summary>
		public bool FoundLethal => lethal_node != null;

		/// <summary>
		/// True if there is nothing left to expand, either because there are no more nodes to expand
		/// or there are 
		/// </summary>
		public bool DoneExpanding => FoundLethal || expansion_queue.Count == 0;

		public void Run(float runtime)
		{
			Stopwatch watch = new Stopwatch();
			watch.Start();

			//while we are not done expanding and there is still time left
			while(!DoneExpanding && watch.Elapsed.TotalSeconds < runtime)
			{
				GameNode current = expansion_queue.Dequeue();

				(Dictionary<GameRep, GameNode>, GameNode) newNodes = current.FindChildren();

				//if lethal was found, store it and break
				if(newNodes.Item2 != null)
				{
					lethal_node = newNodes.Item2;
					break;
				}

				//add each (non-lethal, non-loss, non-discovered) nodes to the expansion list and 
				foreach(KeyValuePair<GameRep, GameNode> node in newNodes.Item1)
				{
					Nodes.Add(node.Key, node.Value);
					expansion_queue.Enqueue(node.Value, node.Value.Priority);
				}
			}

			CalcMoveQueue();

			watch.Stop();
		}

		public void CalcMoveQueue()
		{
			GameNode current = lethal_node;

			//if there is no lethal node,
			//score the tree and set the current node to 
			if(!FoundLethal)
			{
				SavableTree stree = CreateSavable();
				(float, GameRep) best = stree.Score(Agent.scorer, true);
				current = Nodes.ContainsKey(best.Item2) ? Nodes[best.Item2] : current;
			}

			//if there is no lethal node and no best node, return no queue
			if(current == null)
			{
				taskTerminal = null;
				taskqueue.Clear();
				return;
			}

			LinkedList<GameNode> l = new LinkedList<GameNode>();

			//Iteratively backtrack from that node to its parent
			//keeping track of the actions along the way
			//until you reach the root
			taskTerminal = current;
			while (current != null && Root != current)
			{
				l.AddFirst(current);
				current = current.Predecessor;
			}
			taskqueue = new Queue<GameNode>(l);
		}

		public PlayerTask NextMove(POGame.POGame poGame)
		{
			GameRep input_rep = new GameRep(poGame, Agent.Record);

			//if we are on the terminal node and the terminal node matches the input,
			//then return an end turn action
			if(taskqueue.Count==0)
			{
				if(taskTerminal!=null && input_rep.Equals(taskTerminal.StateRep))
				{
					return EndTurnTask.Any(poGame.CurrentPlayer);
				}
			}
			else
			{
				GameRep n_rep = taskqueue.Peek().Predecessor.StateRep;
				PlayerTask act = taskqueue.Peek().Action;

				//if the predeccesor of the next node in the queue is the input node
				//and the action to give is a valid one
				if (n_rep.Equals(input_rep) && poGame.Simulate(new List<PlayerTask>() { act }).Values.Last() != null)
				{
					//return the action of the front node
					GameNode n = taskqueue.Dequeue();
					return n.Action;
				}
			}

			//if the terminal node is not the poGame,
			//or the 
			return null;
		}

		public SavableTree CreateSavable()
		{
			return new SavableTree(this);
		}

		public class SavableTree
		{
			public GameRep State;
			public GameRep[] Actions;

			public SavableTree(GameSearchTree tree)
			{
				State = tree.StartTurnState.Copy();
				Actions = (from rep in tree.Nodes.Keys select rep.Copy()).ToArray();
			}

			[JsonConstructor]
			public SavableTree(GameRep state, GameRep[] actions)
			{
				State = state;
				Actions = actions;
			}

			public (float, GameRep) Score(Scorer scorer, bool use_online)
			{
				NDArray scores = scorer.Q(State, Actions, use_online);
				int bestScore = scores.max().GetValue<int>(0);
				int bestIndex = scores.argmax();
				GameRep bestAction = Actions[bestIndex];
				return (bestScore, bestAction);
			}

			public float DoubleQScore(Scorer scorer)
			{
				//get the best state according to the online score
				(float, GameRep) bestOnline = Score(scorer, true);

				//return the target score of that state
				return scorer.Q(State, bestOnline.Item2, false);
			}
		}
	}
}
