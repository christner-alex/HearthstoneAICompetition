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
		public Scorer scorer;

		private MaxTree tree;
		private float move_seconds = 5.0f;
		private Stopwatch turn_watch;
		private Random rnd;

		public List<MoveRecord> records;
		public MoveRecord current_record;

		public override void FinalizeAgent()
		{
			
		}

		public override void FinalizeGame()
		{
			//TODO: score each record in the record list
		}

		public override PlayerTask GetMove(POGame.POGame poGame)
		{
			//if the watch is not running (i.e. if it is the start of your turn)...
			if(!turn_watch.IsRunning)
			{
				turn_watch.Start();

				//if a record of the previous turn had been started...
				if(current_record != null)
				{
					//mark the current input as the previous turn's sucessor and store the record
					current_record.sucessor = poGame.getCopy();
					records.Add(current_record);
				}

				//create a new record with the input as the start state
				current_record = new MoveRecord();
				current_record.state = poGame.getCopy();
			}

			PlayerTask move = null;

			while (true)
			{
				if (tree != null)
				{
					move = tree.GetNextMove(poGame);
					if (move != null)
					{
						break;
					}
				}

				if (turn_watch.Elapsed.TotalSeconds > move_seconds)
				{
					break;
				}

				//if a subtree exploring the given state already exists, take it
				tree = tree?.FindSubtree(poGame);
				//otherwise, create a new tree with the given state as the root
				tree = tree ?? new MaxTree(poGame, agent: this);

				float t = (float)(move_seconds - turn_watch.Elapsed.TotalSeconds) / 2.0f;
				tree.Run(t);
			}

			//if a move has not been found, return a random move
			if (move == null)
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

				//TODO: find the true result of the move somehow
				current_record.action = poGame.Simulate(new List<PlayerTask>() { move }).Values.Last();
			}

			return move;
		}

		public override void InitializeAgent()
		{
			tree = null;
			turn_watch = new Stopwatch();
			rnd = new Random();

			scorer = new Scorer();

			records = new List<MoveRecord>();
			current_record = null;
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
