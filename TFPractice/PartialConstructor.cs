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
	class PartialConstructor
	{
		public static (Tensor, List<RefVariable>) CreatePartial(Tensor x, string n)
		{
			Tensor mult = null;
			RefVariable W = null;

			NDArray data = np.array(2);

			tf_with(tf.variable_scope(n), delegate
			{
				W = tf.Variable(data, name: "weights");
				mult = tf.multiply(x, W);
			});

			List<RefVariable> trainable = tf.get_collection<RefVariable>(tf.GraphKeys.TRAINABLE_VARIABLES, n);

			return (mult, trainable);
		}
	}
}
