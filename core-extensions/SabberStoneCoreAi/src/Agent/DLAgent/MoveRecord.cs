using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class MoveRecord
	{

		public MoveRecord()
		{
			State = null;
			StateRep = null;
			Reward = 0;
			Action = null;
			ActionRep = null;
			Successor = null;
			SuccessorRep = null;
			TerminalStatus = 0;
		}

		public POGame.POGame State { get; private set; }
		public GameRep StateRep { get; private set; }
		public float Reward { get; private set; }
		public POGame.POGame Action { get; private set; }
		public GameRep ActionRep { get; private set; }
		public POGame.POGame Successor { get; private set; }
		public GameRep SuccessorRep { get; private set; }
		public int TerminalStatus { get; private set; }

		public void SetState(POGame.POGame state, GameRep state_rep = null)
		{
			State = state;
			StateRep = state_rep ?? new GameRep(state);
		}

		public void SetAction(POGame.POGame action, GameRep action_rep = null)
		{
			Action = action;
			ActionRep = action_rep ?? new GameRep(action, false);
		}

		public void SetSuccsessor(POGame.POGame successor, GameRep successor_rep = null)
		{
			Successor = successor;
			SuccessorRep = successor_rep ?? new GameRep(successor);
		}

		public void SetTerminalStatus(int status)
		{
			TerminalStatus = Math.Clamp(status, -1, 1);
		}

		public void SetScore(Scorer scorer)
		{

			if(State != null && Action != null && TerminalStatus == 1)
			{
				Reward = scorer.WinScore;
			}
			else if (State != null && Action != null && TerminalStatus == -1)
			{
				Reward = scorer.LossScore;
			}

			else if (State != null && Action != null && Successor != null)
			{
				Reward = scorer.ScoreTransition(State, Action, Successor);
			}
		}

	}
}
