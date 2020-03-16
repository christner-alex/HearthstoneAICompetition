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
		//private readonly NDArray representation;

		private readonly NDArray friendly_minion_rep;
		private readonly NDArray enemy_minion_rep;
		private readonly NDArray hand_rep;
		private readonly NDArray board_rep;

		public const int minion_vec_len = 12;
		public const int card_vec_len = 7;
		public const int board_vec_len = 12;

		public const int max_minions = 14;
		public const int max_side_minions = 7;
		public const int max_hand_cards = 10;
		public const int max_num_boards = 2;

		/// <summary>
		/// A wrapper for an NDArray which serves both as a key for the MaxTree state dictionaries
		/// and as input for the Scorer Neural Network.
		/// </summary>
		/// <param name="poGame">The PoGame to make a representation of.</param>
		/// <param name="use_current_player"> True if the player considered friendly is poGame.CurrentPlayer.
		/// False if it should be poGame.CurrentOpponent. This should be false if the state this is representing
		/// is the result of an END_TURN action by poGame.CurrentPlayer</param>
		public GameRep(POGame.POGame poGame, bool use_current_player)
		{
			Controller current_player = use_current_player ? poGame.CurrentPlayer : poGame.CurrentOpponent;

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
				List<NDArray> vec_list = x.Item2;

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
			board_vecs.Add(BoardToVec(poGame, use_current_player));

			//sort some lists according to the comparisons
			NDArrayDLAgentComparer comp = new NDArrayDLAgentComparer();
			player_minion_vecs.Sort(comp);
			opponent_minion_vecs.Sort(comp);
			hand_vecs.Sort(comp);

			//stack the results to get each representation

			board_rep = board_vecs[0];

			hand_rep = np.stack(hand_vecs.ToArray());

			friendly_minion_rep = np.stack(player_minion_vecs.ToArray());

			enemy_minion_rep = np.stack(opponent_minion_vecs.ToArray());

			CheckRep();
		}

		public GameRep(GameRep rep)
		{
			friendly_minion_rep = rep.FriendlyMinionRep;
			enemy_minion_rep = rep.EnemyMinionRep;
			board_rep = rep.BoardRep;
			hand_rep = rep.HandRep;

			CheckRep();
		}

		public GameRep Copy()
		{
			return new GameRep(this);
		}

		public NDArray FriendlyMinionRep => friendly_minion_rep.copy();
		public NDArray EnemyMinionRep => enemy_minion_rep.copy();
		public NDArray BoardRep => board_rep.copy();
		public NDArray HandRep => hand_rep.copy();
		public NDArray FlatRep => np.concatenate(new NDArray[] { board_rep.flat, hand_rep.flat, friendly_minion_rep.flat, enemy_minion_rep.flat });

		public bool Equals(GameRep other)
		{
			return hand_rep.Equals(other.hand_rep)
				&& board_rep.Equals(other.board_rep)
				&& friendly_minion_rep.Equals(other.friendly_minion_rep)
				&& enemy_minion_rep.Equals(other.enemy_minion_rep);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)FlatRep.ToArray<int>()).GetHashCode(EqualityComparer<int>.Default);
		}

		private static NDArray BoardSideToVec(POGame.POGame game, Controller player)
		{
			return np.array(
				player.Hero.Health + player.Hero.Armor, //player_health
				player.BaseMana, //player_base_mana
				player.RemainingMana, //player_remaining_mana
				player.HandZone.Count, //player_hand_size
				player.BoardZone.Count, //player_board_size
				player.DeckZone.Count, //player_deck_size
				player.SecretZone.Count, //player_secret_size
				player.BoardZone.Sum(p => p.AttackDamage), //player_total_atk
				player.BoardZone.Sum(p => p.Health), //player_total_health
				player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health), //player taunt_health
				player.Hero.Weapon != null ? player.Hero.Weapon.AttackDamage : 0, //player_weapon_atk
				player.Hero.Weapon != null ? player.Hero.Weapon.Durability : 0 //player_weapon_dur
			);
		}

		public static NDArray BoardToVec(POGame.POGame game, bool use_current_player)
		{
			if(game == null)
			{
				return np.zeros(new Shape(max_num_boards, board_vec_len), NPTypeCode.Int32);
			}

			Controller current_player = use_current_player ? game.CurrentPlayer : game.CurrentOpponent;
			Controller opponent = current_player.Opponent;

			NDArray friendly = BoardSideToVec(game, current_player);
			NDArray enemy = BoardSideToVec(game, opponent);

			NDArray result = np.stack(new NDArray[] { friendly, enemy });
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

			return result;
		}

		private bool CheckRep()
		{
			if(!Parameters.doCheckRep)
			{
				return true;
			}

			bool result = true;

			if(!hand_rep.Shape.Equals(new Shape(max_hand_cards,card_vec_len)))
			{
				Console.WriteLine("Hand is the wrong dimension");
				result = false;
			}

			if (!board_rep.Shape.Equals(new Shape(max_num_boards, board_vec_len)))
			{
				Console.WriteLine("Board is the wrong dimension");
				result = false;
			}

			if (!friendly_minion_rep.Shape.Equals(new Shape(max_side_minions,minion_vec_len)))
			{
				Console.WriteLine("Friendly Minions is the wrong dimension");
				result = false;
			}

			if (!enemy_minion_rep.Shape.Equals(new Shape(max_side_minions, minion_vec_len)))
			{
				Console.WriteLine("Enemy Minions is the wrong dimension");
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
