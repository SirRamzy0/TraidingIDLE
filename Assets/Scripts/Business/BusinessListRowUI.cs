using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TraidingIDLE.Business
{
    /// <summary>
    /// Одна карточка бизнеса. Можно клонировать из префаба через <see cref="BusinessController"/> в ScrollRect Content.
    /// </summary>
    public sealed class BusinessListRowUI : MonoBehaviour
    {
        [Tooltip("Номер в Catalog задаёт контроллер при спавне/сборе списка. В префабе можно оставить 0.")]
        [SerializeField, Min(0)] private int businessIndex;
        [Tooltip("Выбор этого бизнеса (панель умения). Пусто — ищется Button на этом же GameObject.")]
        [SerializeField] private Button? selectButton;
        [Tooltip("Необязательно: объект «рамка». Цвет задаёт обычно Card Background ниже.")]
        [SerializeField] private GameObject? selectionHighlight;

        [Header("Фон карточки")]
        [Tooltip("Частый вариант: Image-подложка за текстом.")]
        [SerializeField] private Image? cardBackground;
        [SerializeField] private Color tintOwnedNeutral = new(0.25f, 0.42f, 0.72f, 0.95f);
        [SerializeField] private Color tintOwnedSelected = new(0.35f, 0.58f, 0.95f, 1f);
        [SerializeField] private Color tintNotPurchased = new(0.18f, 0.18f, 0.2f, 0.92f);
        [SerializeField] private TMP_Text? nameText;
        [SerializeField] private TMP_Text? categoryText;
        [SerializeField] private TMP_Text? incomePerHourText;
        [SerializeField] private TMP_Text? levelText;
        [SerializeField] private TMP_Text? actionPriceText;
        [SerializeField] private Button? primaryActionButton;
        [Tooltip("Отдельный TMP на кнопке: «Купить» / «Улучшить» (без суммы; сумма — в Action Price Text).")]
        [SerializeField] private TMP_Text? primaryActionVerbText;

        public int BusinessIndex => businessIndex;

        /// <summary>Вызывает контроллер при сборе строк с родителя — перезаписывает номер позиции в каталоге.</summary>
        public void AssignCatalogIndex(int indexInCatalog)
        {
            businessIndex = Mathf.Max(0, indexInCatalog);
        }

        /// <summary>Кнопка выбора: на корне строки или поле выше; вторая кнопка в дочерних — покупка/апгрейд.</summary>
        internal void ResolveButtonReferences()
        {
            if (selectButton == null)
                selectButton = GetComponent<Button>();

            if (primaryActionButton != null)
                return;

            foreach (var b in GetComponentsInChildren<Button>(true))
                if (b != null && !ReferenceEquals(b, selectButton))
                {
                    primaryActionButton = b;
                    break;
                }
        }

        public void BindSelect(UnityAction handler)
        {
            if (selectButton == null)
                return;

            selectButton.onClick.RemoveAllListeners();
            if (handler != null)
                selectButton.onClick.AddListener(handler);
        }

        public void BindPrimaryAction(UnityAction handler)
        {
            if (primaryActionButton == null)
                return;

            primaryActionButton.onClick.RemoveAllListeners();
            if (handler != null)
                primaryActionButton.onClick.AddListener(handler);
        }

        /// <param name="selected">Строка выбрана (портрет умения и т.д.).</param>
        /// <param name="owned">Есть хотя бы 1-й уровень (куплено).</param>
        public void RefreshAppearance(bool selected, bool owned)
        {
            if (cardBackground != null)
                cardBackground.color = !owned ? tintNotPurchased : selected ? tintOwnedSelected : tintOwnedNeutral;

            if (selectionHighlight != null)
                selectionHighlight.SetActive(selected);
        }

        public void RefreshRow(
            string name,
            string category,
            string incomeLine,
            string levelLine,
            string actionPriceSum,
            string primaryActionVerbCaption,
            bool showPrimaryActionButton,
            bool primaryInteractableWhenShown)
        {
            if (nameText != null)
                nameText.text = name;
            if (categoryText != null)
                categoryText.text = category;
            if (incomePerHourText != null)
                incomePerHourText.text = incomeLine;
            if (levelText != null)
                levelText.text = levelLine;

            if (actionPriceText != null)
                actionPriceText.text = actionPriceSum;

            if (primaryActionVerbText != null)
                primaryActionVerbText.text = showPrimaryActionButton ? primaryActionVerbCaption : "";

            if (primaryActionButton != null)
            {
                primaryActionButton.gameObject.SetActive(showPrimaryActionButton);
                if (showPrimaryActionButton)
                    primaryActionButton.interactable = primaryInteractableWhenShown;
            }
        }
    }
}
