using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using System.Diagnostics;
using System.IO;
using Tensorflow;
using static Tensorflow.Binding;

namespace TFPractice
{
	class InternalDoubleObject
	{
		private Graph graph;
		private Session sess;

		private Tensor x;
		private RefVariable W;
		private Tensor mult;

		private Operation init;

		private VariableV1[] savables;
		private Saver saver;

		public InternalDoubleObject(string n)
		{
			sess = null;
			graph = new Graph().as_default();

			x = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(1), name: n+"_placeholder");
			W = tf.Variable(2, name: n + "_weight");
			mult = tf.multiply(x, W);

			init = tf.global_variables_initializer();
			saver = tf.train.Saver(savables);

			savables = new VariableV1[]
			{
				W
			};
		}

		public void StartSession()
		{
			sess = tf.Session(graph);
		}

		public int Predict(NDArray input)
		{
			return sess.run(mult, new FeedItem(x, input));
		}

		public void CloseSession()
		{
			if (sess != null) sess.close();
		}

		public void SaveModel(string path)
		{
			if (sess == null) return;
			var save_path = saver.save(sess, path + "/model.ckpt");
			//tf.train.write_graph(sess.graph, path, "model.pbtxt", as_text: true);
		}

		public void LoadModel(string path)
		{
			if (sess == null) return;

			saver.restore(sess, tf.train.latest_checkpoint(path));
		}

		public void Initialize()
		{
			sess.run(init);
		}
		public static void Test()
		{
			var m1 = new InternalDoubleObject("m1");
			var m2 = new InternalDoubleObject("m2");

			m1.StartSession();
			m2.StartSession();

			bool new_model = false;
			if (new_model)
			{
				m1.Initialize();
				m2.Initialize();
			}
			else
			{
				m1.LoadModel("model1");
				m2.LoadModel("model2");
			}

			int a = m1.Predict(3);
			int b = m2.Predict(4);

			Console.WriteLine(a);
			Console.WriteLine(b);

			if (!new_model)
			{
				m1.SaveModel("model1");
				m2.SaveModel("model2");
			}

			m1.CloseSession();
			m2.CloseSession();
		}
	}
}
