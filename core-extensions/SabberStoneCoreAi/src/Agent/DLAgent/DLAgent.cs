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
using SabberStoneCore.Model.Entities;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class DLAgent : AbstractAgent
	{
		public Scorer scorer;

		private MaxTree tree;

		private float move_seconds = 60.0f;
		private Stopwatch turn_watch;

		private Random rnd;

		private List<MoveRecord> records;
		private MoveRecord current_record;

		public float Epsilon { get; set; }
		private bool do_random;

		public POGame.POGame StartTurnState { get; private set; }

		public GameRep StartTurnRep { get; private set; }

		public bool debug = false;

		public override void FinalizeAgent()
		{
			
		}

		public override void FinalizeGame()
		{
			//calculate the observed score of each transistion
			foreach(MoveRecord r in records)
			{
				r.SetScore(scorer);
			}

			//TODO: store the records elsewhere
		}

		public override PlayerTask GetMove(POGame.POGame poGame)
		{
			//if the watch is not running (i.e. if it is the start of your turn)...
			if (!turn_watch.IsRunning)
			{
				turn_watch.Start();

				//if a record of the previous turn had been started...
				if(current_record != null)
				{
					//mark the current input as the previous turn's sucessor and store the record
					current_record.SetSuccsessor(poGame.getCopy());
					records.Add(current_record);
				}

				//create a new record with the input as the start state
				current_record = new MoveRecord();
				current_record.SetState(poGame.getCopy());

				//with chance of epsilon, make random moves this turn.
				do_random = rnd.NextDouble() < Epsilon;

				//keep track of the state the turn starts on
				StartTurnState = poGame;
				StartTurnRep = new GameRep(poGame);
			}

			PlayerTask move = null;

			//if random moves are not being used...
			while (!do_random)
			{
				//if the tree exists, get the next move it says to do
				move = tree?.GetNextMove(poGame);

				//if a move has been found or we are out of time, break out of the loop
				if (move != null || turn_watch.Elapsed.TotalSeconds > move_seconds)
				{
					break;
				}

				//if a subtree exploring the given state already exists, take it
				tree = tree?.FindSubtree(poGame);
				//otherwise, create a new tree with the given state as the root
				tree = tree ?? new MaxTree(poGame, agent: this);

				//run the tree for up to half of the remaining turn time
				float t = (float)(move_seconds - turn_watch.Elapsed.TotalSeconds) / 2.0f;
				tree.Run(t);
			}

			bool del_tree = false;

			//if a move has not been found, return a random move
			if (move == null)
			{
				List<PlayerTask> l = poGame.CurrentPlayer.Options();
				move = l[rnd.Next(l.Count)];

				do_random = true;

				del_tree = true;
			}

			//if the move is an end turn action, reset the turn watch and delete the tree
			if (move.PlayerTaskType == PlayerTaskType.END_TURN)
			{
				turn_watch.Reset();

				//TODO: find the true endturn state somehow
				current_record.SetAction( poGame.Simulate(new List<PlayerTask>() { move }).Values.Last() );

				//if the move is lethal, record it
				if(tree != null && tree.FoundLethal)
				{
					//TODO: find a way to find if game is win, loss, or tie w/o the tree
					current_record.SetTerminalStatus(1);
				}

				del_tree = true;
			}

			if(del_tree)
			{
				tree = null;
			}

			return move;
		}

		public override void InitializeAgent()
		{
			tree = null;
			turn_watch = new Stopwatch();
		}

		public override void InitializeGame()
		{
			rnd = new Random();

			scorer = new Scorer(this);

			records = new List<MoveRecord>();

			do_random = false;

			StartTurnState = null;
			StartTurnRep = null;

			current_record = null;
		}

		public DLAgent(float eps = 0f)
		{
			Epsilon = Math.Clamp(eps, 0f, 1f);
		}

		public List<MoveRecord> GetRecords()
		{
			return records;
		}

		private bool CheckRep()
		{
			return true;
		}
	}
}
