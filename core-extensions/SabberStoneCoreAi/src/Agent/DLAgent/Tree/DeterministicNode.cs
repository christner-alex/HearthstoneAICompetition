using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneCoreAi.POGame;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class DeterministicNode : Node
	{
		/// <summary>
		/// Summary the Deterministic nodes found that result from taking a deterministic action from this state
		/// </summary>
		private Dictionary<GameRep, DeterministicNode> deterministic_children;

		/// <summary>
		/// Summary the Deterministic nodes found that result from taking a stochastic action from this state
		/// </summary>
		private List<ChanceNode> chance_children;

		/// <summary>
		/// Summary the Deterministic nodes found that result from taking a deterministic action from this state
		/// </summary>
		private Dictionary<GameRep, DeterministicNode> end_turn_children;

		/// <summary>
		/// The state this node represents
		/// </summary>
		public POGame.POGame State { get; private set; }

		/// <summary>
		/// The representaion of the state used for hashing, comparisons, and as NN input
		/// </summary>
		public GameRep StateRep { get; private set; }

		public DeterministicNode(POGame.POGame s, DeterministicNode p, PlayerTask a, MaxTree t) : base(p,a,t)
		{
			State = s;
			StateRep = new GameRep(s);

			deterministic_children = new Dictionary<GameRep, DeterministicNode>();
			chance_children = new List<ChanceNode>();
			end_turn_children = new Dictionary<GameRep, DeterministicNode>();

			CheckRep();
		}

		/// <summary>
		/// Whether this node is an EndTurn node: one right after taking the EndTurn task
		/// and after any endturn effects have resolved.
		/// </summary>
		public bool IsEndTurn => Action != null ? Action.PlayerTaskType == PlayerTaskType.END_TURN : false;

		/// <summary>
		/// Whether this node is a lethal state: one where the player has won.
		/// </summary>
		public bool IsLethal => State.CurrentOpponent.Hero.Health <= 0 && !IsLoss;
		/// <summary>
		/// Whether this node is a loss state: one where the player has lost.
		/// </summary>
		public bool IsLoss => (State.CurrentOpponent != State.CurrentPlayer) && (State.CurrentPlayer.Hero.Health <= 0);

		/// <summary>
		/// Finds and stores all the states that result from taking an action from this node's state,
		/// except those that are already in this node's tree. Stops early if a lethal node is found.
		/// </summary>
		/// <returns>The list of Deterministic children found, chance children found, and a lethal node if found (null if not)</returns>
		public (Dictionary<GameRep, DeterministicNode>, List<ChanceNode>, Dictionary<GameRep, DeterministicNode>, DeterministicNode) FindChildren()
		{
			CheckRep();

			//if children were already found, just return them
			if (deterministic_children.Count > 0 || chance_children.Count > 0 || end_turn_children.Count > 0)
			{
				return (deterministic_children, chance_children, end_turn_children, null);
			}

			List<PlayerTask> options = State.CurrentPlayer.Options();

			DeterministicNode winner = null;

			//for each options from the given poGamge
			foreach (PlayerTask option in options)
			{
				//check if that action is deterministic or stochastic by simulating it a few times
				//and checking if all the results are the same
				int action_type = 0;
				List<POGame.POGame> sucessors = new List<POGame.POGame>();
				List<GameRep> sim_reps = new List<GameRep>();
				for (int s = 0; s < Tree.deterministic_simulation_check; s++)
				{
					Dictionary<PlayerTask, POGame.POGame> result = State.Simulate(new List<PlayerTask> { option });
					sucessors.Add(result[option]);
					sim_reps.Add(new GameRep(result[option]));

					//if the new result is different from the last,
					//treat it as random
					if (!sim_reps.Last().Equals(sim_reps.First()))
					{
						action_type = 1;
						break;
					}
				}

				//if it is deterministic and the action is END TURN, set the action type accordingly
				if(action_type == 0 && option.PlayerTaskType == PlayerTaskType.END_TURN)
				{
					action_type = 2;
				}

				POGame.POGame st = sucessors.Last();
				GameRep k = sim_reps.Last();
				switch (action_type)
				{
					//if deterministic...
					case 0:
						//if the discovered state doesn't already in the tree...
						if (!Tree.DeterministicNodes.ContainsKey(k) && !deterministic_children.ContainsKey(k))
						{
							//...add it to this node's children
							DeterministicNode n = new DeterministicNode(st, this, option, Tree);
							deterministic_children.Add(k, n);

							//if the discovered node is Lethal,
							//set the winner to it
							winner = n.IsLethal? n : null;
						}
						break;

					//if stochastic...
					case 1:
						ChanceNode c = new ChanceNode(this, option, Tree);
						chance_children.Add(c);
						break;

					//if end turn...
					case 2:
						//if the discovered state doesn't already in the tree...
						if (!Tree.EndTurnNodes.ContainsKey(k) && !end_turn_children.ContainsKey(k))
						{
							//...add it to this node's children
							DeterministicNode n = new DeterministicNode(st, this, option, Tree);
							end_turn_children.Add(k, n);

							//if the discovered node is Lethal,
							//set the winner to it
							winner = n.IsLethal ? n : null;
						}
						break;
				}

				//if a Lethal node had been found, stop searching
				if (winner!=null)
				{
					break;
				}
			}

			CheckRep();

			return (deterministic_children, chance_children, end_turn_children, winner);
		}

		public override float Score()
		{

			Scorer scorer = Tree.Agent.scorer;
			float score;

			if (!IsEndTurn)
			{
				score = 0;
			}
			else
			{
				//Otherwise, return the NN score
				score = scorer.Q(Tree.Agent.StartTurnState, State);
			}

			return score;
		}

		public override bool CheckRep()
		{
			if (!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			if(!(new GameRep(State)).Equals(StateRep))
			{
				Console.WriteLine("A StateRep is not that of it's State");
				result = false;
			}

			List<string> options = (from o in State.CurrentPlayer.Options() select o.FullPrint()).ToList();
			foreach (DeterministicNode n in deterministic_children.Values)
			{
				if (!options.Contains(n.Action.FullPrint()))
				{
					Console.WriteLine("DeterministicNode: successor does not represent a possible action");
					result = false;
				}

				if(n.Predecessor != this)
				{
					Console.WriteLine("DeterministicNode: successor does not name it as it's predecessor");
					result = false;
				}
			}

			foreach (ChanceNode n in chance_children)
			{
				if (!options.Contains(n.Action.FullPrint()))
				{
					Console.WriteLine("DeterministicNode: A chance node's successor does not represent a possible action");
					result = false;
				}

				if (n.Predecessor != this)
				{
					Console.WriteLine("DeterministicNode: A chance node's successor does not name it as it's predecessor");
					result = false;
				}
			}

			foreach (KeyValuePair<GameRep, DeterministicNode> p in deterministic_children)
			{
				DeterministicNode n = p.Value;
				GameRep s = p.Key;

				if (!n.StateRep.Equals(s))
				{
					Console.WriteLine("DeterministicNode: A deterministic node's state rep is not it's key");
					result = false;
				}
			}

			return result;
		}
	}
}
