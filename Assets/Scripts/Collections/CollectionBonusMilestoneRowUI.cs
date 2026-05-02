using TMPro;
using UnityEngine;

namespace TraidingIDLE.Collections
{
    public sealed class CollectionBonusMilestoneRowUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text conditionText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private Color reachedColor = new(0f, 1f, 0.55f, 1f);
        [SerializeField] private Color lockedColor = Color.white;

        public void Configure(string condition, string bonus, bool reached)
        {
            if (conditionText != null)
            {
                conditionText.text = condition;
                conditionText.color = reached ? reachedColor : lockedColor;
            }

            if (bonusText != null)
            {
                bonusText.text = bonus;
                bonusText.color = reached ? reachedColor : lockedColor;
            }

            gameObject.SetActive(true);
        }
    }
}
