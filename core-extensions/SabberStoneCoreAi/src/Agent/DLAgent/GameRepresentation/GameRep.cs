using Newtonsoft.Json;
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
using System.Text.RegularExpressions;

namespace SabberStoneCoreAi.Agent.DLAgent
{
	class GameRep : IEquatable<GameRep>
	{
		private NDArray friendly_minion_rep;
		private NDArray enemy_minion_rep;
		private NDArray hand_rep;
		private NDArray board_rep;
		private NDArray history_rep;

		public NDArray FriendlyMinionRep { get { return friendly_minion_rep.copy(); } private set { friendly_minion_rep = value; } }
		public NDArray EnemyMinionRep { get { return enemy_minion_rep.copy(); } private set { enemy_minion_rep = value; } }
		public NDArray HandRep { get { return hand_rep.copy(); } private set { hand_rep = value; } }
		public NDArray BoardRep { get { return board_rep.copy(); } private set { board_rep = value; } }
		public NDArray HistoryRep { get { return history_rep.copy(); } private set { history_rep = value; } }

		[JsonIgnore]
		public NDArray FullHistoryRep => np.concatenate(new NDArray[] { HistoryRep, np.expand_dims(BoardRep, 0) }, axis: 0);
		[JsonIgnore]
		public NDArray FlatRep => np.concatenate(new NDArray[] { HandRep.flat, FriendlyMinionRep.flat, EnemyMinionRep.flat, BoardRep.flat, HistoryRep.flat });


		public const int minion_vec_len = 14;
		public const int card_vec_len = 14;
		public const int board_vec_len = 15;

		public const int max_minions = 14;
		public const int max_side_minions = 7;
		public const int max_hand_cards = 10;
		public const int max_num_boards = 2;
		public const int max_num_history = 3;

		/// <summary>
		/// A wrapper for an NDArray which serves both as a key for the MaxTree state dictionaries
		/// and as input for the Scorer Neural Network.
		/// </summary>
		/// <param name="poGame">The PoGame to make a representation of.</param>
		/// <param name="use_current_player"> True if the player considered friendly is poGame.CurrentPlayer.
		/// False if it should be poGame.CurrentOpponent. This should be false if the state this is representing
		/// is the result of an END_TURN action by poGame.CurrentPlayer</param>
		public GameRep(POGame.POGame poGame, GameRecord record)
		{
			Controller current_player = poGame.CurrentPlayer;

			//get board informations
			Minion[] player_minions = current_player.BoardZone.GetAll();
			Minion[] opponent_minions = current_player.Opponent.BoardZone.GetAll();
			IPlayable[] player_hand = current_player.HandZone.GetAll();

			//create storage for vector representations
			List<NDArray> player_minion_vecs = new List<NDArray>();
			List<NDArray> opponent_minion_vecs = new List<NDArray>();
			List<NDArray> hand_vecs = new List<NDArray>();

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

			//sort some lists according to the comparisons
			NDArrayDLAgentComparer comp = new NDArrayDLAgentComparer();
			player_minion_vecs.Sort(comp);
			opponent_minion_vecs.Sort(comp);
			hand_vecs.Sort(comp);

			//stack the results to get each representation

			BoardRep = BoardToVec(poGame);

			HandRep = np.stack(hand_vecs.ToArray());

			FriendlyMinionRep = np.stack(player_minion_vecs.ToArray());

			EnemyMinionRep = np.stack(opponent_minion_vecs.ToArray());

			HistoryRep = HistoryToVec(record);

			CheckRep();
		}

		public GameRep(GameRep rep)
		{
			FriendlyMinionRep = rep.FriendlyMinionRep;
			EnemyMinionRep = rep.EnemyMinionRep;
			BoardRep = rep.BoardRep;
			HandRep = rep.HandRep;
			HistoryRep = rep.HistoryRep;

			CheckRep();
		}

		public GameRep Copy()
		{
			return new GameRep(this);
		}

		[JsonConstructor]
		public GameRep(int[] friendlyMinionRep, int[] enemyMinionRep, int[] handRep, int[] boardRep, int[] historyRep)
		{
			FriendlyMinionRep = np.array(friendlyMinionRep).reshape(max_side_minions, minion_vec_len);
			EnemyMinionRep = np.array(enemyMinionRep).reshape(max_side_minions, minion_vec_len);
			HandRep = np.array(handRep).reshape(max_hand_cards, card_vec_len);
			BoardRep = np.array(boardRep).reshape(max_num_boards, board_vec_len);
			HistoryRep = np.array(historyRep).reshape(max_num_history, max_num_boards, board_vec_len);
		}

		//public NDArray FriendlyMinionRep => friendly_minion_rep.copy();
		//public NDArray EnemyMinionRep => enemy_minion_rep.copy();
		//public NDArray BoardRep => board_rep.copy();
		//public NDArray HandRep => hand_rep.copy();
		//public NDArray HistoryRep => history_rep.copy();
		//public NDArray FullHistoryRep => np.concatenate(new NDArray[] { history_rep, np.expand_dims(board_rep, 0) }, axis:0);
		//public NDArray FlatRep => np.concatenate(new NDArray[] { hand_rep.flat, friendly_minion_rep.flat, enemy_minion_rep.flat, board_rep.flat, history_rep.flat });

		public bool Equals(GameRep other)
		{
			return hand_rep.Equals(other.hand_rep)
				&& board_rep.Equals(other.board_rep)
				&& friendly_minion_rep.Equals(other.friendly_minion_rep)
				&& enemy_minion_rep.Equals(other.enemy_minion_rep)
				&& history_rep.Equals(other.history_rep);
		}

		public override int GetHashCode()
		{
			return ((IStructuralEquatable)FlatRep.ToArray<int>()).GetHashCode(EqualityComparer<int>.Default);
		}

		private static NDArray BoardSideToVec(Controller player)
		{
			return np.array(
				player.Hero.Health + player.Hero.Armor, //player_health
				player.BaseMana, //player_base_mana
				player.RemainingMana, //player_remaining_mana
				player.HandZone.Count, //player_hand_size
				player.BoardZone.Count, //player_board_size
				player.DeckZone.Count, //player_deck_size
				player.SecretZone.Count, //player_secret_size
				player.GraveyardZone.Count, //graveyard size
				player.BoardZone.Sum(p => p.AttackDamage), //player_total_atk
				player.BoardZone.Sum(p => p.Health), //player_total_health
				player.BoardZone.Where(p => p.HasTaunt).Sum(p => p.Health), //player taunt_health
				player.Hero.TotalAttackDamage, //hero attack damage
				player.Hero.Weapon != null ? player.Hero.Weapon.Durability : 0, //hero weapon durability
				player.HeroPowerActivationsThisTurn,  //hero power activations
				player.HandZone.Sum(p => p.Cost) //hand cost
			);
		}

		public static NDArray BoardToVec(POGame.POGame game)
		{
			if(game == null)
			{
				return np.zeros(new Shape(max_num_boards, board_vec_len), NPTypeCode.Int32);
			}

			Controller current_player = game.CurrentPlayer;
			Controller opponent = current_player.Opponent;

			NDArray friendly = BoardSideToVec(current_player);
			NDArray enemy = BoardSideToVec(opponent);

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
			int deathrattle = minion.HasDeathrattle ? 1 : 0;
			int divine_shild = minion.HasDivineShield ? 1 : 0;
			int elusive = minion.CantBeTargetedBySpells ? 1 : 0;
			int frozen = minion.IsFrozen ? 1 : 0;
			int inspire = minion.HasInspire ? 1 : 0;
			int lifesteal = minion.HasLifeSteal ? 1 : 0;
			int poisonous = minion.Poisonous ? 1 : 0;
			//int silenced = minion.IsSilenced ? 1 : 0;
			int spell_power = minion.SpellPower;
			int stealthed = minion.HasStealth ? 1 : 0;
			int taunt = minion.HasTaunt ? 1 : 0;
			int windfury = minion.HasWindfury ? 1 : 0;
			
			NDArray result = np.array(attack, health, can_attack, deathrattle, divine_shild, elusive, frozen, inspire, lifesteal, poisonous, spell_power, stealthed, taunt, windfury);

			return result;
		}

		public static NDArray CardToVec(Card card)
		{
			NDArray result = np.zeros(new Shape(card_vec_len), NPTypeCode.Int32);
			if (card==null)
			{
				return result;
			}

			int cost = 0;
			int atk = 0;
			int def = 0;

			bool specialSpell = false;

			switch (card.Type)
			{
				case CardType.MINION:
					card.Tags.TryGetValue(GameTag.ATK, out atk);
					card.Tags.TryGetValue(GameTag.HEALTH, out def);
					result[0] = 1;
					break;
				case CardType.SPELL:
					result[1] = 1;
					specialSpell = card.IsSecret || card.IsQuest;
					break;
				case CardType.WEAPON:
					card.Tags.TryGetValue(GameTag.ATK, out atk);
					card.Tags.TryGetValue(GameTag.DURABILITY, out def);
					result[2] = 1;
					break;
				case CardType.HERO:
					card.Tags.TryGetValue(GameTag.ARMOR, out def);
					result[3] = 1;
					break;
			}
			card.Tags.TryGetValue(GameTag.COST, out cost);

			int draw = 0;
			int damage = 0;
			int restore = 0;

			bool start_turn = false;
			bool end_turn = false;
			bool whenever = false;
			bool when = false;
			bool after = false;
			bool battlecry = false;
			bool inspire = false;

			int aoe = 0;

			if (card.Text != null)
			{
				string text = card.Text.ToLower();

				battlecry = text.Contains("<b>battlecry:</b>");
				inspire = text.Contains("<b>inspire:</b>");
				start_turn = text.Contains("at the start");
				end_turn = text.Contains("at the end");
				whenever = text.Contains("whenever");
				when = text.Contains("when ");
				after = text.Contains("after");
				aoe = text.Contains("to all") ? 1 : 0;


				Match draw_match = Regex.Match(text, "draw\\s.*\\scard");
				Match damage_match = Regex.Match(text, "deal\\s.*\\sdamage");
				Match heal_match = Regex.Match(text, "restore\\s.*\\shealth");

				if (draw_match.Success)
				{
					string amount = draw_match.Value.Split(" ")[1];
					if (amount.Equals("a")) draw = 1;
					else if (amount.Any(char.IsDigit)) draw = Int32.Parse(Regex.Match(amount, "[0-9]+").Value);
				}
				if (damage_match.Success)
				{
					string amount = damage_match.Value.Split(" ")[1];
					if (amount.Equals("a")) damage = 1;
					else if (amount.Any(char.IsDigit)) damage = Int32.Parse(Regex.Match(amount, "[0-9]+").Value);
				}
				if(heal_match.Success)
				{
					string amount = heal_match.Value.Split(" ")[1];
					if (amount.Equals("a")) restore = 1;
					else if (amount.Any(char.IsDigit)) restore = Int32.Parse(Regex.Match(amount, "[0-9]+").Value);
				}
			}

			int instant_effects = new bool[] { battlecry, card.Charge, card.Rush, card.ChooseOne, card.Combo }.Count(v => v);
			int continuous_triggers = new bool[] { start_turn, end_turn, whenever, after, inspire }.Count(v => v);
			int enchantments = new bool[] {card.DivineShield, card.CantBeTargetedBySpells, card.Echo, card.Taunt,
				card.LifeSteal, card.Poisonous, card.Stealth, card.Windfury, card.Deathrattle, card.SpellPower > 0 }.Count(v => v);

			result["4:"] = np.array(cost, atk, def, draw, damage, restore, aoe, instant_effects, continuous_triggers, enchantments);

			return result;
		}

		public static NDArray HistoryToVec(GameRecord rec)
		{
			List<NDArray> last_states = rec.LastBoards(max_num_history);

			while (last_states.Count < GameRep.max_num_history)
			{
				last_states.Insert(0, np.zeros(new Shape(max_num_boards, board_vec_len), NPTypeCode.Int32));
			}

			NDArray result = np.stack(last_states.ToArray());

			return result;
		}

		public NDArray ConstructMinionPairs()
		{
			NDArray[] boards = new NDArray[GameRep.max_side_minions];

			for (int r = 0; r < GameRep.max_side_minions; r++)
			{
				NDArray enemy_board = EnemyMinionRep.roll(r, 0);
				boards[r] = np.concatenate(new NDArray[] { friendly_minion_rep, enemy_board }, 1);
			}

			NDArray result = np.concatenate(boards, 0);

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

			if (!history_rep.Shape.Equals(new Shape(max_num_history, max_num_boards, board_vec_len)))
			{
				Console.WriteLine("Board History is wrong shape");
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
