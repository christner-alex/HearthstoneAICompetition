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

		public List<TransitionRecord> ConstructTransitions(Scorer scorer, bool won)
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

		public List<NDArray> LastBoards(int n)
		{
			return States.TakeLast(n).Select(x => x.BoardRep).ToList();
		}
	}
}
