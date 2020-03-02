﻿using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using System.Linq;
using System.Diagnostics;

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
				0.1f, //friendly health change
				1f, //friendly hand change
				3f, //friendly board change
				0.5f, //friendly secret change
				2f, //friendly weapon change

				-0.1f, //enemy health change
				-1f, //enemy hand change
				-3f, //enemy board change
				-0.5f, //enemy secret change
				-2f, //enemy weapon change

				-0.3f //remaining mana
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
		public float TurnReward(POGame.POGame start_state, POGame.POGame end_state, int friendly_id)
		{
			float score = CheckTerminal(end_state);
			if (score != 0) return score;

			//TODO: revise score if needed

			Controller player_start = start_state.CurrentPlayer.Id == friendly_id ? start_state.CurrentPlayer : start_state.CurrentOpponent;
			Controller opponent_start = player_start.Opponent;

			Controller player_end = end_state.CurrentPlayer.Id == friendly_id ? end_state.CurrentPlayer : end_state.CurrentOpponent;
			Controller opponent_end = player_end.Opponent;

			Debug.Assert(player_start.Id == player_end.Id && opponent_end.Id == opponent_start.Id && player_start.Id != opponent_start.Id);


			float player_start_health = player_start.Hero.Health + player_start.Hero.Armor;
			float player_end_health = player_end.Hero.Health + player_end.Hero.Armor;

			HandZone player_start_hand = player_start.HandZone;
			HandZone player_end_hand = player_end.HandZone;

			BoardZone player_start_board = player_start.BoardZone;
			BoardZone player_end_board = player_end.BoardZone;

			SecretZone player_start_secrets = player_start.SecretZone;
			SecretZone player_end_secrets = player_end.SecretZone;

			int player_start_weapon = player_start.Hero.Weapon != null ? 1 : 0;
			int player_end_weapon = player_end.Hero.Weapon != null ? 1 : 0;


			float opponent_start_health = opponent_start.Hero.Health + opponent_start.Hero.Armor;
			float opponent_end_health = opponent_end.Hero.Health + opponent_end.Hero.Armor;

			HandZone opponent_start_hand = opponent_start.HandZone;
			HandZone opponent_end_hand = opponent_end.HandZone;

			BoardZone opponent_start_board = opponent_start.BoardZone;
			BoardZone opponent_end_board = opponent_end.BoardZone;

			SecretZone opponent_start_secrets = opponent_start.SecretZone;
			SecretZone opponent_end_secrets = opponent_end.SecretZone;

			int opponent_start_weapon = opponent_start.Hero.Weapon != null ? 1 : 0;
			int opponent_end_weapon = opponent_end.Hero.Weapon != null ? 1 : 0;


			List<float> features = new List<float>(new float[] {
				player_end_health - player_start_health, //friendly health change
				player_end_hand.Count - player_start_hand.Count, //friendly hand change
				player_end_board.Count - player_start_board.Count, //friendly board change
				player_end_secrets.Count - player_start_secrets.Count, //friendly secret change
				player_end_weapon - player_start_weapon, //friendly weapon change

				opponent_end_health - opponent_start_health, //enemy health change
				opponent_end_hand.Count - opponent_start_hand.Count, //enemy hand change
				opponent_end_board.Count - opponent_start_board.Count, //enemy board change
				opponent_end_secrets.Count - opponent_start_secrets.Count, //enemy secret change
				opponent_end_weapon - opponent_start_weapon, //enemy weapon change

				player_end.RemainingMana //remaining mana
			});

			float reward = features.Zip(weights, (x, y) => x * y).Sum();
			return reward;
		}

		/// <summary>
		/// Calculate the estimated reward of ending your turn on the state 'action' using a Neural Network.
		/// </summary>
		/// <param name="end_state">The state to estimate the score for</param>
		/// <returns></returns>
		public float ActionScore(POGame.POGame end_state, int friendly_id)
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

			int friendly_id = start_state.CurrentPlayer.Id;
			return TurnReward(start_state, end_state, friendly_id) + ActionScore(end_state, friendly_id);
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

			int friendly_id = p1_start.CurrentPlayer.Id;
			int opp_id = p1_start.CurrentPlayer.Opponent.Id;
			return TurnReward(p1_start, p1_end, friendly_id) - opponent_score_modifier * TurnReward(p1_end, p2_end, friendly_id);
		}
	}
}
