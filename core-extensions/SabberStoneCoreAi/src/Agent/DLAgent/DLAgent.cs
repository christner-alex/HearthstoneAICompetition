using System;
using System.Collections.Generic;
using System.Text;
using SabberStoneCore.Tasks;
using SabberStoneCoreAi.Agent;
using SabberStoneCoreAi.POGame;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Model;
using System.Linq;
using System.Diagnostics;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class DLAgent : AbstractAgent
	{
		private MaxTree tree;
		private float move_seconds = 5.0f;
		private Stopwatch turn_watch;
		private Random rnd;

		public override void FinalizeAgent()
		{
			
		}

		public override void FinalizeGame()
		{
			
		}

		public override PlayerTask GetMove(POGame.POGame poGame)
		{
			if(!turn_watch.IsRunning)
			{
				turn_watch.Start();
			}

			PlayerTask move = null;

			while(true)
			{
				if (tree != null)
				{
					move = tree.GetNextMove(poGame);
					if(move != null)
					{
						break;
					}
				}

				if(turn_watch.Elapsed.TotalSeconds > move_seconds)
				{
					break;
				}

				//if a subtree exploring the given state already exists, take it
				tree = tree?.FindSubtree(poGame);
				//otherwise, create a new tree with the given state as the root
				tree = tree ?? new MaxTree(poGame, agent: this);

				float t = (float)(move_seconds - turn_watch.Elapsed.TotalSeconds)/2.0f;
				tree.Run(t);
			}

			//if a move has not been found, return a random move
			if(move==null)
			{
				List<PlayerTask> l = poGame.CurrentPlayer.Options();
				move = l[rnd.Next(l.Count)];
				tree = null;
			}

			//if the move is an end turn action, reset the turn watch and delete the tree
			if (move.PlayerTaskType == PlayerTaskType.END_TURN)
			{
				turn_watch.Reset();
				tree = null;
			}

			return move;
		}

		public override void InitializeAgent()
		{
			tree = null;
			turn_watch = new Stopwatch();
			rnd = new Random();
		}

		public override void InitializeGame()
		{
			
		}

		private bool CheckRep()
		{
			return true;
		}
	}
}
