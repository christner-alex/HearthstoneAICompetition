using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Scorer
	{

		public readonly float WinScore;
		public readonly float LossScore;

		public Scorer()
		{
			WinScore = 100;
			LossScore = -100;
		}

		public float R_sa(POGame.POGame state, POGame.POGame action)
		{
			return 0;
		}

		public float R_a(POGame.POGame action)
		{
			return 0;
		}

		public float R(POGame.POGame state, POGame.POGame action)
		{
			return R_sa(state, action) + R_a(action);
		}
	}
}
