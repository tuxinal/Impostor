﻿namespace Impostor.Api.Games
{
    public interface IGameCodeFactory
    {
        GameCode Create(int gameCount);
    }
}