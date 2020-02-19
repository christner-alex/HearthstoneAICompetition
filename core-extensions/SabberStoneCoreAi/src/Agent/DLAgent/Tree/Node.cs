using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Text;
using SabberStoneCoreAi.POGame;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	abstract class Node
	{
		/// <summary>
		/// The Node that is this Node's parent in its MaxTree.
		/// null if a child of the root
		/// </summary>
		public DeterministicNode Predecessor { get; protected set; }

		/// <summary>
		/// The PlayerTask that is taken from this Node's parent to
		/// get to this Node
		/// </summary>
		public PlayerTask Action { get; protected set; }

		/// <summary>
		/// The MaxTree this Node belongs to.
		/// </summary>
		public MaxTree Tree { get; protected set; }

		public int Depth { get; protected set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="p">The parent node. Null if the parent is the MaxTree's root</param>
		/// <param name="a">The PlayerTask that is taken from this Node's parent to get to this Node</param>
		/// <param name="t">The MaxTree this Node belongs to.</param>
		public Node(DeterministicNode p, PlayerTask a,  MaxTree t)
		{
			Predecessor = p;
			Action = a;
			Tree = t;
			Depth = p != null ? p.Depth + 1 : 0;
		}

		public abstract float Score();

		public abstract bool CheckRep();

	}
}
