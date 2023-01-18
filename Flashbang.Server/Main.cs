global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using Flashbang.Server.Models;
using Flashbang.Shared;
using FxEvents;
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
            EventDispatcher.Mount("Flashbang:DispatchExplosion", new Action<FlashbangMessage>(OnFlashbangMessage));
            _config.Load();
            _playerList = Players;
        }

        public List<Player> GetClosestPlayers(Vector3 position, float range)
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

                if (Vector3.Distance(player.Character.Position, position) <= _config.MaxUpdateRange)
                {
                    closestPlayers.Add(player);
                }
            }

            return closestPlayers;
        }

        private void OnFlashbangMessage(FlashbangMessage message)
        {
            message.StunDuration = _config.StunDuration;
            message.AfterStunDuration = _config.AfterStunDuration;
            message.Range = _config.Range;
            message.Damage = _config.Damage;
            message.LethalRadius = _config.LethalRadius;

            List<Player> closestPlayers = GetClosestPlayers(message.Position, _config.Range);

            EventDispatcher.Send(closestPlayers, "Flashbang:DispatchExplosion", message);
        }
    }
}
