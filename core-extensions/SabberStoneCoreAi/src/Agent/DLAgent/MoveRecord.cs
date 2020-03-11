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
			Reward = 0;
			Action = null;
			Successor = null;
			TerminalStatus = 0;
		}

		public GameRep State { get; private set; }
		public float Reward { get; private set; }
		public GameRep Action { get; private set; }
		public GameRep Successor { get; private set; }
		public int TerminalStatus { get; private set; }

		public void SetState(POGame.POGame state)
		{
			State = new GameRep(state);
		}

		public void SetAction(POGame.POGame action)
		{
			Action = new GameRep(action, false);
		}

		public void SetSuccsessor(POGame.POGame successor)
		{
			Successor = new GameRep(successor);
		}

		public void SetState(GameRep state)
		{
			State = state;
		}

		public void SetAction(GameRep action)
		{
			Action = action;
		}

		public void SetSuccsessor(GameRep successor)
		{
			Successor = successor;
		}

		public void SetTerminalStatus(int status)
		{
			TerminalStatus = Math.Clamp(status, -1, 1);
		}

		public void CalcReward(Scorer scorer)
		{

			if(TerminalStatus == 1)
			{
				Reward = scorer.WinScore;
			}
			else if (TerminalStatus == -1)
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
