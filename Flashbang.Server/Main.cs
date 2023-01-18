﻿global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using Flashbang.Server.Models;
using Flashbang.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flashbang.Server
{
    public class Main : BaseScript
    {
        private Config _config = new();
        private PlayerList _playerList;
            
        public Main()
        {
            _config.Load();
            _playerList = Players;

            EventHandlers["Flashbang:DispatchExplosion"] += new Action<string>(OnFlashbangMessageAsync);
        }

        internal List<Player> GetClosestPlayers(Vector3 position, float range)
        {
            List<Player> closestPlayers = new();

            for (int i = 0; i < _playerList.Count(); i++)
            {
                Player player = _playerList[i];

                if (player == null || player.Character == null) continue;

                int playerPedHandle = player.Character.Handle;
                bool isEntityVisible = API.IsEntityVisible(playerPedHandle);
                bool isPlayerDead = API.GetEntityHealth(playerPedHandle) == 0;
                if (!isEntityVisible || isPlayerDead) continue;

                if (Vector3.Distance(player.Character.Position, position) <= range)
                {
                    closestPlayers.Add(player);
                }
            }

            return closestPlayers;
        }

        private void OnFlashbangMessageAsync(string message)
        {
            FlashbangMessage flashbangMessage = JsonConvert.DeserializeObject<FlashbangMessage>(message);
            flashbangMessage.StunDuration = _config.StunDuration;
            flashbangMessage.AfterStunDuration = _config.AfterStunDuration;
            flashbangMessage.Range = _config.Range;
            flashbangMessage.Damage = _config.Damage;
            flashbangMessage.LethalRadius = _config.LethalRadius;

            TriggerClientEvent("Flashbang:DispatchExplosion", JsonConvert.SerializeObject(flashbangMessage));
        }
    }
}
