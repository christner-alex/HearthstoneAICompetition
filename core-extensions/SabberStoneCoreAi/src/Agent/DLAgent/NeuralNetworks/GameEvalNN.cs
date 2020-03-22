using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using System.Diagnostics;
using Tensorflow;
using static Tensorflow.Binding;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameEvalNN
	{
		public const int rnn_units = 5;

		protected Tensor hand_input, minions_input, board_hist_input, target;

		public readonly int minions_input_row, minions_input_col, full_history_length;

		public const int num_hand_filters = 3;
		public const int num_minions_filters = 3;

		protected Tensor online_pred, target_pred;

		protected List<RefVariable> online_vars, target_vars;

		private Tensor loss;
		private Optimizer optim;
		private Operation train_op;

		private Session sess;

		public GameEvalNN()
		{
			minions_input_row = GameRep.max_side_minions * GameRep.max_side_minions;
			minions_input_col = GameRep.minion_vec_len * 2;
			full_history_length = GameRep.max_num_history + 1;

			//inputs for the various parts of the GameRep
			hand_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, GameRep.max_hand_cards, GameRep.card_vec_len), name: "hand_input");
			minions_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, minions_input_row, minions_input_col), name: "minions_input");
			board_hist_input = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(-1, full_history_length, GameRep.max_num_boards, GameRep.board_vec_len), name: "board_hist_input");

			target = tf.placeholder(TF_DataType.TF_FLOAT, new TensorShape(-1), name: "target_input");

			//create identical graphs for the online network and the target network
			online_pred = CreateSubgraph("online");
			target_pred = CreateSubgraph("target");

			//get the trainable variables of each subgraph
			online_vars = tf.get_collection<RefVariable>(tf.GraphKeys.TRAINABLE_VARIABLES, "online");
			target_vars = tf.get_collection<RefVariable>(tf.GraphKeys.TRAINABLE_VARIABLES, "target");

			optim = tf.train.AdamOptimizer(0.01f);
			loss = tf.reduce_mean(tf.pow(target - target, 2.0f) / 2.0f);
			train_op = optim.minimize(loss, var_list: online_vars);

			sess = null;
		}

		private Tensor CreateSubgraph(string name)
		{
			Tensor pred = null;

			tf_with(tf.variable_scope(name), delegate
			{
				var hand_reshaped = tf.reshape(hand_input, new int[] { -1, GameRep.max_hand_cards, GameRep.card_vec_len, 1 });
				var minions_reshaped = tf.reshape(minions_input, new int[] { -1, minions_input_row, minions_input_col, 1 });
				var boards_reshaped = tf.reshape(board_hist_input, new int[] { -1, full_history_length, GameRep.max_num_boards * GameRep.board_vec_len });

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
				var rnnCell = tf.nn.rnn_cell.BasicRNNCell(rnn_units);
				var (_,rnn_final) = tf.nn.dynamic_rnn(
					cell: rnnCell,
					inputs: boards_reshaped
					);

				var hand_flat = tf.reshape(hand_conv, new int[] { -1, GameRep.max_hand_cards * num_hand_filters });
				var minions_flat = tf.reshape(minions_conv, new int[] { -1, minions_input_row * num_hand_filters });
				var boards_flat = tf.reshape(rnn_final, new int[] { -1, rnn_units });
				var combined = tf.concat(new List<Tensor>() { hand_flat, minions_flat, boards_flat }, 1);

				var dense1 = tf.layers.dense(
					inputs: combined,
					units: 10,
					name: "last_layer"
					);

				pred = tf.layers.dense(
					inputs: dense1,
					units: 1,
					name: "prediction"
					);
			});

			return pred;
		}

		public float ScoreStates(params GameRep[] reps)
		{
			return 0;
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

		public bool StartSession()
		{
			if (sess == null)
			{
				sess = tf.Session();
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool EndSession()
		{
			if (sess != null)
			{
				sess.close();
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}
