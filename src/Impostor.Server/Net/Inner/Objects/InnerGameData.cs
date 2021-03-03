﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Net.Inner.Objects.Components;
using Impostor.Server.Net.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerGameData : InnerNetObject, IInnerGameData
    {
        private readonly ILogger<InnerGameData> _logger;
        private readonly Game _game;
        private readonly ConcurrentDictionary<byte, InnerPlayerInfo> _allPlayers;

        public InnerGameData(ILogger<InnerGameData> logger, Game game, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _game = game;
            _allPlayers = new ConcurrentDictionary<byte, InnerPlayerInfo>();

            Components.Add(this);
            Components.Add(ActivatorUtilities.CreateInstance<InnerVoteBanSystem>(serviceProvider));
        }

        public int PlayerCount => _allPlayers.Count;

        public IReadOnlyDictionary<byte, InnerPlayerInfo> Players => _allPlayers;

        public InnerPlayerInfo? GetPlayerById(byte id)
        {
            if (id == byte.MaxValue)
            {
                return null;
            }

            return _allPlayers.TryGetValue(id, out var player) ? player : null;
        }

        public override ValueTask<bool> SerializeAsync(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DeserializeAsync(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!await ValidateHost(CheatContext.Deserialize, sender))
            {
                return;
            }

            if (initialState)
            {
                var num = reader.ReadPackedInt32();

                for (var i = 0; i < num; i++)
                {
                    var playerId = reader.ReadByte();
                    var playerInfo = new InnerPlayerInfo(playerId);

                    playerInfo.Deserialize(reader);

                    if (!_allPlayers.TryAdd(playerInfo.PlayerId, playerInfo))
                    {
                        throw new ImpostorException("Failed to add player to InnerGameData.");
                    }
                }
            }
            else
            {
                throw new NotImplementedException("This shouldn't happen, according to Among Us disassembly.");
            }
        }

        public override async ValueTask<bool> HandleRpcAsync(ClientPlayer sender, ClientPlayer? target, RpcCalls call, IMessageReader reader)
        {
            if (!await ValidateHost(call, sender))
            {
                return false;
            }

            switch (call)
            {
                case RpcCalls.SetTasks:
                {
                    Rpc29SetTasks.Deserialize(reader, out var playerId, out var taskTypeIds);
                    SetTasks(playerId, taskTypeIds);
                    break;
                }

                case RpcCalls.UpdateGameData:
                {
                    while (reader.Position < reader.Length)
                    {
                        using var message = reader.ReadMessage();
                        var player = GetPlayerById(message.Tag);
                        if (player != null)
                        {
                            player.Deserialize(message);
                        }
                        else
                        {
                            var playerInfo = new InnerPlayerInfo(message.Tag);

                            playerInfo.Deserialize(reader);

                            if (!_allPlayers.TryAdd(playerInfo.PlayerId, playerInfo))
                            {
                                throw new ImpostorException("Failed to add player to InnerGameData.");
                            }
                        }
                    }

                    break;
                }

                case RpcCalls.CustomRpc:
                    return await HandleCustomRpc(reader, _game);

                default:
                    return await UnregisteredCall(call, sender);
            }

            return true;
        }

        internal void AddPlayer(InnerPlayerControl control)
        {
            var playerId = control.PlayerId;
            var playerInfo = new InnerPlayerInfo(control.PlayerId);

            if (_allPlayers.TryAdd(playerId, playerInfo))
            {
                control.PlayerInfo = playerInfo;
            }
        }

        private void SetTasks(byte playerId, ReadOnlyMemory<byte> taskTypeIds)
        {
            var player = GetPlayerById(playerId);
            if (player == null)
            {
                _logger.LogTrace("Could not set tasks for playerId {0}.", playerId);
                return;
            }

            if (player.Disconnected)
            {
                return;
            }

            player.Tasks = new List<TaskInfo>(taskTypeIds.Length);

            foreach (var taskId in taskTypeIds.ToArray())
            {
                player.Tasks.Add(new TaskInfo
                {
                    Id = taskId,
                });
            }
        }
    }
}
