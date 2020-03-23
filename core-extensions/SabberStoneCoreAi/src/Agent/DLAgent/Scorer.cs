using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using System.Linq;
using System.Diagnostics;
using NumSharp;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class Scorer
	{

		public readonly float WinScore;
		public readonly float LossScore;
		public readonly float OpponentScoreModifier;

		private readonly NDArray diff_weights;
		private readonly NDArray end_weights;

		public GameEvalNN Network { get; }

		public Scorer(GameEvalNN network = null, float win_score = 100f, float loss_score = -100f, float opponent_modifier = 0.8f)
		{
			diff_weights = np.array(
				0.1f, //player_health
				0f, //player_base_mana
				0f, //player_remaining_mana
				0.2f, //player_hand_size
				0.5f, //player_board_size
				0.01f, //player_deck_size
				0.3f, //player_secret_size
				0.1f, //player_total_atk
				0.1f, //player_total_health
				0.2f, //player taunt_health
				0.5f, //player_weapon_atk
				0.2f //player_weapon_dur
			);

			end_weights = np.zeros(new Shape(GameRep.board_vec_len), NPTypeCode.Float);
			end_weights[2] = -1f;

			WinScore = win_score;
			LossScore = loss_score;
			OpponentScoreModifier = opponent_modifier;

			Network = network;

			CheckRep();
		}

		/// <summary>
		/// Calculate the reward of starting in 'state' and ending your turn on the state 'action'.
		/// </summary>
		/// <param name="start_state">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public float TurnReward(GameRep start_state, GameRep end_state, bool modify_enemy_score = false)
		{
			NDArray start_board = start_state.BoardRep.astype(NPTypeCode.Float);
			NDArray end_board = end_state.BoardRep.astype(NPTypeCode.Float);

			NDArray friendly_start_board = start_board[0];
			NDArray enemy_start_board = start_board[1];
			NDArray friendly_end_board = end_board[0];
			NDArray enemy_end_board = end_board[1];

			NDArray friendly_difference = friendly_end_board - friendly_start_board;
			NDArray enemy_difference = enemy_end_board - enemy_start_board;

			float modify = modify_enemy_score ? 1f : OpponentScoreModifier;
			NDArray result = friendly_difference.dot(diff_weights)
				- modify * enemy_difference.dot(diff_weights)
				+ friendly_end_board.dot(end_weights)
				- modify * enemy_end_board.dot(end_weights);
			return result.astype(NPTypeCode.Float).ToArray<float>()[0];
		}

		/// <summary>
		/// Calculate the estimated reward gained after ending a turn with the end_state from the start_state
		/// </summary>
		/// <param name="start_state">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public float FutureRewardEstimate(GameRep start_state, GameRep end_state)
		{
			//TODO implement neural network
			if (Network == null) return 0f;
			return Network.ScoreStates(end_state).GetValue<float>(0);
		}

		/// <summary>
		/// Calculate the Q value of the (state, action) pair, which is the sum of the reward gained turn your turn
		/// plus the estimated reward of 
		/// </summary>
		/// <param name="start_state">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public NDArray Q(GameRep start_state, GameRep end_state)
		{
			return TurnReward(start_state, end_state)
				+ FutureRewardEstimate(start_state, end_state);
		}

		/// <summary>
		/// Calculate the score observed from a (state,action,state) transistion, which is the difference between
		/// the reward gained by the current player on their turn and the opposite of the reward gained by the
		/// opposing player on their turn.
		/// </summary>
		/// <param name="p1_start">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="p1_end">The GameRep representing the result of taking the end turn action from p1_start</param>
		/// <param name="p1_end">The GameRep representing the start of the players turn after opponent ended their turn from p1_end</param>
		public float ScoreTransition(GameRep p1_start, GameRep p1_end, GameRep p2_end)
		{
			return TurnReward(p1_start, p1_end) - OpponentScoreModifier * TurnReward(p1_end, p2_end);
		}

		public NDArray CreateTargets(params GameRecord.TransitionRecord[] transitions)
		{
			GameRecord.TransitionRecord t = transitions[0];
			t.successor_actions.Score(this);

			return null;
		}

		private bool CheckRep()
		{
			if(!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			if(diff_weights.size != end_weights.size || end_weights.size != GameRep.board_vec_len)
			{
				Console.WriteLine("The weight vectors are not the right length");
				result = false;
			}

			return result;
		}
	}
}
