using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using NumSharp.Backends;
using NumSharp.Backends.Unmanaged;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;

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
