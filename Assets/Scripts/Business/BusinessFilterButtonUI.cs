using UnityEngine;
using TraidingIDLE.UI;

namespace TraidingIDLE.Business
{
    public sealed class BusinessFilterButtonUI : CategoryFilterButtonBase
    {
        [SerializeField] private bool showAll;

        public bool ShowAll => showAll;

        protected override bool IsAllFilter => showAll;
    }
}
