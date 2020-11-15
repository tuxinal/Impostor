﻿using System;
using System.Collections.Generic;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;

namespace Impostor.Server.Net.Inner.Objects
{
    internal class InnerPlayerInfo : IInnerPlayerInfo
    {
        public InnerPlayerInfo(byte playerId)
        {
            PlayerId = playerId;
        }

        public InnerPlayerControl Controller { get; internal set; }

        public byte PlayerId { get; }

        public string PlayerName { get; internal set; }

        public byte ColorId { get; internal set; }

        public uint HatId { get; internal set; }

        public uint PetId { get; internal set; }

        public uint SkinId { get; internal set; }

        public bool Disconnected { get; internal set; }

        public bool IsImpostor { get; internal set; }

        public bool IsDead { get; internal set; }

        public DeathReason LastDeathReason { get; internal set; }

        public List<ITaskInfo> Tasks { get; internal set; }

        public DateTimeOffset LastMurder { get; set; }

        public bool CanMurder(IGame game)
        {
            if (!IsImpostor)
            {
                return false;
            }

            return DateTimeOffset.UtcNow.Subtract(LastMurder).TotalSeconds >= game.Options.KillCooldown;
        }

        public void Serialize(IMessageWriter writer)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(IMessageReader reader)
        {
            PlayerName = reader.ReadString();
            ColorId = reader.ReadByte();
            HatId = reader.ReadPackedUInt32();
            PetId = reader.ReadPackedUInt32();
            SkinId = reader.ReadPackedUInt32();
            var flag = reader.ReadByte();
            Disconnected = (flag & 1) > 0;
            IsImpostor = (flag & 2) > 0;
            IsDead = (flag & 4) > 0;
            var taskCount = reader.ReadByte();
            Tasks = new List<ITaskInfo>();
            for (var i = 0; i < taskCount; i++)
            {
                var task = new InnerGameData.TaskInfo();
                task.Deserialize(reader);
                Tasks.Add(task);
            }
        }
    }
}
