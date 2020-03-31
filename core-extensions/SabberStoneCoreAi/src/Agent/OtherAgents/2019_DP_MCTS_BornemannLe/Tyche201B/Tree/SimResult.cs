using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	internal class SimResult
	{
		public POGame.POGame state;
		public PlayerTask task;
		public float value;

		public SimResult(POGame.POGame state, PlayerTask task, float value)
		{
			this.state = state;
			this.value = value;
			this.task = task;
		}

		public bool IsBuggy => state == null;
	}
}
