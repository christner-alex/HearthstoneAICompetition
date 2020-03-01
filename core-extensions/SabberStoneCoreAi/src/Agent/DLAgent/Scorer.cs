using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using System.Linq;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Scorer
	{

		public readonly float WinScore = 100;
		public readonly float LossScore = -100;

		public readonly float opponent_score_modifier = 0.8f;

		public readonly List<float> weights;

		/// <summary>
		/// Whether this state is a lethal state: one where the player has won.
		/// </summary>
		public bool IsLethal(POGame.POGame state) => state.CurrentOpponent.Hero.Health <= 0 && !IsLoss(state);

		/// <summary>
		/// Whether this state is a loss state: one where the player has lost.
		/// </summary>
		public bool IsLoss(POGame.POGame state) =>
			//TODO: check implementation
			(state.CurrentOpponent != state.CurrentPlayer) && (state.CurrentPlayer.Hero.Health <= 0);


		public Scorer()
		{
			weights = new List<float>( new float[] {
				3f, //player board change
				1f, //player hand change
				-3f, //opponent board change
				-1f, //opponent hand change
				0.1f, //player health change
				-0.1f, //opponent health change
				-0.2f //player remaining mana
			} );
		}

		public float CheckTerminal(POGame.POGame state)
		{

			if (IsLoss(state))
			{
				return LossScore;
			}
			if (IsLethal(state))
			{
				return WinScore;
			}

			return 0;
		}

		/// <summary>
		/// Calculate the reward of starting in 'state' and ending your turn on the state 'action'
		/// </summary>
		/// <param name="start_state">The start of the turn to score</param>
		/// <param name="end_state">The end of the turn to score</param>
		/// <returns></returns>
		public float TurnReward(POGame.POGame start_state, POGame.POGame end_state)
		{
			float score = CheckTerminal(end_state);
			if (score != 0) return score;

			//TODO: revise score if needed

			Controller player_start = start_state.CurrentPlayer;
			Controller opponent_start = start_state.CurrentOpponent;

			Controller player_end = end_state.CurrentOpponent;
			Controller opponent_end = end_state.CurrentPlayer;

			HandZone player_start_hand = player_start.HandZone;
			HandZone player_end_hand = player_end.HandZone;

			BoardZone player_start_board = player_start.BoardZone;
			BoardZone player_end_board = player_end.BoardZone;

			HandZone opponent_start_hand = opponent_start.HandZone;
			HandZone opponent_end_hand = opponent_end.HandZone;

			BoardZone opponent_start_board = opponent_start.BoardZone;
			BoardZone opponent_end_board = opponent_end.BoardZone;

			float player_start_health = player_start.Hero.Health + player_start.Hero.Armor;
			float player_end_health = player_end.Hero.Health + player_end.Hero.Armor;

			float opponent_start_health = opponent_start.Hero.Health + opponent_start.Hero.Armor;
			float opponent_end_health = opponent_end.Hero.Health + opponent_end.Hero.Armor;

			List<float> features = new List<float>(new float[] {
				player_end_board.Count - player_start_board.Count, //friendly board change
				player_end_hand.Count - player_start_hand.Count, //friendly hand change
				opponent_end_board.Count - opponent_start_board.Count, //enemy board change
				opponent_end_hand.Count - opponent_start_hand.Count, //fenemy hand change
				player_end_health - player_start_health, //friendly health change
				opponent_end_health - opponent_start_health, //enemy health change
				player_end.RemainingMana //remaining mana
			});

			return features.Zip(weights, (x, y) => x * y).Sum();
		}

		/// <summary>
		/// Calculate the estimated reward of ending your turn on the state 'action' using a Neural Network.
		/// </summary>
		/// <param name="end_state">The state to estimate the score for</param>
		/// <returns></returns>
		public float ActionScore(POGame.POGame end_state)
		{
			float score = CheckTerminal(end_state);
			if (score != 0) return score;

			//TODO implement neural network
			return 0;
		}

		/// <summary>
		/// Calculate the Q value of the (state, action) pair, which is the sum of the reward gained turn your turn
		/// plus the estimated reward of 
		/// </summary>
		/// <param name="start_state"></param>
		/// <param name="end_state"></param>
		/// <returns></returns>
		public float Q(POGame.POGame start_state, POGame.POGame end_state)
		{
			float score = CheckTerminal(end_state);
			if (score != 0) return score;

			return TurnReward(start_state, end_state) + ActionScore(end_state);
		}

		/// <summary>
		/// Calculate the score observed from a (state,action,state) transistion, which is the difference between
		/// the reward gained by the current player on their turn and the opposite of the reward gained by the
		/// opposing player on their turn.
		/// </summary>
		/// <param name="p1_start">The state of the start of your turn</param>
		/// <param name="p1_end">The state of the end of your turn/start of the opponent's turn</param>
		/// <param name="p2_end">The state of the end of your opponent's turn/start of your next turn</param>
		/// <returns></returns>
		public float ScoreTransition(POGame.POGame p1_start, POGame.POGame p1_end, POGame.POGame p2_end)
		{
			float score = CheckTerminal(p1_end);
			if (score != 0) return score;

			return TurnReward(p1_start, p1_end) - opponent_score_modifier * TurnReward(p1_end, p2_end);
		}
	}
}
