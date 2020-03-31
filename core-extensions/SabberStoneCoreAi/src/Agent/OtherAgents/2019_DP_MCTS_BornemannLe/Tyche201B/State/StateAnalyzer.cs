using System;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	// Original idea of state estimation based on:
	// https://www.reddit.com/r/hearthstone/comments/7l1ob0/i_wrote_a_masters_thesis_on_effective_hearthstone/
	// Extended by adding and changing weights, adding armor, adding weapon durability and weapon damage, custom values for special abilities and multiple attacks per round (like windfury)
	internal class StateAnalyzer
	{
		public bool EstimateSecretsAndSpells = true;
		public int OwnPlayerId = -1;
		public StateWeights Weights;

		public StateAnalyzer()
			: this(new StateWeights())
		{
		}

		public StateAnalyzer(StateWeights weights)
		{
			Weights = weights;
		}

		public bool IsMyPlayer(Controller c)
		{
			DebugUtils.Assert(OwnPlayerId != -1);
			return c.PlayerId == OwnPlayerId;
		}

		public float GetStateValue(State playerState, State enemyState, Controller player, Controller opponent,
			PlayerTask task)
		{
			DebugUtils.Assert(IsMyPlayer(player));
			DebugUtils.Assert(!IsMyPlayer(opponent));

			if (EstimateSecretsAndSpells)
			{
				SecretUtils.CalculateValues(playerState, enemyState, player, opponent);
				SecretUtils.EstimateValues(enemyState, opponent);

				var spell = task.TryGetSpell();

				if (spell != null && !spell.IsSecret)
					SpellUtils.CalculateValues(playerState, enemyState, player, opponent, task, spell);
			}

			if (HasLost(enemyState))
				return Single.PositiveInfinity;

			if (HasLost(playerState))
				return Single.NegativeInfinity;

			return GetStateValueFor(playerState, enemyState) - GetStateValueFor(enemyState, playerState);
		}

		private float GetStateValueFor(State player, State enemy)
		{
			var emptyFieldValue = Weights.GetWeight(StateWeights.WeightType.EmptyField) * GetEmptyFieldValue(enemy);
			var healthValue = Weights.GetWeight(StateWeights.WeightType.HealthFactor) *
			                  GetHeroHealthArmorValue(player);
			var deckValue = Weights.GetWeight(StateWeights.WeightType.DeckFactor) * GetDeckValue(player);
			var handValue = Weights.GetWeight(StateWeights.WeightType.HandFactor) * GetHandValues(player);
			var minionValue = Weights.GetWeight(StateWeights.WeightType.MinionFactor) * GetMinionValues(player);
			var biasValues = Weights.GetWeight(StateWeights.WeightType.BiasFactor) * GetBiasValue(player);

			return emptyFieldValue + deckValue + healthValue + handValue + minionValue + biasValues;
		}

		private float GetBiasValue(State player)
		{
			return player.BiasValue;
		}

		private float GetMinionValues(State player)
		{
			//treat the hero weapon as an additional minion with damage and health:
			return player.MinionValues + player.WeaponDamage * player.WeaponDurability;
		}

		private bool HasLost(State player)
		{
			return player.HeroHealth <= 0;
		}

		/// <summary> Gives points for clearing the minion zone of the given opponent. </summary>
		private float GetEmptyFieldValue(State state)
		{
			//its better to clear the board in later stages of the game (more enemies might appear each round):
			if (state.NumMinionsOnBoard == 0)
				return 2.0f + Math.Min(state.TurnNumber, 10.0f);

			return 0.0f;
		}

		/// <summary> Gives points for having cards in the deck. Having no cards give additional penality. </summary>
		private float GetDeckValue(State state)
		{
			var numCards = state.NumDeckCards;
			return (float) Math.Sqrt(numCards) - state.Fatigue;
		}

		/// <summary> Gives points for having health, treat armor as additional health. </summary>
		private float GetHeroHealthArmorValue(State state)
		{
			return (float) Math.Sqrt(state.HeroHealth + state.HeroArmor);
		}

		/// <summary> Gives points for having cards in the hand. </summary>
		private float GetHandValues(State state)
		{
			var firstThree = Math.Min(state.NumHandCards, 3);
			var remaining = Math.Abs(state.NumHandCards - firstThree);
			//3 times the points for the first three cards, 2 for all remaining cards:
			return 3 * firstThree + 2 * remaining;
		}
	}
}
