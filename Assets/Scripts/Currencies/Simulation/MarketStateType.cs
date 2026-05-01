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
        Calm = 7,
        DeadFlatThenRocketPump = 8,
        DeadFlatThenRocketDump = 9,
        BusinessSkillPumpDump = 10,
    }
}
