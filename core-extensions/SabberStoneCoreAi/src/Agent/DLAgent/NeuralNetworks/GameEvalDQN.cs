using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using System.Diagnostics;
using Tensorflow;
using static Tensorflow.Binding;
using System.Linq;
using System.Threading;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameEvalDQN
	{
		Mutex mutex;

		//inputs to the network
		protected Tensor hand_input, minions_input, board_hist_input, target;

		//calculated hyperparameters about the network input based on the GameRep object
		public readonly int minions_input_row, minions_input_col, full_history_length;

		//constant hyperparameters about the network
		public const int num_hand_filters = 1;
		public const int num_minions_filters = 1;
		public const int rnn_units = 3;

		public const float regularization_param = 0.00001f;

		//prediction tensors
		protected Tensor online_pred, target_pred;

		//trainable variables belonging to each subgraph
		protected Dictionary<string, RefVariable> online_vars, target_vars;

		//operation for copying online variables to the correspoding target variables
		protected Operation copy_ops;

		protected IInitializer initializer;

		protected Tensor[] online_reg_terms;
		protected Tensor online_reg;

		//bits for training the network
		private Tensor loss;
		private Optimizer optim;
		private Operation train_op;

		//bits for running the network
		protected Session sess;
		private Operation init;
		private Saver saver;

		public GameEvalDQN()
		{
			minions_input_row = GameRep.max_side_minions * GameRep.max_side_minions;
			minions_input_col = GameRep.minion_vec_len * 2;
			full_history_length = GameRep.max_num_history + 1;

			//inputs for the various parts of the GameRep
			hand_input = tf.placeholder(TF_DataType.TF_FLOAT, new TensorShape(-1, GameRep.max_hand_cards, GameRep.card_vec_len), name: "hand_input");
			minions_input = tf.placeholder(TF_DataType.TF_FLOAT, new TensorShape(-1, minions_input_row, minions_input_col), name: "minions_input");
			board_hist_input = tf.placeholder(TF_DataType.TF_FLOAT, new TensorShape(-1, full_history_length, GameRep.max_num_boards, GameRep.board_vec_len), name: "board_hist_input");

			target = tf.placeholder(TF_DataType.TF_FLOAT, new TensorShape(-1), name: "target_input");

			initializer = tf.random_normal_initializer(stddev: 0.1f, dtype: TF_DataType.TF_FLOAT);

			//create identical graphs for the online network and the target network
			(online_pred, online_vars) = CreateSubgraph("online");
			(target_pred, target_vars) = CreateSubgraph("target");

			var cops = from v in target_vars select v.Value.assign(online_vars[v.Key]);
			copy_ops = tf.group(cops.ToArray());

			online_reg_terms = (from v in online_vars select tf.reduce_sum(tf.square(v.Value))).ToArray(); // squared sum of the variable
			online_reg = online_reg_terms.Aggregate((x, y) => x + y);

			optim = tf.train.AdamOptimizer(0.01f);
			loss = tf.reduce_mean(tf.pow(target - online_pred, 2.0f) / 2.0f) + regularization_param * online_reg; //mean squared error with L2 regularization
			train_op = optim.minimize(loss, var_list: online_vars.Values.ToList());

			sess = null;
			init = tf.global_variables_initializer();

			saver = tf.train.Saver();

			mutex = new Mutex();
		}

		private (Tensor PredOp, Dictionary<string, RefVariable> Trainables) CreateSubgraph(string name)
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
					strides: new int[] { 1, 1 },
					kernel_initializer: initializer,
					bias_initializer: initializer
					);

				//1d convolution over the pairs of minions
				//returns [-1, minions_input_row, 1, 3]
				var minions_conv = tf.layers.conv2d(
					inputs: minions_reshaped,
					filters: num_minions_filters,
					kernel_size: new int[] { 1, minions_input_col },
					strides: new int[] { 1, 1 },
					kernel_initializer: initializer,
					bias_initializer: initializer
					);

				//RNN over board history
				//returns [-1, GameRep.max_num_boards * GameRep.board_vec_len]
				//var rnnCell = tf.nn.rnn_cell.BasicRNNCell(rnn_units);
				var rnnCell = new BasicRnnCell(rnn_units, dtype: TF_DataType.TF_FLOAT);
				var rnn_result = tf.nn.dynamic_rnn(
					cell: rnnCell,
					inputs: boards_reshaped,
					dtype: TF_DataType.TF_FLOAT
					);

				var hand_flat = tf.reshape(hand_conv, new int[] { -1, GameRep.max_hand_cards * num_hand_filters });
				var minions_flat = tf.reshape(minions_conv, new int[] { -1, minions_input_row * num_hand_filters });
				var boards_flat = tf.reshape(rnn_result.Item2, new int[] { -1, rnn_units });
				var combined = tf.concat(new List<Tensor>() { hand_flat, minions_flat, boards_flat }, 1);

				var dense1 = tf.layers.dense(
					inputs: combined,
					units: 10,
					name: "last_layer",
					kernel_initializer: initializer,
					bias_initializer: initializer
					);

				pred = tf.layers.dense(
					inputs: dense1,
					units: 1,
					name: "prediction",
					kernel_initializer: initializer,
					bias_initializer: initializer
					);
			});

			var trainable_vars = tf.get_collection<RefVariable>(tf.GraphKeys.TRAINABLE_VARIABLES);

			Dictionary<string, RefVariable> subgraph_dict = (from v in trainable_vars where v.name.StartsWith(name) select v)
				.ToDictionary(v => v.name.Remove(0, name.Length));

			return (pred, subgraph_dict);
		}

		public (NDArray HandIn, NDArray MinionIn, NDArray HistoryIn) UnwrapReps(params GameRep[] reps)
		{
			NDArray hand_in = np.stack((from r in reps select r.HandRep).ToArray());
			NDArray minions_in = np.stack((from r in reps select r.ConstructMinionPairs()).ToArray());
			NDArray history_in = np.stack((from r in reps select r.FullHistoryRep).ToArray());
			return (hand_in, minions_in, history_in);
		}

		public NDArray ScoreStates(bool use_online, params GameRep[] reps)
		{
			mutex.WaitOne();

			var input = UnwrapReps(reps);
			Tensor score_op = use_online ? online_pred : target_pred;
			var result = sess.run(score_op, new FeedItem(hand_input, input.HandIn), new FeedItem(minions_input, input.MinionIn), new FeedItem(board_hist_input, input.HistoryIn));

			mutex.ReleaseMutex();
			return result.flat;
		}

		public float TrainStep(GameRep[] training_points, NDArray targets)
		{
			mutex.WaitOne();

			var input = UnwrapReps(training_points);
			FeedItem[] items = new FeedItem[4] { new FeedItem(hand_input, input.HandIn), new FeedItem(minions_input, input.MinionIn), new FeedItem(board_hist_input, input.HistoryIn), new FeedItem(target, targets) };
			sess.run(train_op, items);
			float l = loss.eval(sess, items).GetValue<float>(0);

			mutex.ReleaseMutex();

			return l;
		}

		public void CopyOnlineToTarget()
		{
			mutex.WaitOne();

			sess.run(copy_ops);
			Console.WriteLine("Copied"); //temporary

			mutex.ReleaseMutex();
		}

		public void SaveModel(int step = -1)
		{
			mutex.WaitOne();

			saver.save(sess, "GameEvalDQN/model.ckpt", global_step: step);

			mutex.ReleaseMutex();
		}

		public void LoadModel()
		{
			mutex.WaitOne();

			saver.restore(sess, tf.train.latest_checkpoint("GameEvalDQN"));
			Console.WriteLine("Loaded model");

			mutex.ReleaseMutex();
		}

		public bool StartSession()
		{
			mutex.WaitOne();

			bool result;
			if (sess == null)
			{
				sess = tf.Session();
				result = true;
			}
			else
			{
				result = false;
			}

			mutex.ReleaseMutex();
			return result;
		}

		public bool EndSession()
		{
			mutex.WaitOne();
			bool result;

			if (sess != null)
			{
				sess.close();
				sess = null;
				result = true;
			}
			else
			{
				result = false;
			}

			mutex.ReleaseMutex();
			return result;
		}

		public void Initialize()
		{
			mutex.WaitOne();

			sess.run(init);

			mutex.ReleaseMutex();
		}
	}
}
