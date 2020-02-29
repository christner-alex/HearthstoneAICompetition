using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class MoveRecord
	{
		public POGame.POGame State { get; private set; }
		public string StateRep { get; private set; }
		public float Reward { get; private set; }
		public POGame.POGame Action { get; private set; }
		public string ActionRep { get; private set; }
		public POGame.POGame Successor { get; private set; }
		public string SuccessorRep { get; private set; }

		public MoveRecord()
		{
			State = null;
			StateRep = null;
			Reward = 0;
			Action = null;
			ActionRep = null;
			Successor = null;
			SuccessorRep = null;
		}

		public void SetState(POGame.POGame state, string state_rep = null)
		{
			State = state;
			StateRep = state_rep ?? GameToRep.Convert(state);
		}

		public void SetAction(POGame.POGame action, string action_rep = null)
		{
			Action = action;
			ActionRep = action_rep ?? GameToRep.Convert(State);
		}

		public void SetSuccsessor(POGame.POGame successor, string successor_rep = null)
		{
			Successor = Action;
			SuccessorRep = ActionRep ?? GameToRep.Convert(State);
		}

		public void SetScore(Scorer scorer)
		{
			if(State != null && Action != null && Successor != null)
			{
				Reward = scorer.ScoreTransition(State, Action, Successor);
			}
		}
	}
}
