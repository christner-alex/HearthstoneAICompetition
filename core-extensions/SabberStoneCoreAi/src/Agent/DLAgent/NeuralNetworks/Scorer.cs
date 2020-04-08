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

		public static float WinScore = 200f;

		public static float LossScore = -200f;

		public static NDArray friendly_diff_weights = np.array(
				0.01f, //player_health
				0.5f, //player_base_mana
				-0.5f, //player_remaining_mana
				0.1f, //player_hand_size
				0.3f, //player_board_size
				0.01f, //player_deck_size
				0.2f, //player_secret_size
				0f, //graveyard size
				0.03f, //player_total_atk
				0.03f, //player_total_health
				0.03f, //player_taunt_health
				0.1f, //hero attack damage
				0.1f, //hero weapon durability
				0f, //hero power activations
				0f //hand cost
			);

		public static NDArray enemy_diff_weights = np.array(
				-0.01f, //player_health
				-0.5f, //player_base_mana
				0.5f, //player_remaining_mana
				-0.1f, //player_hand_size
				-0.3f, //player_board_size
				-0.01f, //player_deck_size
				-0.2f, //player_secret_size
				0f, //graveyard size
				-0.03f, //player_total_atk
				-0.03f, //player_total_health
				-0.03f, //player_taunt_health
				-0.1f, //hero attack damage
				-0.1f, //hero weapon durability
				0f, //hero power activations
				0f //hand cost
			);

		public static NDArray friendly_end_weights = np.array(
				0f, //player_health
				0f, //player_base_mana
				0f, //player_remaining_mana
				0f, //player_hand_size
				0f, //player_board_size
				0f, //player_deck_size
				0f, //player_secret_size
				0f, //graveyard size
				0f, //player_total_atk
				0f, //player_total_health
				0f, //player taunt_health
				0f, //hero attack damage
				0f, //hero weapon durability
				0f, //hero power activations
				0f //hand cost
			);

		public static NDArray enemy_end_weights = np.array(
				0f, //player_health
				0f, //player_base_mana
				0f, //player_remaining_mana
				0f, //player_hand_size
				0f, //player_board_size
				0f, //player_deck_size
				0f, //player_secret_size
				0f, //graveyard size
				0f, //player_total_atk
				0f, //player_total_health
				0f, //player taunt_health
				0f, //hero attack damage
				0f, //hero weapon durability
				0f, //hero power activations
				0f //hand cost
			);

		public GameEvalDQN Network { get; }

		public float Gamma { get; set; }

		public Scorer(GameEvalDQN network = null, float gamma = 0.99f)
		{
			Gamma = gamma;

			Network = network;

			CheckRep();
		}

		/// <summary>
		/// Calculate the reward of starting in 'state' and ending your turn on the state 'action'.
		/// </summary>
		/// <param name="start_state">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public static float TurnReward(GameRep start_state, GameRep end_state)
		{
			NDArray start_board = start_state.BoardRep.astype(NPTypeCode.Float);
			NDArray end_board = end_state.BoardRep.astype(NPTypeCode.Float);

			NDArray friendly_start_board = start_board[0];
			NDArray enemy_start_board = start_board[1];
			NDArray friendly_end_board = end_board[0];
			NDArray enemy_end_board = end_board[1];

			//return the win/loss scores if health is 0 or less
			if(friendly_end_board.GetValue<float>(0) <= 0 )
			{
				return LossScore;
			}
			else if(enemy_end_board.GetValue<float>(0) <= 0)
			{
				return WinScore;
			}

			//vectors representing the change in each board
			NDArray friendly_difference = friendly_end_board - friendly_start_board;
			NDArray enemy_difference = enemy_end_board - enemy_start_board;

			NDArray result = friendly_difference.dot(friendly_diff_weights)
				+ enemy_difference.dot(enemy_diff_weights)
				+ friendly_end_board.dot(friendly_end_weights)
				+ enemy_end_board.dot(enemy_end_weights);
			return result.astype(NPTypeCode.Float).GetValue<float>(0);
		}

		public static NDArray TurnReward(GameRep[] start_states, GameRep[] end_states)
		{
			var l = from int i in Enumerable.Range(0, start_states.Length)
					select TurnReward(start_states[i], end_states[i]);
			return np.array(l.ToArray()).astype(NPTypeCode.Float);
		}

		/// <summary>
		/// Calculate the estimated reward gained after ending a turn with the end_state from the start_state
		/// </summary>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public float FutureRewardEstimate(GameRep end_state, bool use_online)
		{
			if (Network == null) return 0f;

			var result = Network.ScoreStates(use_online, end_state).GetValue<float>(0);

			return result;
		}

		public NDArray FutureRewardEstimate(GameRep[] end_states, bool use_online)
		{
			if (Network == null) return np.zeros(end_states.Length).astype(NPTypeCode.Float);

			var result = Network.ScoreStates(use_online, end_states);

			return result;
		}

		/// <summary>
		/// Calculate the Q value of the (state, action) pair, which is the sum of the reward gained turn your turn
		/// plus the estimated reward of 
		/// </summary>
		/// <param name="start_state">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="end_state">The GameRep representing the result of taking the end turn action</param>
		public float Q(GameRep start_state, GameRep end_state, bool use_online)
		{
			return TurnReward(start_state, end_state)
				+ FutureRewardEstimate(end_state, use_online);
		}

		public NDArray Q(GameRep[] start_states, GameRep[] end_states, bool use_online)
		{
			NDArray turn = TurnReward(start_states, end_states);
			NDArray future = FutureRewardEstimate(end_states, use_online);
			return np.add(turn, future).astype(NPTypeCode.Float);
		}

		public NDArray Q(GameRep start_state, GameRep[] end_states, bool use_online)
		{
			var start_states = Enumerable.Repeat(start_state, end_states.Length).ToArray();
			return Q(start_states, end_states, use_online);
		}

		/*
		/// <summary>
		/// Calculate the score observed from a (state,action,state) transistion, which is the difference between
		/// the reward gained by the current player on their turn and the opposite of the reward gained by the
		/// opposing player on their turn.
		/// </summary>
		/// <param name="turn1start">The GameRep representing the start of the turn meant to be scores</param>
		/// <param name="turn1end">The GameRep representing the result of taking the end turn action from p1_start</param>
		/// <param name="turn1end">The GameRep representing the start of the players turn after opponent ended their turn from p1_end</param>
		public float ScoreTransition(GameRep turn1start, GameRep turn1end, GameRep turn2start)
		{
			return TurnReward(turn1start, turn1end) - TurnReward(turn1end, turn2start);
		}
		*/

		/// <summary>
		/// Use the target portion of the network to create target values for each of the transitions
		/// </summary>
		/// <param name="transitions"></param>
		/// <returns></returns>
		public NDArray CreateTargets(params GameRecord.TransitionRecord[] transitions)
		{
			//target = r + lambda * max_a' Q(s', a')
			var l = from t in transitions select t.reward==WinScore || t.reward==LossScore ? t.reward : t.reward + Gamma * (t.successor_actions?.DoubleQScore(this) ?? 0);
			return np.array(l.ToArray()).astype(NPTypeCode.Float);
		}

		private bool CheckRep()
		{
			if(!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			return result;
		}
	}
}
