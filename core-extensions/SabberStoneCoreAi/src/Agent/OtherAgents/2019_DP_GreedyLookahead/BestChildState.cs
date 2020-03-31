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

using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCoreAi.Score;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCoreAi.src.Agent
{
    // Contain current game state, actions to reach state
        partial class BestChildNode {
            private POGame.POGame currentState;
            private List<PlayerTask> movesSequence;
            private List<PlayerTask> possibleMoves;

            public bool IsFinisher()
            {
                bool finisher = possibleMoves.All(option => option.PlayerTaskType == PlayerTaskType.END_TURN);
                return finisher;
            }

            public POGame.POGame GetState()
            {
                return currentState;
            }

                public List<PlayerTask> GetMoves()
            {
                return possibleMoves;
            }
            
            public List<PlayerTask> GetMoveSequence()
            {
                return movesSequence;
            }

            public BestChildNode(BestChildNode parent, PlayerTask task, POGame.POGame state) {
				this.currentState = state;
                this.movesSequence = new List<PlayerTask>(parent.movesSequence);
                this.movesSequence.Add(task);
                this.possibleMoves = new List<PlayerTask>(currentState.CurrentPlayer.Options());
            }

            public BestChildNode(POGame.POGame game) {
                this.currentState = game.getCopy();
                this.movesSequence = new List<PlayerTask>();
            }
            public void SetActions(List<PlayerTask> actions){
                this.possibleMoves = actions;
            }
        }
}