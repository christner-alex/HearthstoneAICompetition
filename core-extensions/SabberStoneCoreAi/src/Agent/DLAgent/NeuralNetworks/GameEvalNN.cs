using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using System.Diagnostics;
using Tensorflow;
using static Tensorflow.Binding;

namespace SabberStoneCoreAi.Agent.DLAgent.NeuralNetworks
{
	class GameEvalNN
	{
		public int history_len;

		private Tensor hand_input, minions_input, board_input;

		private int minions_input_row, minions_input_col;

		private int num_hand_filters = 3;
		private int num_minions_filters = 3;
		private int rnn_nodes;

		public GameEvalNN()
		{
			minions_input_row = GameRep.max_side_minions * GameRep.max_side_minions;
			minions_input_col = GameRep.minion_vec_len * 2;

			rnn_nodes = GameRep.max_num_boards * GameRep.board_vec_len;

			//inputs for the various parts of the GameRep
			hand_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, GameRep.max_hand_cards, GameRep.card_vec_len), name: "hand_input");
			minions_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, minions_input_row, minions_input_col), name: "minions_input");
			board_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, history_len, GameRep.max_num_boards, GameRep.board_vec_len), name: "boards_input");
		}

		private void CreateSubgraph(string name)
		{

			tf_with(tf.variable_scope(name), delegate
			{
				var hand_reshaped = tf.reshape(hand_input, new int[] { -1, GameRep.max_hand_cards, GameRep.card_vec_len, 1 });
				var minions_reshaped = tf.reshape(minions_input, new int[] { -1, minions_input_row, minions_input_col, 1 });
				var boards_reshaped = tf.reshape(board_input, new int[] { -1, history_len, rnn_nodes });

				//a 1d convolution which passes over each card in the hand
				//returns [-1, max_hand_cards, 1, 3]
				var hand_conv = tf.layers.conv2d(
					inputs: hand_reshaped,
					filters: num_hand_filters,
					kernel_size: new int[] { 1, GameRep.card_vec_len },
					strides: new int[] { 0, 1 }
					);

				//1d convolution over the pairs of minions
				//returns [-1, minions_input_row, 1, 3]
				var minions_conv = tf.layers.conv2d(
					inputs: minions_reshaped,
					filters: num_minions_filters,
					kernel_size: new int[] { 1, minions_input_col },
					strides: new int[] { 0, 1 }
					);

				//RNN over board history
				//returns [-1, GameRep.max_num_boards * GameRep.board_vec_len]
				var rnnCell = tf.nn.rnn_cell.BasicRNNCell(rnn_nodes);
				var (_,rnn_final) = tf.nn.dynamic_rnn(
					cell: rnnCell,
					inputs: boards_reshaped
					);

				var hand_flat = tf.reshape(hand_conv, new int[] { -1, GameRep.max_hand_cards * num_hand_filters });
				var minions_flat = tf.reshape(minions_conv, new int[] { -1, minions_input_row * num_hand_filters });
				var boards_flat = tf.reshape(rnn_final, new int[] { -1, rnn_nodes });
				var combined = tf.concat(new List<Tensor>() { hand_flat, minions_flat, boards_flat }, 1);

				var dense1 = tf.layers.dense(
					inputs: combined,
					units: 10
					);

				var output = tf.layers.dense(
					inputs: dense1,
					units: 1
					);
			});
		}

		public float ScoreStates(params GameRep[] reps)
		{
			return 0;
		}

		private NDArray ConstructMinionPairs(GameRep rep)
		{
			NDArray friendly_minions = rep.FriendlyMinionRep;
			NDArray[] boards = new NDArray[GameRep.max_side_minions];

			for(int r=0; r<GameRep.max_side_minions; r++)
			{
				NDArray enemy_board = rep.EnemyMinionRep.roll(r, 0);
				boards[r] = np.concatenate(new NDArray[] { friendly_minions, enemy_board }, 1);
			}

			return np.concatenate(boards, 0);
		}

		private NDArray ContructBoardHistory(GameRep rep)
		{
			return np.zeros(1);
		}

		public void TrainStep()
		{

		}

		public void SaveModel()
		{

		}

		public void LoadModel()
		{

		}
	}
}
