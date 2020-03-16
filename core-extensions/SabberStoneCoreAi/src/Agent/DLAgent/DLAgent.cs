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
using static SabberStoneCoreAi.Agent.DLAgent.MaxTree;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class DLAgent : AbstractAgent
	{
		public Scorer scorer;

		private MaxTree tree;
		private MaxTree root_tree;

		private float move_seconds = 60.0f;
		private Stopwatch turn_watch;

		private Random rnd;

		private GameRecord record;

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

		}

		public override PlayerTask GetMove(POGame.POGame poGame)
		{
			//if the watch is not running (i.e. if it is the start of your turn)...
			if (!turn_watch.IsRunning)
			{
				turn_watch.Start();

				//with chance of epsilon, make random moves this turn.
				//do_random = rnd.NextDouble() < Epsilon;
				do_random = false;

				//keep track of the state the turn starts on
				StartTurnState = poGame;
				StartTurnRep = new GameRep(poGame);

				record.PushState(StartTurnRep.Copy());

				//create a new tree for the start of the turn
				tree = null;
				root_tree = null;
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

				if (root_tree == null) root_tree = tree;
			}

			//if a move has not been found, return a random move
			if (move == null)
			{
				List<PlayerTask> l = poGame.CurrentPlayer.Options();
				move = l[rnd.Next(l.Count)];

				//do random moves for the rest of the turn
				do_random = true;
			}

			//if the move is an end turn action, reset the turn watch and save the action taken
			if (move.PlayerTaskType == PlayerTaskType.END_TURN)
			{
				turn_watch.Reset();

				//TODO: find the true endturn state somehow
				record.PushAction(poGame, root_tree);
			}

			return move;
		}

		public override void InitializeAgent()
		{

		}

		public override void InitializeGame()
		{
			rnd = new Random();

			scorer = new Scorer(this);

			record = new GameRecord();

			do_random = false;

			StartTurnState = null;
			StartTurnRep = null;

			tree = null;
			root_tree = null;

			turn_watch = new Stopwatch();
		}

		public DLAgent(float eps = 0f)
		{
			Epsilon = Math.Clamp(eps, 0f, 1f);
		}

		public GameRecord GetRecords()
		{
			return record;
		}

		private bool CheckRep()
		{
			if (!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			return result;
		}
	}
}
