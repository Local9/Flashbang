﻿global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using Flashbang.Server.Models;
using Flashbang.Shared;
using FxEvents;
using Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Flashbang.Server
{
    public class Main : BaseScript
    {
        private Config _config = new();
        private PlayerList _playerList;
        private Log Logger = new();
            
        public Main()
        {
            Logger.Info($"Started Flashbang Server Resource");
            _config.Load();
            _playerList = Players;

            Load();
        }

        async void Load()
        {
            EventDispatcher eventDispatcher = new();
            await BaseScript.Delay(0);
            EventDispatcher.Mount("Flashbang:DispatchExplosion", new Action<Player, FlashbangMessage>(OnFlashbangMessageAsync));
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

        private void OnFlashbangMessageAsync([FromSource] Player player, FlashbangMessage message)
        {
            Logger.Debug($"Received Flashbang Message from '{player.Name}'");
            
            message.StunDuration = _config.StunDuration;
            message.AfterStunDuration = _config.AfterStunDuration;
            message.Range = _config.Range;
            message.Damage = _config.Damage;
            message.LethalRadius = _config.LethalRadius;

            List<Player> closestPlayers = GetClosestPlayers(message.Position, _config.MaxUpdateRange);

            EventDispatcher.Send(closestPlayers, "Flashbang:Explode", message);
        }
    }
}
