using System;
using System.Collections.Generic;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	internal class SpellUtils
	{
		private static Dictionary<string, Action<State, State, Controller, Controller, PlayerTask, Spell>>
			_spellDictionary;

		private static void Init()
		{
			_spellDictionary =
				new Dictionary<string, Action<State, State, Controller, Controller, PlayerTask, Spell>>();

			_spellDictionary.Add("The Coin", TheCoin);
			_spellDictionary.Add("Maelstrom Portal", MaelstromPortal);
			_spellDictionary.Add("Jade Lightning", JadeLightning);
			_spellDictionary.Add("Lightning Bolt", LightningBolt);
			_spellDictionary.Add("Lightning Storm", LightningStorm);
			_spellDictionary.Add("Hex", Hex);
		}

		public static void CalculateValues(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell)
		{
			if (_spellDictionary == null)
				Init();

			//give reward/punishment if spells cost less/more than usual:
			var diff = spell.Card.Cost - (float) spell.Cost;
			playerState.BiasValue += diff * 1.25f;

			var key = spell.Card.Name;

			if (_spellDictionary.ContainsKey(key))
			{
				var action = _spellDictionary[key];
				action(playerState, opponentState, player, opponent, task, spell);
			}

			else if (TycheAgentConstants.LOG_UNKNOWN_SECRETS)
			{
				DebugUtils.LogInfo("Unknown spell: " + task.FullPrint());
			}
		}

		//Gain 1 Mana Crystal this turn only.
		private static void TheCoin(State playerState, State opponentState, Controller player, Controller opponent,
			PlayerTask task, Spell spell)
		{
			var curMana = player.GetAvailableMana();
			var newMana = curMana + 1;

			var enablesNewCards = false;

			foreach (var card in player.HandZone)
			{
				//if the card can only be played after using the coin, then it is not bad:
				if (card.Cost > curMana && card.Cost <= newMana)
				{
					enablesNewCards = true;
					break;
				}
			}

			//if the coin does not enable to play new cards, give punishment.
			if (!enablesNewCards)
				playerState.BiasValue -= 100.0f;
		}

		//Lightning Storm: Deal 2-3 damage to all enemy minions. Overload: (2)
		private static void LightningStorm(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell)
		{
			//give punishment when having less than this enemies:
			const int NUM_ENEMY_TARGETS = 3;
			playerState.BiasValue += (opponentState.NumMinionsOnBoard - NUM_ENEMY_TARGETS) * 1.25f;
		}

		//Lightning Bolt: Deal 3 damage. Overload: (1)
		private static void LightningBolt(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell)
		{
			if (task.HasTarget)
				//reward if the spell does NOT overkill an enemy:
				if (task.Target is Minion)
					SpellDamageReward(playerState, opponentState, player, opponent, task, spell, 3, 1.25f);
		}

		//Hex: Transform a minion into a 0/1 Frog with Taunt.
		private static void Hex(State playerState, State opponentState, Controller player, Controller opponent,
			PlayerTask task, Spell spell)
		{
			var myMana = player.GetAvailableMana();
			playerState.BiasValue += StateUtility.LateReward(myMana, 3, 1.25f);
		}

		//Jade Lightning: Deal 4 damage. Summon a{1} {0} Jade Golem.@Deal 4 damage. Summon a Jade Golem.
		private static void JadeLightning(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell)
		{
			if (task.HasTarget)
				//reward if the spell does NOT overkill an enemy:
				if (task.Target is Minion)
					SpellDamageReward(playerState, opponentState, player, opponent, task, spell, 4, 1.25f);
		}

		//Maelstrom Portal: Deal 1 damage to all enemy minions. Summon a random 1-Cost minion.
		private static void MaelstromPortal(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell)
		{
			const int NUM_TARGET_MINIONS = 3;

			//negative if below NUM_TARGET_MINIONS, neutral at NUM_TARGET_MINIONS, then positive:
			var diff = opponentState.NumMinionsOnBoard - NUM_TARGET_MINIONS;
			playerState.BiasValue += diff * 1.25f;
		}


		private static void SpellDamageReward(State playerState, State opponentState, Controller player,
			Controller opponent, PlayerTask task, Spell spell, int damage, float reward)
		{
			var targetMinion = task.Target as Minion;

			if (spell.IsAffectedBySpellpower)
				damage += player.CurrentSpellPower;

			if (targetMinion.HasDivineShield && damage > 1)
			{
				//punishment for wasting damage for divine shield
				playerState.BiasValue -= 5.0f;
				return;
			}

			var targetHealth = targetMinion.Health;

			var diff = targetHealth - damage;

			var finalReward = diff * reward;

			//if the spell kills a minion on point, give additional bonus:
			if (diff == 0)
				finalReward += reward;

			playerState.BiasValue += finalReward;
		}
	}
}
