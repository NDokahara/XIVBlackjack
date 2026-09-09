namespace XIVBlackjack.Data;

public enum GameState
{
    NotRunning = 0,
    Done = 2,

    Registration = 101,
    Crash = 199,

    // Blackjack
    PrepareRound = 200,
    DrawFirstCards = 201,
    DrawSecondCards = 202,
    PlayerRound = 203,
    Hit = 204,
    DoubleDown = 205,
    DrawSplit = 206,
    DealerRound = 207,
    DealerFirstCards = 208,
    DealerSecondCards = 209,
    DrawDealerCard = 210,
    DealerDone = 211,
    FillDraw = 212
}
