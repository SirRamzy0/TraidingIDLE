using System;
using UnityEngine;

namespace TraidingIDLE.Currencies.Simulation
{
    [Serializable]
    public struct PriceRoundingRule
    {
        [Min(0f)]
        public float minPrice;

        [Min(0.000001f)]
        public float step;
    }
}

