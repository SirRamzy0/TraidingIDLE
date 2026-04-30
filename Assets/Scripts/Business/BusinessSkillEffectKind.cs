namespace TraidingIDLE.Business
{
    public enum BusinessSkillEffectKind : byte
    {
        InstantRubles = 0,
        TemporaryBoostAllBusinessIncome = 1,
        TemporaryBoostMiningIncome = 2,
        MarketManipulate = 3,
    }

    public enum BusinessTemporaryBuffKind : byte
    {
        None = 0,
        AllBusinessIncome = 1,
        MiningIncome = 2,
    }
}
