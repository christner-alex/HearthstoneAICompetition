using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static SabberStoneCoreAi.Agent.DLAgent.MaxTree;
using NumSharp;
using Newtonsoft.Json;
using System.IO;
using static SabberStoneCoreAi.Agent.DLAgent.GameSearchTree;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameRecord
	{
		public struct TransitionRecord
		{
			public GameRep state;
			public GameRep action;
			public float reward;
			public GameRep successor;
			public SavableTree successor_actions;
		}

		private List<GameRep> States;
		private List<GameRep> Actions;
		private List<SavableTree> SuccessorTrees;

		public GameRecord()
		{
			States = new List<GameRep>();
			Actions = new List<GameRep>();
			SuccessorTrees = new List<SavableTree>();
		}

		public bool PushState(GameRep state)
		{
			//a new state should only be added if
			//every previous state already has an action
			if (States.Count == Actions.Count)
			{
				States.Add(state.Copy());
				return true;
			}

			return false;
		}

		public bool PushState(POGame.POGame game)
		{
			return PushState(new GameRep(game, this));
		}

		public bool PushAction(GameRep action, SavableTree stree)
		{
			//an action should only be added when there is a
			//state without an accompanying action
			if (States.Count == Actions.Count + 1)
			{
				Actions.Add(action.Copy());
				SuccessorTrees.Add(stree);

				return true;
			}

			return false;
		}

		public bool PushAction(POGame.POGame game, GameSearchTree stree)
		{
			return PushAction(new GameRep(game, this), stree.CreateSavable());
		}

		/// <summary>
		/// DEPRICATED
		/// </summary>
		/// <param name="won"></param>
		/// <returns></returns>
		public List<TransitionRecord> ConstructTransitions(bool won)
		{
			List<TransitionRecord> records = new List<TransitionRecord>();

			for (int i = 0; i < Actions.Count; i++)
			{
				TransitionRecord r = new TransitionRecord();
				r.state = States[i].Copy();
				r.action = Actions[i].Copy();
				r.successor = i + 1 < States.Count ? States[i + 1].Copy() : null;
				r.successor_actions = i + 1 < SuccessorTrees.Count ? SuccessorTrees[i + 1] : null;

				//if the action lead to the terminal state, get the win or loss score
				if (i == Actions.Count - 1)
				{
					//if the action lead to the terminal state, get the win or loss score
					r.reward = won ? Scorer.WinScore : Scorer.LossScore;
				}
				else
				{
					//remember, we are storing the portion of the immediate reward
					//gained during the opponent's turn
					r.reward = Scorer.TurnReward(r.action, r.successor);
				}

				records.Add(r);
			}

			return records;
		}

		public List<NDArray> LastBoards(int n)
		{
			return States.TakeLast(n).Select(x => x.BoardRep).ToList();
		}

		public static List<TransitionRecord> ConstructTransitions(GameRecord records, bool won)
		{
			List<TransitionRecord> transitions = new List<TransitionRecord>();

			for (int i = 0; i < records.Actions.Count; i++)
			{
				TransitionRecord r = new TransitionRecord();
				r.state = records.States[i].Copy();
				r.action = records.Actions[i].Copy();
				r.successor = i + 1 < records.States.Count ? records.States[i + 1].Copy() : null;
				r.successor_actions = i + 1 < records.SuccessorTrees.Count ? records.SuccessorTrees[i + 1] : null;

				if (i == records.Actions.Count - 1)
				{
					//if it was the last turn, set the score to the corresponding win/loss score
					r.reward = won ? Scorer.WinScore : Scorer.LossScore;
				}
				else if (r.successor != null)
				{
					//otherwise, set the score to the opposite of what the second player gained on their next turn, and a decay to penalize too many turns
					r.reward = Scorer.TransitionReward(r.action, r.successor);
				}

				transitions.Add(r);
			}

			return transitions;
		}

		/*
		public static List<TransitionRecord> ConstructTransitions(GameRecord firstPlayer, GameRecord secondPlayer, bool firstWon)
		{
			if(firstPlayer.Actions.Count != secondPlayer.Actions.Count && firstPlayer.Actions.Count != secondPlayer.Actions.Count + 1)
			{
				throw new ArgumentException($"The shape of the GameRecord Actions are incorrect: firstPlayer: {firstPlayer.Actions.Count} secondPlayer; {secondPlayer.Actions.Count}");
			}

			List<TransitionRecord> transitions = new List<TransitionRecord>();

			for(int i=0; i< firstPlayer.Actions.Count; i++)
			{
				TransitionRecord r = new TransitionRecord();
				r.state = firstPlayer.States[i].Copy();
				r.action = firstPlayer.Actions[i].Copy();
				r.successor = i + 1 < firstPlayer.States.Count ? firstPlayer.States[i + 1].Copy() : null;
				r.successor_actions = i + 1 < firstPlayer.SuccessorTrees.Count ? firstPlayer.SuccessorTrees[i + 1] : null;

				if (i == firstPlayer.Actions.Count - 1)
				{
					//if it was the last turn, set the score to the corresponding win/loss score
					r.reward = firstWon ? Scorer.WinScore : Scorer.LossScore;
				}
				else if (i < secondPlayer.Actions.Count)
				{
					//otherwise, set the score to the opposite of what the second player gained on their next turn, and a decay to penalize too many turns
					r.reward = - Scorer.TurnReward(secondPlayer.States[i], secondPlayer.Actions[i]) - Scorer.TurnDecay;
				}

				transitions.Add(r);
			}

			for(int i=0; i<secondPlayer.Actions.Count; i++)
			{
				TransitionRecord r = new TransitionRecord();
				r.state = secondPlayer.States[i].Copy();
				r.action = secondPlayer.Actions[i].Copy();
				r.successor = i + 1 < secondPlayer.States.Count ? secondPlayer.States[i + 1].Copy() : null;
				r.successor_actions = i + 1 < secondPlayer.SuccessorTrees.Count ? secondPlayer.SuccessorTrees[i + 1] : null;

				if (i == secondPlayer.Actions.Count - 1)
				{
					//if it was the last turn, set the score to the corresponding win/loss score
					r.reward = firstWon ? Scorer.LossScore : Scorer.WinScore;
				}
				else if (i + 1 < firstPlayer.Actions.Count)
				{
					//otherwise, set the score to the opposite of what the first player gained on their next turn, and a decay to penalize too many turns
					r.reward = - Scorer.TurnReward(firstPlayer.States[i+1], firstPlayer.Actions[i+1]) - Scorer.TurnDecay;
				}

				transitions.Add(r);
			}

			return transitions;
		}
		*/
	}
}
