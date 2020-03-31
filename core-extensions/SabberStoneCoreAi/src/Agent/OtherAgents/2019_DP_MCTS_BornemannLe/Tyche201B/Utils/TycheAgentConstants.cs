namespace SabberStoneCoreAi.Tyche2
{
	internal static class TycheAgentConstants
	{
		public const bool LOG_UNKNOWN_CORRECTIONS = false;
		public const bool LOG_UNKNOWN_SECRETS = false;

		public const double MAX_EPISODE_TIME = 5.0f;
		public const double MAX_SIMULATION_TIME = 65.0f;
		public const double MAX_TURN_TIME = 70.0;

		public const double DECREASE_SIMULATION_TIME = MAX_SIMULATION_TIME * 0.4;
	}
}
