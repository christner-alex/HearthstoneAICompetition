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
	class TripleGraph
	{
		private Tensor x;

		private Tensor op;

		public TripleGraph()
		{
			x = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(1), name: "X");

			var W = tf.Variable(new Tensor(new int[] { 3, 4 }));

			var mul = tf.matmul(x, W);

			op = tf.max(mul, 0, name: "max");
		}

		public TripleGraph(Session sess, string path)
		{
			var new_saver = tf.train.import_meta_graph(path);
			new_saver.restore(sess, tf.train.latest_checkpoint("models"));
		}

		public int Run(Session sess, int x_in)
		{
			return sess.run(op, new FeedItem(x, x_in));
		}

		public void Save(Session sess, int step)
		{
			var saver = tf.train.Saver();
			var save_path = saver.save(sess, "models/model.ckpt", step);
			tf.train.write_graph(sess.graph, "models", "model.pbtxt", as_text: true);
		}

	}
}
