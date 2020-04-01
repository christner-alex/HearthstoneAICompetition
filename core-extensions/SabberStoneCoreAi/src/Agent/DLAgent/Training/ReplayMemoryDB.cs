using Npgsql;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static SabberStoneCoreAi.Agent.DLAgent.MaxTree;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class ReplayMemoryDB
	{
		private const string CONNECT_STRING = "Host=localhost; Database=dl_project_db; Username=dl_user; Password=dl_password";

		private const string COUNT_QUERY = "SELECT COUNT(*) FROM transitions;";
		private const string MAX_ID_QUERY = "SELECT MAX(id) FROM transitions;";
		private const string MIN_ID_QUERY = "SELECT MIN(id) FROM transitions;";

		private const string NONE = "none";

		private NpgsqlConnection conn;

		private Random rnd;

		/// <summary>
		/// The id to give the next tuple added to the database. Should always be one more than the largest currently existing id
		/// </summary>
		private int current_id;

		/// <summary>
		/// The maximum number of tuples to keep in the database
		/// </summary>
		private const int replayMemorySize = 10000;

		private Mutex mutex;
		
		public ReplayMemoryDB()
		{
			conn = new NpgsqlConnection(CONNECT_STRING);
			conn.Open();

			current_id = 0;

			mutex = new Mutex();

			rnd = new Random();
		}

		public void Close()
		{
			conn.Close();
			mutex.Close();
		}

		public void Initialize()
		{
			mutex.WaitOne();

			//get the maximum id in the database
			NpgsqlCommand cmd = new NpgsqlCommand(MAX_ID_QUERY, conn);
			NpgsqlDataReader rdr = cmd.ExecuteReader();
			while (rdr.Read())
			{
				current_id = rdr.GetInt32(0) + 1;
			}
			rdr.Close();

			Console.WriteLine("Initialized current_id to {0}", current_id);

			mutex.ReleaseMutex();
		}

		public void Push(GameRecord.TransitionRecord rec)
		{
			Push(new List<GameRecord.TransitionRecord>() { rec });
		}

		private void EnforceSize(int toAdd)
		{
			int overflow = -1;
			NpgsqlCommand cmd = new NpgsqlCommand(COUNT_QUERY, conn);
			NpgsqlDataReader rdr = cmd.ExecuteReader();
			while (rdr.Read())
			{
				overflow = rdr.GetInt32(0) + toAdd - replayMemorySize;
			}
			rdr.Close();


			//delete the oldest tuples from the database if there are more than the maximum
			if (overflow > 0)
			{
				int minId = -1;
				cmd = new NpgsqlCommand(MIN_ID_QUERY, conn);
				rdr = cmd.ExecuteReader();
				while (rdr.Read())
				{
					minId = rdr.GetInt32(0);
				}
				rdr.Close();

				if (minId >= 0)
				{
					string comand = $"DELETE FROM transitions WHERE id<{minId + toAdd};";
					cmd = new NpgsqlCommand(comand, conn);
					cmd.ExecuteNonQuery();
				}
			}
		}

		public void Push(List<GameRecord.TransitionRecord> recs)
		{
			mutex.WaitOne();

			try
			{
				EnforceSize(recs.Count);

				foreach (GameRecord.TransitionRecord record in recs)
				{
					string state = JsonConvert.SerializeObject(record.state);
					string action = JsonConvert.SerializeObject(record.action);
					float reward = record.reward;
					string successor = record.successor != null ? JsonConvert.SerializeObject(record.successor) : NONE;
					string successor_actions = record.successor_actions != null ? JsonConvert.SerializeObject(record.successor_actions) : NONE;

					string comand = $"INSERT INTO transitions (state, action, reward, successor, successor_actions, id) VALUES ('{state}', '{action}', {reward}, '{successor}', '{successor_actions}', {current_id});";
					NpgsqlCommand cmd = new NpgsqlCommand(comand, conn);
					cmd.ExecuteNonQuery();

					current_id++;
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine("Failed to save data");
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}

			mutex.ReleaseMutex();
		}

		public GameRecord.TransitionRecord[] Sample(int batchSize)
		{
			mutex.WaitOne();

			GameRecord.TransitionRecord[] result = new GameRecord.TransitionRecord[batchSize];

			try
			{
				var ids = from r in Enumerable.Range(0, batchSize) select rnd.Next(Math.Max(0, current_id-replayMemorySize), current_id);
				string id_str = String.Join(",", ids.ToArray());

				string sample_query = $"SELECT * FROM transitions WHERE id IN ({id_str});";
				var cmd = new NpgsqlCommand(sample_query, conn);
				NpgsqlDataReader rdr = cmd.ExecuteReader();

				int i = 0;
				while (rdr.Read())
				{
					GameRecord.TransitionRecord newRec = new GameRecord.TransitionRecord();

					string statr_str = rdr.GetString(0);
					string action_str = rdr.GetString(1);
					float reward = rdr.GetFloat(2);
					string successor_str = rdr.GetString(3);
					string succ_act_str = rdr.GetString(4);

					newRec.state = JsonConvert.DeserializeObject<GameRep>(statr_str);
					newRec.action = JsonConvert.DeserializeObject<GameRep>(action_str);
					newRec.reward = reward;
					newRec.successor = successor_str.Equals(NONE) ? null : JsonConvert.DeserializeObject<GameRep>(successor_str);
					newRec.successor_actions = succ_act_str.Equals(NONE) ? null :  JsonConvert.DeserializeObject<SparseTree>(succ_act_str);

					result[i] = newRec;
					i++;
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine("Failed to sample data");
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}

			mutex.ReleaseMutex();

			return result;
		}
	}
}
