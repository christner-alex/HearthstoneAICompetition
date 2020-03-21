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
	class AddTwoGraph
	{
		public int Run(Session sess, int x1_in)
		{
			var x1 = tf.placeholder(TF_DataType.TF_INT32, new TensorShape(1));
			var v = tf.Variable(new int[] { 2 });

			var addition = tf.reduce_sum(tf.multiply(x1, v));

			int o1 = sess.run(addition, new FeedItem(x1, x1_in));

			return o1;
		}
	}
}
