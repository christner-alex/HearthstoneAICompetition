using System;
using System.Text.RegularExpressions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneCoreAi.Tyche2
{
	/// <summary> Holds information about the state of the game for a single agent. </summary>
	internal class State
	{
		public float BiasValue;

		public int Fatigue;
		public int HeroArmor;
		public int HeroHealth;
		public float MinionValues;

		public int NumDeckCards;
		public int NumHandCards;
		public int NumMinionsOnBoard;

		public int TurnNumber;

		public int WeaponDamage;
		public int WeaponDurability;

		private State()
		{
		}

		public static State FromSimulatedGame(POGame.POGame newState, Controller me, PlayerTask task)
		{
			var s = new State
			{
				HeroHealth = me.Hero.Health,
				HeroArmor = me.Hero.Armor,

				TurnNumber = newState.Turn,

				NumDeckCards = me.DeckZone.Count,
				NumHandCards = me.HandZone.Count,
				NumMinionsOnBoard = me.BoardZone.Count,

				Fatigue = me.Hero.Fatigue,
				MinionValues = MinionUtils.ComputeMinionValues(me)
			};

			if (me.Hero.Weapon != null)
			{
				s.WeaponDurability = me.Hero.Weapon.Durability;
				s.WeaponDamage = me.Hero.Weapon.AttackDamage;
			}

			//this case is met, if the player uses a card that temporarily boosts attack:
			if (me.Hero.TotalAttackDamage > s.WeaponDamage)
			{
				s.WeaponDamage = me.Hero.TotalAttackDamage;

				//assume that the player can at least attack once:
				if (s.WeaponDurability == 0)
					s.WeaponDurability = 1;
			}

			//aka, can't attack:
			if (me.Hero.IsFrozen)
				s.WeaponDamage = 0;

			var minion = task.TryGetMinion();

			if (minion != null)
			{
				//give reward/punishment of minions cost less/more than usual:
				float diff = minion.Card.Cost - (float) minion.Cost;
				s.BiasValue += diff * 1.5f;
			}

			return s;
		}

		public static bool CorrectBuggySimulation(State lastPlayerState, State lastEnemyState,
			POGame.POGame lastState, PlayerTask task)
		{
			var taskType = task.PlayerTaskType;

			//Testing.TyDebug.LogError(task.FullPrint());

			var corrected = false;

			switch (taskType)
			{
				case PlayerTaskType.END_TURN:
					CorrectTurnEnd(lastPlayerState, lastEnemyState, lastState, task, ref corrected);
					break;
				case PlayerTaskType.HERO_ATTACK:
					CorrectHeroAttack(lastPlayerState, lastEnemyState, lastState, task, ref corrected);
					break;
				case PlayerTaskType.PLAY_CARD:
					CorrectPlayCard(lastPlayerState, lastEnemyState, lastState, task, ref corrected);
					break;
				case PlayerTaskType.MINION_ATTACK:
					CorrectMinionAttack(lastPlayerState, lastEnemyState, lastState, task, ref corrected);
					break;
				case PlayerTaskType.HERO_POWER:
					CorrectHeroPower(lastPlayerState, lastEnemyState, lastState, task, ref corrected);
					break;
			}

			if (TycheAgentConstants.LOG_UNKNOWN_CORRECTIONS && !corrected)
				DebugUtils.LogError("Unknown buggy PlayerTask: " + task.FullPrint());

			return corrected;
		}

		private static void CorrectHeroPower(State lastPlayerState, State lastEnemyState, POGame.POGame lastState,
			PlayerTask task, ref bool corrected)
		{
			//Dagger mastery from rogue: equip 1/2 daggers
			if (lastState.CurrentPlayer.HeroClass == CardClass.ROGUE)
			{
				EquipWeapon(lastPlayerState, 1, 2);
				corrected = true;
			}
		}

		private static void CorrectMinionAttack(State lastPlayerState, State lastEnemyState,
			POGame.POGame lastState, PlayerTask task, ref bool corrected)
		{
			if (!task.HasSource) return;
			if (!(task.Source is Minion minionSource)) return;
			if (!task.HasTarget) return;
			if (task.Target is Minion targetMinion)
			{
				MinionTookDamage(targetMinion, lastEnemyState, lastPlayerState, minionSource.AttackDamage,
					minionSource.Poisonous, task);
				MinionTookDamage(minionSource, lastPlayerState, lastEnemyState, targetMinion.AttackDamage,
					targetMinion.Poisonous, task);
				corrected = true;
			}
		}

		private static void CorrectTurnEnd(State lastPlayerState, State lastEnemyState, POGame.POGame lastState,
			PlayerTask task, ref bool corrected)
		{
			//mostly occurs if the weapon disappears from the hero at the end of the round, so lets just remove it
			EquipWeapon(lastPlayerState, 0, 0);
			corrected = true;
		}

		private static void CorrectHeroAttack(State lastPlayerState, State lastEnemyState, POGame.POGame lastState,
			PlayerTask playerTask, ref bool corrected)
		{
			var target = playerTask.Target;
			var attackingHero = lastState.CurrentPlayer.Hero;

			lastPlayerState.WeaponDurability--;

			//hero attacks a minion:
			if (target is Minion)
			{
				var targetMinion = target as Minion;

				MinionTookDamage(targetMinion, lastEnemyState, lastPlayerState, attackingHero.TotalAttackDamage, false,
					playerTask);

				//"revenge" damage from the minion to the hero:
				ComputeDamageToHero(lastPlayerState, attackingHero, targetMinion.AttackDamage);
				corrected = true;
			}

			//hero attacks a hero:
			else if (target is Hero)
			{
				var targetHero = target as Hero;

				//compute damage to the targetHero:
				ComputeDamageToHero(lastEnemyState, targetHero, attackingHero.TotalAttackDamage);
				corrected = true;
			}
		}

		private static void MinionTookDamage(Minion targetMinion, State ownerState, State opponentState,
			int totalAttackDamage, bool poison, PlayerTask task)
		{
			//didn't take damage:
			if (targetMinion.HasDivineShield)
			{
				ownerState.MinionValues -= MinionUtils.DIVINE_SHIELD_VALUE;
			}

			else
			{
				var damage = totalAttackDamage;

				if (poison || damage >= targetMinion.Health)
					RemoveMinion(targetMinion, ownerState, opponentState, task);
				else
					ownerState.MinionValues -= damage;
			}
		}

		private static void RemoveMinion(Minion minion, State ownerState, State opponentState, PlayerTask task)
		{
			//remove the minion value from the overall minion values and remove it from the board
			ownerState.MinionValues -= MinionUtils.ComputeMinionValue(minion);
			ownerState.NumMinionsOnBoard--;

			if (minion.HasDeathrattle)
				if (!CorrectForSummonAndEquip(minion.Card, ownerState, opponentState) &&
				    TycheAgentConstants.LOG_UNKNOWN_CORRECTIONS)
				{
					DebugUtils.LogError("Unknown deathrattle from " + minion.Card.FullPrint());
					DebugUtils.LogWarning("After task " + task.FullPrint());
				}
		}

		private static bool CorrectForSummonAndEquip(Card card, State ownerState, State opponentState)
		{
			var text = card.Text;

			if (text.Contains("Equip"))
			{
				var dmg = 0;
				var durability = 0;
				if (FindNumberValues(text, ref dmg, ref durability))
				{
					EquipWeapon(ownerState, dmg, durability);
				}
			}

			if (text.Contains("Summon"))
			{
				var dmg = 0;
				var health = 0;
				if (FindNumberValues(text, ref dmg, ref health))
				{
					ownerState.MinionValues += MinionUtils.ComputeMinionValue(health, dmg, 1);
					//just assume that the minion has some sort of (unknown) ability:
					//Testing.TyDebug.LogInfo("Summoned " + dmg + "/" + health);
					ownerState.MinionValues += 3;
				}
			}

			return true;
		}

		private static void ComputeDamageToHero(State targetHeroState, Hero targetHero, int damageToHero)
		{
			var armor = targetHero.Armor;
			var dmgAfterArmor = Math.Max(0, damageToHero - armor);

			targetHeroState.HeroArmor = Math.Max(0, armor - damageToHero);
			targetHeroState.HeroHealth = Math.Max(0, targetHero.Health - dmgAfterArmor);
		}

		private static void CorrectPlayCard(State lastPlayerState, State lastEnemyState, POGame.POGame lastState,
			PlayerTask task, ref bool corrected)
		{
			if (!task.HasSource) return;
			var source = task.Source;

			switch (source)
			{
				//player played a weapon to be equipped:
				case Minion sourceMinion:
				{
					if (sourceMinion.HasBattleCry)
					{
						var success = false;

						// https://hearthstone.gamepedia.com/Medivh,_the_Guardian
						if (sourceMinion.Card.AssetId == 39841)
						{
							EquipWeapon(lastPlayerState, 1, 3);
							success = true;
						}

						else if (CorrectForSummonAndEquip(sourceMinion.Card, lastPlayerState, lastEnemyState))
						{
							success = true;
						}

						if (sourceMinion.Card.Text.Contains("Destroy your opponent's weapon"))
						{
							// destroy opponent weapon and gain armor
							// https://www.hearthpwn.com/cards/55488-gluttonous-ooze
							if (sourceMinion.Card.AssetId == 41683)
								lastPlayerState.HeroArmor += lastEnemyState.WeaponDamage;

							EquipWeapon(lastEnemyState, 0, 0);
							success = true;
						}

						corrected = success;
					}

					break;
				}

				case Weapon sourceWeapon:
					EquipWeapon(lastPlayerState, sourceWeapon.AttackDamage, sourceWeapon.Durability);
					corrected = true;
					break;
				case Spell sourceSpell:
				{
					if (CorrectForSummonAndEquip(sourceSpell.Card, lastPlayerState, lastEnemyState)) corrected = true;
					break;
				}
			}

			if (task.HasTarget && task.Target is Minion targetMinion)
			{
				RemoveMinion(targetMinion, lastEnemyState, lastPlayerState, task);
			}
		}

		private static void EquipWeapon(State lastPlayerState, int attackDamage, int durability)
		{
			var newSum = attackDamage + durability;
			var oldSum = lastPlayerState.WeaponDamage + lastPlayerState.WeaponDurability;

			//equipping a worse weapon:
			if (newSum > 0 && newSum <= oldSum)
			{
				//assign negative values to indicate for a bad move (kinda hacky to do it that way):
				lastPlayerState.WeaponDamage = -Math.Abs(lastPlayerState.WeaponDamage);
				lastPlayerState.WeaponDurability = -Math.Abs(lastPlayerState.WeaponDurability);
			}

			//equip either 0/0 (destroy) or a better weapon:
			else
			{
				lastPlayerState.WeaponDamage = attackDamage;
				lastPlayerState.WeaponDurability = durability;
			}
		}

		/// <summary> Searches for XX/YY in the given text and parses it to int. </summary>
		private static bool FindNumberValues(string text, ref int first, ref int second)
		{
			var regex = new Regex("[0-9]+/[0-9]+");
			var match = regex.Match(text);

			if (match.Success)
			{
				var numbers = match.Value.Split("/", StringSplitOptions.RemoveEmptyEntries);
				if (numbers.Length == 2)
					if (Int32.TryParse(numbers[0], out first))
						if (Int32.TryParse(numbers[1], out second))
							return true;
			}

			if (TycheAgentConstants.LOG_UNKNOWN_CORRECTIONS) DebugUtils.LogError("Could find number values in " + text);

			return false;
		}

		public float GetAverageMinionValue()
		{
			if (NumMinionsOnBoard <= 0)
				return 0.0f;

			return MinionValues / NumMinionsOnBoard;
		}
	}
}
