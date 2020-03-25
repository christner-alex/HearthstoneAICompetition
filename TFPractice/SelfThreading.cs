using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TFPractice
{
	class SelfThreading
	{
		private int number;

		public SelfThreading()
		{
			number = 10;
		}

		public void Calc(object mult)
		{
			int value = number * (int)mult;
			Console.WriteLine("Mult is {0}", value);
		}

		public void RunThreads(int num_threads)
		{
			Thread[] threads = new Thread[num_threads];
			for(int i=0; i< num_threads; i++)
			{
				threads[i] = new Thread(Calc);
				threads[i].Start(i);
			}

			for (int i = 0; i < num_threads; i++)
			{
				threads[i].Join();
				Console.WriteLine("Thread {0} joined", i);
			}
		}
	}
}
