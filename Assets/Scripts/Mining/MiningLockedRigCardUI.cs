using TMPro;
using TraidingIDLE.Localization;
using UnityEngine;

namespace TraidingIDLE.Mining
{
    public sealed class MiningLockedRigCardUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText = null!;
        [SerializeField] private string messageFormat = "Купи предыдущий\nриг что бы\nразблокировать\nэтой слот";

        public void Configure(int rigIndex)
        {
            if (messageText != null)
                messageText.text = LocalizationManager.Tr("mining.locked_rig_message", messageFormat);
        }
    }
}
