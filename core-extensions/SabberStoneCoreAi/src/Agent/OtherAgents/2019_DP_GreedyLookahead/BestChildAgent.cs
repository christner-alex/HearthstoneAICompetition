using System.Runtime.CompilerServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCoreAi.POGame;
using SabberStoneCoreAi.Agent.ExampleAgents;
using SabberStoneCoreAi.Agent;
using SabberStoneCoreAi.Meta;

using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCoreAi.Score;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;
using SabberStoneCoreAi.src.Agent;

namespace SabberStoneCoreAi.Competition.Agents
{
    class BestChildAgent : AbstractAgent
    {
        private List<PlayerTask> bestMoves;

        private static int Max_Depth = 10;

        public override PlayerTask GetMove(POGame.POGame poGame) {
            //Starting a new list of moves to take
            bestMoves = new List<PlayerTask>();

            //Setting the root node of the tree
            BestChildNode root = new BestChildNode(poGame);
            root.SetActions(poGame.CurrentPlayer.Options());
            List<BestChildNode> parentNodes = new List<BestChildNode>(){root};

            //Helper for the last moves
            List<BestChildNode> leaves = new List<BestChildNode>();

            // while we have moves to play
            while (true) {
				
                //Spliting the states into leaves and nodes
                List<BestChildNode> nodes = new List<BestChildNode>();
                foreach (BestChildNode test in parentNodes){
                    if(test.IsFinisher()||test.GetMoveSequence().Count>=6){
                        leaves.Add(test);
                    }
                    else{
                        nodes.Add(test);
                    }
                }

                //If there is nothing else to simulate
                if (nodes.Count == 0) {
                    break;
                }

                List<BestChildNode> childNodes = new List<BestChildNode>();
                
                //For each node that we have, get the moves that are not end turn and simulate them to store new Child nodes
                foreach (BestChildNode Node in nodes) {
                    //Getting moves that are not end turn
                    List<PlayerTask> moves = Node.GetMoves();
                    List<PlayerTask> movesToSimulate = new List<PlayerTask>();
                    foreach(PlayerTask isNotEndTurn in moves){
                        if(isNotEndTurn.PlayerTaskType != PlayerTaskType.END_TURN)
                          movesToSimulate.Add(isNotEndTurn);
                    }

                    //Simulating the actions and getting the states
                    Dictionary<PlayerTask, SabberStoneCoreAi.POGame.POGame> sim = Node.GetState().Simulate(movesToSimulate);
			        Dictionary<PlayerTask, SabberStoneCoreAi.POGame.POGame>.KeyCollection keyColl = sim.Keys;
                    
                    //Create more nodes for the new simulated actions
                   foreach (PlayerTask key in keyColl) {
						if (sim[key] != null)
							childNodes.Add(new BestChildNode(Node,key, sim[key]));
                    }
                }

                // Get only the best children 
               List<Tuple<BestChildNode,double>> scoredChild = new List<Tuple<BestChildNode, double>>();
                foreach(BestChildNode toScore in childNodes){
                    double score = Score(toScore.GetState());
                     Tuple<BestChildNode,double> scoring = new Tuple<BestChildNode, double>(toScore,score);
                    scoredChild.Add(scoring);
                }

               List<Tuple<BestChildNode,double>> sortedChildren = new List<Tuple<BestChildNode, double>>();
               foreach(var material in scoredChild.OrderByDescending(t=>t.Item2)){
                sortedChildren.Add(material);   
               }      

                parentNodes = sortedChildren.Take(Max_Depth).Select(stateScore => stateScore.Item1).ToList();
            }

            // No moves possible
            var BestChilds = leaves.ToList();
            if (BestChilds.Count == 0) {
                if(parentNodes.Count!=0){
                bestMoves.Add(EndTurnTask.Any(parentNodes[0].GetState().CurrentPlayer.Controller));
                PlayerTask endMove = bestMoves[0]; 
                return endMove;}
                else{
                    return EndTurnTask.Any(root.GetState().CurrentPlayer.Controller);
                }
            }

            // Get the very best state
            BestChildNode bestState = new BestChildNode(poGame);
            if (BestChilds.Count == 1){
                 bestMoves = BestChilds[0].GetMoveSequence();
            }
            else{
                bestState = BestChilds[0];
                for (int i = 1; i < BestChilds.Count; ++i) {
                    if(Score(bestState.GetState())<Score(BestChilds[i].GetState())){
                    bestState = BestChilds[i];
                    }
                } 
                //Get the actions to reach the best state
                bestMoves = bestState.GetMoveSequence();
            }
            

            if (bestMoves.Count == 0 || bestMoves[bestMoves.Count - 1].PlayerTaskType != PlayerTaskType.END_TURN) {
                bestMoves.Add(EndTurnTask.Any(bestState.GetState().CurrentPlayer.Controller));
            }

            PlayerTask move = bestMoves[0]; 
            return move;
        }


        private static double Score(POGame.POGame state) {
			return  agentScoreFunction(state.CurrentPlayer, state.CurrentPlayer) -
                    agentScoreFunction(state.CurrentPlayer, state.CurrentOpponent);
		}

        private static double agentScoreFunction(Controller ourPlayer, Controller player) {
            double score = 0;
            if (player.Hero.Health <= 0){
                return Double.MinValue;
            }
            else {
                score = Math.Sqrt(player.Hero.Health) * 2;
            }
            if(player.Game.CurrentOpponent.Hero.Health <1){
                return Double.MaxValue;
            }

            foreach (Minion minion in player.BoardZone.GetAll()) {
                            
                //Damage is evaluate at 1.0 per point
                score += minion.AttackDamage ;
                // Health is evaluated at 1 per point
                score += minion.Health;
                // Cards default to a budget of 2*[mana cost]+1.
                if(HasPassiveAbility(minion)){
                    score += 2 * minion.Card.Cost + 1;
                }
                else{
                    score +=3;
                }

            }
            int turnsTaken = player.Game.Turn/2;
            // These two are not calculated for the opponent
            if (ourPlayer.Equals(player)) {
                //Having no minions on the board subtracts 2.0 + 1.0 * [turn count to a max of 10]
                if(player.BoardZone.GetAll().Count() == 0){
                    score -= (2 + (turnsTaken >= 10 ? 10 : turnsTaken));
                }
                // Having cleared the enemy board adds 2.0 + 1.0 * [turn count to a max of 10]
                if(player.Game.CurrentOpponent.BoardZone.GetAll().Count() == 0){
                    score += (2 + (turnsTaken >= 10 ? 10 : turnsTaken));
                }
               
            }
            //Cards in hand are evaluated at 3.0 for the first 3 cards and 2.0 for every card beyond that
            for (int i = 0; i < player.HandZone.GetAll().Count(); ++i) {
                score += (i < 3) ? 3 : 2;
            }
            //At turns 10+: Remaining cards in deck are valued at (sqrt(n) – draw damage) * 1.0.
            if (turnsTaken >=10){
                //score += 100;
                int cardsInDeck = player.DeckZone.GetAll().Count();
                if (cardsInDeck != 0) {
                    score += Math.Sqrt(cardsInDeck);
                } else {
                    score -= 2;
                }
            }
            return score;
        }


        private static bool HasPassiveAbility(Minion minion){
            return minion.HasTaunt 
                || minion.HasCharge 
                || minion.HasWindfury 
                || minion.HasDivineShield 
                || minion.HasStealth 
                || minion.HasDeathrattle 
                || minion.HasBattleCry 
                || minion.HasInspire 
                || minion.HasLifeSteal 
                || minion.IsImmune;

        }

        public override void InitializeAgent()
		{
		}

		public override void InitializeGame()
		{

		}

        public override void FinalizeAgent()
		{
		}

		public override void FinalizeGame()
		{
		}
    }
}
