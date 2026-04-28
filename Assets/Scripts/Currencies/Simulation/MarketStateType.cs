namespace TraidingIDLE.Currencies.Simulation
{
    public enum MarketStateType
    {
        ChopInCorridor = 0,
        Flat = 1,
        SlowUpThenDump = 2,
        SlowUpThenPumpAndDump = 3,
        LongUptrend = 4,
        LongDowntrend = 5,
        MarketCrash = 6,
        /// <summary>Плавное движение с малым шумом в середине коридора (цели ±10–30% от центра).</summary>
        Calm = 7,
    }
}

