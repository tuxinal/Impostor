﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Events.Meeting;
using Impostor.Server.Events.Player;
using Impostor.Server.Net.State;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerMeetingHud : InnerNetObject
    {
        private readonly ILogger<InnerMeetingHud> _logger;
        private readonly IEventManager _eventManager;
        private readonly Game _game;
        private readonly GameNet _gameNet;
        private PlayerVoteArea[] _playerStates;

        public InnerMeetingHud(ILogger<InnerMeetingHud> logger, IEventManager eventManager, Game game)
        {
            _logger = logger;
            _eventManager = eventManager;
            _game = game;
            _gameNet = game.GameNet;
            _playerStates = null;

            Components.Add(this);
        }

        public byte ReporterId { get; private set; }

        private void PopulateButtons(byte reporter)
        {
            _playerStates = _gameNet.GameData.Players
                .Select(x =>
                {
                    var area = new PlayerVoteArea(this, x.Key);
                    area.SetDead(x.Value.PlayerId == reporter, x.Value.Disconnected || x.Value.IsDead);
                    return area;
                })
                .ToArray();
        }

        public override ValueTask<bool> SerializeAsync(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DeserializeAsync(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!await ValidateHost(CheatContext.Deserialize, sender) || !await ValidateBroadcast(CheatContext.Deserialize, sender, target))
            {
                return;
            }

            if (initialState)
            {
                PopulateButtons(0);

                foreach (var playerState in _playerStates)
                {
                    playerState.Deserialize(reader);

                    if (playerState.DidReport)
                    {
                        ReporterId = playerState.TargetPlayerId;
                    }
                }
            }
            else
            {
                var num = reader.ReadPackedUInt32();

                for (var i = 0; i < _playerStates.Length; i++)
                {
                    if ((num & 1 << i) != 0)
                    {
                        _playerStates[i].Deserialize(reader);
                    }
                }
            }
        }

        public override async ValueTask<bool> HandleRpcAsync(ClientPlayer sender, ClientPlayer? target, RpcCalls call, IMessageReader reader)
        {
            switch (call)
            {
                case RpcCalls.Close:
                {
                    if (!await ValidateHost(call, sender))
                    {
                        return false;
                    }

                    Rpc22Close.Deserialize(reader);
                    break;
                }

                case RpcCalls.VotingComplete:
                {
                    if (!await ValidateHost(call, sender))
                    {
                        return false;
                    }

                    Rpc23VotingComplete.Deserialize(reader, out var states, out var playerId, out var tie);
                    await HandleVotingComplete(sender, states, playerId, tie);
                    break;
                }

                case RpcCalls.CastVote:
                {
                    Rpc24CastVote.Deserialize(reader, out var playerId, out var suspectPlayerId);
                    return await HandleCastVote(sender, target, playerId, suspectPlayerId);
                }

                case RpcCalls.ClearVote:
                {
                    if (!await ValidateHost(call, sender))
                    {
                        return false;
                    }

                    Rpc25ClearVote.Deserialize(reader);
                    break;
                }

                case RpcCalls.CustomRpc:
                    return await HandleCustomRpc(reader, _game);

                default:
                    return await UnregisteredCall(call, sender);
            }

            return true;
        }

        private async ValueTask HandleVotingComplete(ClientPlayer sender, ReadOnlyMemory<byte> states, byte playerId, bool tie)
        {
            if (playerId != byte.MaxValue)
            {
                var player = _game.GameNet.GameData.GetPlayerById(playerId);
                if (player != null)
                {
                    player.Controller.Die(DeathReason.Exile);
                    await _eventManager.CallAsync(new PlayerExileEvent(_game, sender, player.Controller));
                }
            }

            await _eventManager.CallAsync(new MeetingEndedEvent(_game, this));
        }

        private async ValueTask<bool> HandleCastVote(ClientPlayer sender, ClientPlayer? target, byte playerId, sbyte suspectPlayerId)
        {
            if (sender.IsHost)
            {
                if (!await ValidateBroadcast(RpcCalls.CastVote, sender, target))
                {
                    return false;
                }
            }
            else
            {
                if (!await ValidateCmd(RpcCalls.CastVote, sender, target))
                {
                    return false;
                }
            }

            if (playerId != sender.Character!.PlayerId)
            {
                if (await sender.Client.ReportCheatAsync(RpcCalls.CastVote, $"Client sent {nameof(RpcCalls.CastVote)} to an unowned {nameof(InnerPlayerControl)}"))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
