using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCoreAi.Tyche2
{
	internal static class SecretUtils
	{
		private const float SECRET_VALUE_FACTOR = 3.0f;
		private const float ESTIMATED_SECRET_COST = 2.5f;
		private const float ESTIMATED_SECRET_VALUE = ESTIMATED_SECRET_COST * SECRET_VALUE_FACTOR;

		private static Dictionary<string, Action<State, State, Controller, Controller, Spell>> _secretDictionary;

		private static void Init()
		{
			_secretDictionary = new Dictionary<string, Action<State, State, Controller, Controller, Spell>>();

			_secretDictionary.Add("Potion of Polymorph", PotionOfPolymorph);
			_secretDictionary.Add("Explosive Runes", ExplosiveRunes);
			_secretDictionary.Add("Mirror Entity", MirrorEntity);
			_secretDictionary.Add("Frozen Clone", FrozenClone);
			_secretDictionary.Add("Spellbender", Spellbender);
			_secretDictionary.Add("Ice Barrier", IceBarrier);
			_secretDictionary.Add("Ice Block", IceBlock);
			_secretDictionary.Add("Vaporize", Vaporize);

			//TODO: Counterspell: <b>Secret:</b> When your opponent casts a spell, <b>Counter</b> it.
			//TODO: Mana Bind: <b>Secret:</b> When your opponent casts a spell, add a copy to your hand that costs (0).
		}

		/// <summary> Estimates values for the secrets on the table. Does not look at the secrets themselves. </summary>
		public static void EstimateValues(State state, Controller player)
		{
			state.BiasValue += player.SecretZone.Count * ESTIMATED_SECRET_VALUE;
		}

		public static void CalculateValues(State playerState, State opponentState, Controller player,
			Controller opponent)
		{
			if (_secretDictionary == null)
				Init();

			foreach (var secret in player.SecretZone)
			{
				var key = secret.Card.Name;

				if (_secretDictionary.ContainsKey(key))
				{
					var action = _secretDictionary[key];
					action(playerState, opponentState, player, opponent, secret);
				}

				else
				{
					if (TycheAgentConstants.LOG_UNKNOWN_SECRETS)
						DebugUtils.LogWarning("Unknown secret: " + secret.Card.FullPrint());

					playerState.BiasValue += secret.Card.Cost * SECRET_VALUE_FACTOR;
				}
			}
		}

		//After your opponent plays a minion, transform it into a 1/1 Sheep.
		private static void PotionOfPolymorph(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			var opponentMana = opponent.GetAvailableMana();

			//punish playing early:
			playerState.BiasValue += StateUtility.LateReward(opponentMana, 5, 5.0f);

			//value is the difference between an average minion and the sheep:
			var sheepValue = MinionUtils.ComputeMinionValue(1, 1, 1);
			var averageMinionValue = MinionUtils.EstimatedValueFromMana(opponentMana);
			var polymorphedValue = sheepValue - averageMinionValue;
			opponentState.MinionValues += polymorphedValue;
		}

		//After your opponent plays a minion, summon a copy of it.
		private static void MirrorEntity(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			var mana = opponent.GetAvailableMana();
			var minion = MinionUtils.EstimatedValueFromMana(mana);
			playerState.BiasValue += StateUtility.LateReward(mana, 4, 5.0f);
			playerState.MinionValues += minion;
		}

		//When your hero takes fatal damage, prevent it and become Immune this turn.
		private static void IceBlock(State playerState, State opponentState, Controller player, Controller opponent,
			Spell secret)
		{
			//give punishment when at full hp, give reward if hp lessens

			const int MAX_HEALTH = 30;
			const int MIN_HEALTH = 1;
			var healthPercent = 1.0f - Utils.InverseLerp(playerState.HeroHealth, MIN_HEALTH, MAX_HEALTH);

			//punishment when at full hp:
			const float MIN_VALUE = -30.0f;
			//reward when at 1 hp:
			const float MAX_VALUE = 45.0f;

			var value = Utils.Lerp(MIN_VALUE, MAX_VALUE, healthPercent);
			playerState.BiasValue += value;
		}

		//When your hero is attacked, gain 8 Armor.
		private static void IceBarrier(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			playerState.HeroArmor += 8;
		}

		//After your opponent plays a minion, add two copies of it to_your hand.
		private static void FrozenClone(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			var mana = opponent.GetAvailableMana();
			var minion = MinionUtils.EstimatedValueFromMana(mana);
			//dont multiply by 2, because player still has to play the minions:
			playerState.BiasValue += minion * 1.75f + StateUtility.LateReward(mana, 4, 4.0f);
		}

		//When an enemy casts a spell on a minion, summon a 1/3 as the new target.
		private static void Spellbender(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			var myMana = player.GetAvailableMana();
			var possibleAverageMinion = MinionUtils.EstimatedValueFromMana(myMana);
			var myAverageMinion = playerState.GetAverageMinionValue();
			//dont play if my minions are weaker than a "good" minion at that point in game, also punish when played early:
			playerState.BiasValue +=
				myAverageMinion - possibleAverageMinion + StateUtility.LateReward(myMana, 4, 2.0f);
		}

		//When a minion attacks your hero, destroy it.
		private static void Vaporize(State playerState, State opponentState, Controller player, Controller opponent,
			Spell secret)
		{
			var opponentMana = opponent.GetAvailableMana();

			//punish playing early:
			playerState.BiasValue += StateUtility.LateReward(opponentMana, 5, 5.0f);

			//estimate destroying an enemy minion:
			var avgMinionValue = MinionUtils.EstimatedValueFromMana(opponentMana);
			opponentState.MinionValues -= avgMinionValue;
		}

		//After your opponent plays a minion, deal $6 damage to it and any excess to their hero
		private static void ExplosiveRunes(State playerState, State opponentState, Controller player,
			Controller opponent, Spell secret)
		{
			//doesnt matter if played early or late (early: deals damage to hero, later will most likely kill a minion)

			//multiply with a factor because either it kills a minion (higher value than just the damage dealt)
			//or/and it deals damage to the hero (also worth more than just reducing the hp)
			const float FACTOR = 2.0f;
			const int BASE_DAMAGE = 6;
			opponentState.BiasValue -= (BASE_DAMAGE + player.CurrentSpellPower) * FACTOR;
		}
	}
}
