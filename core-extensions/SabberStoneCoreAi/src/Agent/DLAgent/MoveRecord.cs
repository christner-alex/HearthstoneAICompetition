using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class MoveRecord
	{
		public POGame.POGame state = null;
		public float reward = 0;
		public POGame.POGame action = null;
		public POGame.POGame sucessor = null;
	}
}
