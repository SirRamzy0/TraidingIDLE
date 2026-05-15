namespace TraidingIDLE.Currencies.Simulation
{
    public enum MarketStateType
    {
        // Legacy regular states (kept for save compatibility)
        Legacy_ChopInCorridor = 0,
        Legacy_Flat = 1,
        Legacy_SlowUpThenDump = 2,
        Legacy_SlowUpThenPumpAndDump = 3,
        Legacy_LongUptrend = 4,
        Legacy_LongDowntrend = 5,
        Legacy_MarketCrash = 6,
        Legacy_Calm = 7,
        Legacy_DeadFlatThenRocketPump = 8,
        Legacy_DeadFlatThenRocketDump = 9,
        Legacy_BusinessSkillPumpDump = 10,
        Legacy_StairUp = 11,
        Legacy_StairDown = 12,
        Legacy_CompressionBreakout = 13,
        Legacy_LowRangeRecovery = 14,
        Legacy_AccumulationRun = 15,

        // New regular states
        CalmCorridor = 100,
        ActiveCorridor = 101,
        ScalpingWindow = 102,
        PressureUp = 103,
        PressureDown = 104,
        SlowTrendUp = 105,
        SlowTrendDown = 106,
        VolatileChop = 107,
        Accumulation = 108,
        Distribution = 109,

        // New patterns
        BigSaw = 200,
        StaircaseShiftUp = 201,
        StaircaseShiftDown = 202,
        FalseBreakoutUp = 203,
        FalseBreakoutDown = 204,
        PumpAndCorrection = 205,
        DumpAndRecovery = 206,
        CompressionBreakoutNew = 207,
    }
}