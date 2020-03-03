using SabberStoneCore.Model.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using NumSharp;
using NumSharp.Backends;
using NumSharp.Backends.Unmanaged;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;

namespace SabberStoneCoreAi.Agent.DLAgent
{

	class GameToRep
	{
		public static string Convert(POGame.POGame game)
		{
			Controller player = game.CurrentPlayer;
			Controller opponent = game.CurrentOpponent;

			Minion[] player_minions = player.BoardZone.GetAll();
			Minion[] opponent_minions = player.Opponent.BoardZone.GetAll();

			IPlayable[] player_hand = player.HandZone.GetAll();

			return game.FullPrint();
		}

		public static NDArray MinionToVec(Minion m)
		{
			int attack = m.AttackDamage;
			int health = m.Health;
			int can_attack = m.CanAttack ? 1 : 0;
			int spell_power = m.SpellPower;
			int divine_shild = m.HasDivineShield ? 1 : 0;
			int frozen = m.IsFrozen ? 1 : 0;
			int stealthed = m.HasStealth ? 1 : 0;
			int lifesteal = m.HasLifeSteal ? 1 : 0;
			int taunt = m.HasTaunt ? 1 : 0;
			int windfury = m.HasWindfury ? 1 : 0;
			int elusive = m.CantBeTargetedBySpells ? 1 : 0;
			string text = m.Card.Text;

			return np.array(attack, health, can_attack, spell_power, divine_shild, frozen, stealthed, lifesteal, taunt, windfury, elusive);
		}

		public static NDArray CardToVec(Card card)
		{
			//TODO: add text, tribe, and card type

			NDArray result = np.zeros(new Shape(7));
			switch (card.Type)
			{
				case CardType.MINION:
					result["4:"] = np.array(card.Tags[GameTag.COST], card.Tags[GameTag.ATK], card.Tags[GameTag.HEALTH]);
					result[0] = 1;
					break;
				case CardType.SPELL:
					result["4:"] = np.array(card.Tags[GameTag.COST], 0, 0);
					result[1] = 1;
					break;
				case CardType.WEAPON:
					result["4:"] = np.array(card.Tags[GameTag.COST], card.Tags[GameTag.ATK], card.Tags[GameTag.DURABILITY]);
					result[2] = 1;
					break;
				case CardType.HERO:
					result["4:"] = np.array(card.Tags[GameTag.COST], 0, card.Tags[GameTag.ARMOR]);
					result[3] = 1;
					break;
			}

			return result;
		}
	}

	class NDArrayDLAgentComparer : IComparer<NDArray>
	{
		public int Compare(NDArray x, NDArray y)
		{
			int c = x.shape[0].CompareTo(y.shape[0]);
			if (c != 0)
			{
				return x;
			}

			for (int i = 0; i < x.shape[0]; i++)
			{
				NDArray a = x[i.ToString() + ",:"];
				NDArray b = y[i.ToString() + ",:"];

				c = a.ToArray<int>()[0].CompareTo(b.ToArray<int>()[0]);
				if (c != 0)
				{
					return c;
				}
			}

			return 0;
		}
	}
}
