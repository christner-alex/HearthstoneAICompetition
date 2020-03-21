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
    class Program
    {
		static void Main(string[] args)
        {
			Tensor x = tf.placeholder(TF_DataType.TF_INT32, new Shape(1));
			(Tensor, List<RefVariable>) n1 = PartialConstructor.CreatePartial(x, "one");
			(Tensor, List<RefVariable>) n2 = PartialConstructor.CreatePartial(x, "two");

			Saver saver = tf.train.Saver(n2.Item2.ToArray());

			using(var sess = tf.Session())
			{
				int o1 = sess.run(n1.Item1, new FeedItem(x, 2));
				int o2 = sess.run(n2.Item1, new FeedItem(x, 3));

				Console.WriteLine(o1);
				Console.WriteLine(o2);
			}
		}
	}
}
