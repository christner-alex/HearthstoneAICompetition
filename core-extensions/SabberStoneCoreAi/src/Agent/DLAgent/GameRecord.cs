using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using static SabberStoneCoreAi.Agent.DLAgent.MaxTree;

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
			public SparseTree successor_actions;
		}

		private List<GameRep> States;
		private List<GameRep> Actions;
		private List<SparseTree> SuccessorTrees;

		public GameRecord()
		{
			States = new List<GameRep>();
			Actions = new List<GameRep>();
			SuccessorTrees = new List<SparseTree>();
		}

		public bool PushState(GameRep state)
		{
			//a new state should only be added if
			//every previous state already has an action
			if(States.Count == Actions.Count)
			{
				States.Add(state.Copy());
				return true;
			}

			return false;
		}

		public bool PushState(POGame.POGame game)
		{
			return PushState(new GameRep(game, true));
		}

		public bool PushAction(GameRep action, SparseTree stree)
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

		public bool PushAction(POGame.POGame game, MaxTree mtree)
		{
			return PushAction(new GameRep(game, false), mtree.CreateSparseTree());
		}

		public List<TransitionRecord> ConstructTransitions(Scorer scorer, bool won)
		{
			List<TransitionRecord> records = new List<TransitionRecord>();

			for (int i=0; i<Actions.Count; i++)
			{
				TransitionRecord r = new TransitionRecord();
				r.state = States[i];
				r.action = Actions[i];
				r.successor = i+1 < States.Count ? States[i + 1] : null;
				r.successor_actions = i + 1 < SuccessorTrees.Count ? SuccessorTrees[i + 1] : null;

				//if the action lead to the terminal state, get the win or loss score
				if (i == Actions.Count - 1)
				{
					r.reward = won ? scorer.WinScore : scorer.LossScore;
				}
				else
				{
					//remember, we are storing the portion of the immediate reward
					//gained during the opponent's turn
					r.reward = scorer.TurnReward(r.action, r.successor);
				}

				records.Add(r);
			}

			return records;
		}
	}
}
