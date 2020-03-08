using NumSharp;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameRep : IEquatable<GameRep>
	{
		private readonly NDArray representation;

		public const int minion_vec_len = 11;
		public const int card_vec_len = 7;
		public const int board_vec_len = 18;

		public const int num_minions = 14;
		public const int num_side_minions = 7;
		public const int num_hand_cards = 10;
		public const int num_boards = 6; 

		public GameRep(POGame.POGame poGame, List<MoveRecord> record)
		{
			representation = Convert(poGame, record);
		}

		public NDArray GetVectorRep => representation.copy();

		public bool Equals(GameRep other)
		{
			return representation.Equals(other.representation);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)representation.ToArray<float>()).GetHashCode(EqualityComparer<float>.Default);
		}

		public static NDArray Convert(POGame.POGame game, List<MoveRecord> record)
		{
			Controller player = game.CurrentPlayer;

			//get board informations
			Minion[] player_minions = player.BoardZone.GetAll();
			Minion[] opponent_minions = player.Opponent.BoardZone.GetAll();
			IPlayable[] player_hand = player.HandZone.GetAll();

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

				for (int i = 0; i < num_side_minions; i++)
				{
					Minion m = i < minions.Length ? minions[i] : null;
					vec_list.Add(MinionToVec(m));
				}
			}

			//get the vector representations for the cards in your hand
			for (int i = 0; i < num_hand_cards; i++)
			{
				Card c = i < player_hand.Length ? player_hand[i].Card : null;
				hand_vecs.Add(CardToVec(c));
			}

			//get the vector representation for the current and last few boards
			board_vecs.Add(BoardToVec(game));
			int added = 1;
			int ind = record.Count - 1;
			while(added < num_boards)
			{
				MoveRecord rec = record[ind];

				foreach(POGame.POGame g in new POGame.POGame[] { rec.Successor, rec.State })
				{
					if(g!=null && added < num_boards)
					{
						board_vecs.Add(BoardToVec(g));
						added++;
					}
				}

				ind--;
			}

			//sort some lists according to the comparisons
			NDArrayDLAgentComparer comp = new NDArrayDLAgentComparer();
			player_minion_vecs.Sort(comp);
			opponent_minion_vecs.Sort(comp);
			hand_vecs.Sort(comp);

			//TODO: concetenate the vectors

			return np.zeros(1);
		}

		public NDArray GetSlice(int start, int offset)
		{
			return representation[new Slice(start, start+offset)].copy();
		}
		public NDArray GetMinionVecs()
		{
			NDArray slice = GetSlice(0,
				num_minions * minion_vec_len);

			return slice.reshape(new int[] { num_minions, minion_vec_len });
		}
		public NDArray GetFriendlyMinionVecs()
		{
			NDArray slice = GetSlice(0,
				num_side_minions * minion_vec_len);

			return slice.reshape(new int[] { num_side_minions, minion_vec_len });
		}
		public NDArray GetEnemyMinionVecs()
		{
			NDArray slice = GetSlice(num_side_minions * minion_vec_len,
				num_side_minions * minion_vec_len);

			return slice.reshape(new int[] { num_side_minions, minion_vec_len });
		}
		public NDArray GetHandVecs()
		{
			NDArray slice = GetSlice(num_minions * minion_vec_len,
				num_hand_cards * card_vec_len);

			return slice.reshape(new int[] { num_hand_cards, card_vec_len });
		}
		public NDArray GetBoardVecs()
		{
			NDArray slice = GetSlice(
				num_minions * minion_vec_len + num_hand_cards * card_vec_len,
				num_boards * board_vec_len);

			return slice.reshape(new int[] { num_boards, board_vec_len });
		}

		public static NDArray BoardToVec(POGame.POGame game)
		{
			if(game == null)
			{
				return np.zeros(board_vec_len);
			}

			int player_health = game.CurrentPlayer.Hero.Health + game.CurrentPlayer.Hero.Armor;
			int player_base_mana = game.CurrentPlayer.BaseMana;
			int player_used_mana = game.CurrentPlayer.UsedMana;
			int player_hand_size = game.CurrentPlayer.HandZone.Count;
			int player_deck_size = game.CurrentPlayer.DeckZone.Count;
			int player_board_size = game.CurrentPlayer.BoardZone.Count;
			int player_secret_size = game.CurrentPlayer.SecretZone.Count;
			int player_num_played = game.CurrentPlayer.NumCardsPlayedThisTurn;
			int player_has_weapon = game.CurrentPlayer.Hero.Weapon != null ? 1 : 0;

			int opponent_health = game.CurrentOpponent.Hero.Health + game.CurrentPlayer.Hero.Armor;
			int opponent_base_mana = game.CurrentOpponent.BaseMana;
			int opponent_used_mana = game.CurrentOpponent.UsedMana;
			int opponent_hand_size = game.CurrentOpponent.HandZone.Count;
			int opponent_deck_size = game.CurrentOpponent.DeckZone.Count;
			int opponent_board_size = game.CurrentOpponent.BoardZone.Count;
			int opponent_secret_size = game.CurrentOpponent.SecretZone.Count;
			int opponent_num_played = game.CurrentOpponent.NumCardsPlayedThisTurn;
			int opponent_has_weapon = game.CurrentOpponent.Hero.Weapon != null ? 1 : 0;

			return np.array(
				player_health, player_base_mana, player_used_mana, player_hand_size, player_deck_size, player_board_size, player_secret_size, player_num_played, player_has_weapon,
				opponent_health, opponent_base_mana, opponent_used_mana, opponent_hand_size, opponent_deck_size, opponent_board_size, opponent_secret_size, opponent_num_played, opponent_has_weapon
				);
		}

		public static NDArray MinionToVec(Minion minion)
		{
			if (minion == null)
			{
				return np.zeros(minion_vec_len);
			}

			int attack = minion.AttackDamage;
			int health = minion.Health;
			int can_attack = minion.CanAttack ? 1 : 0;
			int spell_power = minion.SpellPower;
			int divine_shild = minion.HasDivineShield ? 1 : 0;
			int frozen = minion.IsFrozen ? 1 : 0;
			int stealthed = minion.HasStealth ? 1 : 0;
			int lifesteal = minion.HasLifeSteal ? 1 : 0;
			int taunt = minion.HasTaunt ? 1 : 0;
			int windfury = minion.HasWindfury ? 1 : 0;
			int elusive = minion.CantBeTargetedBySpells ? 1 : 0;
			string text = minion.Card.Text;

			return np.array(attack, health, can_attack, spell_power, divine_shild, frozen, stealthed, lifesteal, taunt, windfury, elusive);
		}

		public static NDArray CardToVec(Card card)
		{
			//TODO: add text, tribe

			NDArray result = np.zeros(card_vec_len);
			if(card==null)
			{
				return result;
			}

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
			int c = y.shape[0] - x.shape[0];
			if (c != 0)
			{
				return c;
			}

			float[] diff = (y-x).ToArray<float>();

			foreach (float n in diff)
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
