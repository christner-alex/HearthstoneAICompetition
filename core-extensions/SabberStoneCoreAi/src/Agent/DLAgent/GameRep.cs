using NumSharp;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameRep : IEquatable<GameRep>
	{
		private readonly NDArray representation;

		public const int minion_vec_len = 12;
		public const int card_vec_len = 7;
		public const int board_vec_len = 24;

		public const int max_minions = 14;
		public const int max_side_minions = 7;
		public const int max_hand_cards = 10;

		/// <summary>
		/// A wrapper for an NDArray which serves both as a key for the MaxTree state dictionaries
		/// and as input for the Scorer Neural Network.
		/// </summary>
		/// <param name="poGame">The PoGame to make a representation of.</param>
		/// <param name="use_current_player"> True if the player considered friendly is poGame.CurrentPlayer.
		/// False if it should be poGame.CurrentOpponent. This should be false if the state this is representing
		/// is the result of an END_TURN action by poGame.CurrentPlayer</param>
		public GameRep(POGame.POGame poGame, bool use_current_player = true /*, List<MoveRecord> record*/)
		{
			representation = Convert(poGame, use_current_player);

			CheckRep();
		}

		public GameRep(GameRep rep)
		{
			representation = rep.representation.copy();

			CheckRep();
		}

		public NDArray GetVectorRep => representation.copy();

		public bool Equals(GameRep other)
		{
			return representation.Equals(other.representation);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)representation.ToArray<int>()).GetHashCode(EqualityComparer<int>.Default);
		}

		public static NDArray Convert(POGame.POGame game, bool use_current_player = true /*, List<MoveRecord> record*/)
		{
			Controller current_player = use_current_player ? game.CurrentPlayer : game.CurrentOpponent;

			//get board informations
			Minion[] player_minions = current_player.BoardZone.GetAll();
			Minion[] opponent_minions = current_player.Opponent.BoardZone.GetAll();
			IPlayable[] player_hand = current_player.HandZone.GetAll();

			//create storage for vector representations
			List<NDArray> player_minion_vecs = new List<NDArray>();
			List<NDArray> opponent_minion_vecs = new List<NDArray>();
			List<NDArray> hand_vecs = new List<NDArray>();
			List<NDArray> board_vecs = new List<NDArray>();


			//get the vector representations for the minions on the board
			foreach ((Minion[], List<NDArray>) x in new (Minion[], List<NDArray>)[] { (player_minions, player_minion_vecs), (opponent_minions, opponent_minion_vecs) })
			{
				Minion[] minions = x.Item1;
				List < NDArray > vec_list = x.Item2;

				for (int i = 0; i < max_side_minions; i++)
				{
					Minion m = i < minions.Length ? minions[i] : null;
					vec_list.Add(MinionToVec(m));
				}
			}

			//get the vector representations for the cards in your hand
			for (int i = 0; i < max_hand_cards; i++)
			{
				Card c = i < player_hand.Length ? player_hand[i].Card : null;
				hand_vecs.Add(CardToVec(c));
			}

			//get the vector representation for the current and last few boards
			board_vecs.Add(BoardToVec(game, use_current_player));

			//sort some lists according to the comparisons
			NDArrayDLAgentComparer comp = new NDArrayDLAgentComparer();
			player_minion_vecs.Sort(comp);
			opponent_minion_vecs.Sort(comp);
			hand_vecs.Sort(comp);

			//concatenate all the vectors into one
			NDArray result = np.concatenate(
				player_minion_vecs
				.Concat<NDArray>(opponent_minion_vecs)
				.Concat<NDArray>(hand_vecs)
				.Concat<NDArray>(board_vecs)
				.ToArray()
				);

			return result;
		}

		public NDArray GetSlice(int start, int offset)
		{
			return representation[new Slice(start, start+offset)].copy();
		}
		public NDArray GetMinionVecs()
		{
			NDArray slice = GetSlice(0,
				max_minions * minion_vec_len);

			return slice.reshape(new int[] { max_minions, minion_vec_len });
		}
		public NDArray GetFriendlyMinionVecs()
		{
			NDArray slice = GetSlice(0,
				max_side_minions * minion_vec_len);

			return slice.reshape(new int[] { max_side_minions, minion_vec_len });
		}
		public NDArray GetEnemyMinionVecs()
		{
			NDArray slice = GetSlice(max_side_minions * minion_vec_len,
				max_side_minions * minion_vec_len);

			return slice.reshape(new int[] { max_side_minions, minion_vec_len });
		}
		public NDArray GetHandVecs()
		{
			NDArray slice = GetSlice(max_minions * minion_vec_len,
				max_hand_cards * card_vec_len);

			return slice.reshape(new int[] { max_hand_cards, card_vec_len });
		}
		public NDArray GetBoardVec()
		{
			NDArray slice = GetSlice(
				max_minions * minion_vec_len + max_hand_cards * card_vec_len,
				board_vec_len);

			return slice.reshape(new int[] { board_vec_len });
		}

		public static NDArray BoardToVec(POGame.POGame game, bool use_current_player = true)
		{
			if(game == null)
			{
				return np.zeros(new Shape(board_vec_len), NPTypeCode.Int32);
			}

			Controller current_player = use_current_player ? game.CurrentPlayer : game.CurrentOpponent;
			Controller opponent = current_player.Opponent;

			NDArray result = np.array(
				current_player.Hero.Health + current_player.Hero.Armor, //player_health
				current_player.BaseMana, //player_base_mana
				current_player.RemainingMana, //player_remaining_mana
				current_player.HandZone.Count, //player_hand_size
				current_player.BoardZone.Count, //player_board_size
				current_player.DeckZone.Count, //player_deck_size
				current_player.SecretZone.Count, //player_secret_size
				current_player.BoardZone.Sum(p => p.AttackDamage), //player_total_atk
				current_player.BoardZone.Sum(p => p.Health), //player_total_health
				current_player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health), //player taunt_health
				current_player.Hero.Weapon != null ? current_player.Hero.Weapon.AttackDamage : 0, //player_weapon_atk
				current_player.Hero.Weapon != null ? current_player.Hero.Weapon.Durability : 0, //player_weapon_dur

				opponent.Hero.Health + opponent.Hero.Armor, //opponent_health
				opponent.BaseMana, //opponent_base_mana
				opponent.RemainingMana, //opponent_remaining_mana
				opponent.HandZone.Count, //opponent_hand_size
				opponent.BoardZone.Count, //opponent_board_size
				opponent.DeckZone.Count, //opponent_deck_size
				opponent.SecretZone.Count, //opponent_secret_size
				opponent.BoardZone.Sum(p => p.AttackDamage), //opponent_total_atk
				opponent.BoardZone.Sum(p => p.Health), //opponent_total_health
				opponent.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health), //opponent taunt_health
				opponent.Hero.Weapon != null ? opponent.Hero.Weapon.AttackDamage : 0, //opponent_weapon_atk
				opponent.Hero.Weapon != null ? opponent.Hero.Weapon.Durability : 0 //opponent_weapon_dur


			);

			Debug.Assert(result.Shape[0] == board_vec_len);

			return result;
		}

		public static NDArray MinionToVec(Minion minion)
		{
			if (minion == null)
			{
				return np.zeros(new Shape(minion_vec_len), NPTypeCode.Int32);
			}

			int attack = minion.AttackDamage;
			int health = minion.Health;
			int can_attack = minion.CanAttack ? 1 : 0;
			int divine_shild = minion.HasDivineShield ? 1 : 0;
			int elusive = minion.CantBeTargetedBySpells ? 1 : 0;
			int frozen = minion.IsFrozen ? 1 : 0;
			int lifesteal = minion.HasLifeSteal ? 1 : 0;
			int silenced = minion.IsSilenced ? 1 : 0;
			int spell_power = minion.SpellPower;
			int stealthed = minion.HasStealth ? 1 : 0;
			int taunt = minion.HasTaunt ? 1 : 0;
			int windfury = minion.HasWindfury ? 1 : 0;
			//string text = minion.Card.Text; //TODO, add card text or something similar

			NDArray result = np.array(attack, health, can_attack, divine_shild, elusive, frozen, lifesteal, silenced, spell_power, stealthed, taunt, windfury);

			Debug.Assert(result.Shape[0] == minion_vec_len);

			return result;
		}

		public static NDArray CardToVec(Card card)
		{
			//TODO: add text, tribe

			NDArray result = np.zeros(new Shape(card_vec_len), NPTypeCode.Int32);
			if (card==null)
			{
				return result;
			}

			int a = 0;
			int b = 0;
			int c = 0;
			switch (card.Type)
			{
				case CardType.MINION:
					card.Tags.TryGetValue(GameTag.COST, out a);
					card.Tags.TryGetValue(GameTag.ATK, out b);
					card.Tags.TryGetValue(GameTag.HEALTH, out c);
					result[0] = 1;
					break;
				case CardType.SPELL:
					card.Tags.TryGetValue(GameTag.COST, out a);
					b = 0;
					c = 0;
					result[1] = 1;
					break;
				case CardType.WEAPON:
					card.Tags.TryGetValue(GameTag.COST, out a);
					card.Tags.TryGetValue(GameTag.ATK, out b);
					card.Tags.TryGetValue(GameTag.DURABILITY, out c);
					result[2] = 1;
					break;
				case CardType.HERO:
					card.Tags.TryGetValue(GameTag.COST, out a);
					b = 0;
					card.Tags.TryGetValue(GameTag.ARMOR, out c);
					result[3] = 1;
					break;
			}

			result["4:"] = np.array(a, b, c);

			Debug.Assert(result.Shape[0] == card_vec_len);

			return result;
		}

		private bool CheckRep()
		{
			if(!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;
			int s = representation.size;
			int q = minion_vec_len * max_minions + card_vec_len * max_hand_cards + board_vec_len;
			if (s != q)
			{
				Console.WriteLine("Representation is not the correct length: " + s.ToString() + "!=" + q.ToString());
				result = false;
			}
			return result;
		}
	}

	class NDArrayDLAgentComparer : IComparer<NDArray>
	{
		public int Compare(NDArray x, NDArray y)
		{
			int c = y.shape[0] - x.shape[0];
			if (c != 0)
			{
				return c;
			}

			int[] diff = (y - x).ToArray<int>();

			foreach (int n in diff)
			{
				if (n > 0)
				{
					return 1;
				}
				else if (n < 0)
				{
					return -1;
				}
			}

			return 0;
		}
	}
}
