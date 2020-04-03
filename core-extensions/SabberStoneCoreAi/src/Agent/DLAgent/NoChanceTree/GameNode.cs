using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameNode
	{
		public GameNode Predecessor { get; }

		public PlayerTask Action { get; }

		public GameSearchTree Tree { get; }

		public POGame.POGame State { get; }

		public GameRep StateRep { get; }

		public GameNode(GameNode pred, PlayerTask act, GameSearchTree tree, POGame.POGame state)
		{
			Predecessor = pred;
			Action = act;
			Tree = tree;
			State = state;
			StateRep = new GameRep(state, tree.Agent.Record);
		}

		/// <summary>
		/// Whether this node is a lethal state: one where the player has won.
		/// </summary>
		public bool IsLethal => State.CurrentOpponent.Hero.Health <= 0 && !IsLoss;

		/// <summary>
		/// Whether this node is a loss state: one where the player has lost.
		/// </summary>
		public bool IsLoss => (State.CurrentPlayer.Hero.Health <= 0);

		public (Dictionary<GameRep, GameNode>, GameNode) FindChildren()
		{
			Dictionary<GameRep, GameNode> newChildren = new Dictionary<GameRep, GameNode>();

			List<PlayerTask> options = State.CurrentPlayer.Options();

			GameNode winner = null;

			foreach(PlayerTask option in options)
			{
				//ignore the end turn action
				if (option.PlayerTaskType == PlayerTaskType.END_TURN
					|| option.PlayerTaskType == PlayerTaskType.CONCEDE)
				{
					continue;
				}

				Dictionary<PlayerTask, POGame.POGame> result = State.Simulate(new List<PlayerTask> { option });

				POGame.POGame st = result[option];
				if (result[option] == null) continue;
				GameRep k = new GameRep(st, Tree.Agent.Record);

				//if the node has not yet been discovered...
				if(!Tree.Nodes.ContainsKey(k) && !newChildren.ContainsKey(k))
				{
					GameNode newNode = new GameNode(this, option, Tree, st);

					//set the node to the winner and be done
					if(newNode.IsLethal)
					{
						winner = newNode;
						break;
					}
					else if(!newNode.IsLoss)
					{
						newChildren.Add(k, newNode);
					}
				}
			}

			return (newChildren, winner);
		}

		public float Priority => -Scorer.TurnReward(Tree.StartTurnState, StateRep);

		/*
		public float Score()
		{
			Scorer scorer = Tree.Agent.scorer;

			if (IsLethal)
			{
				return scorer.WinScore;
			}
			else if (IsLoss)
			{
				return scorer.LossScore;
			}
			else
			{
				//If an end turn node, return the NN score
				return scorer.Q(Tree.Agent.StartTurnRep, StateRep, true);
			}
		}
		*/
	}
}
