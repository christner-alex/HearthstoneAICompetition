using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameToRep
	{

		public static string Convert(POGame.POGame game)
		{
			return game.FullPrint();
		}
	}
}
