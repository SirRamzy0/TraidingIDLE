using System;
using System.Collections.Generic;
using System.Text;

namespace TraidingIDLE.Localization
{
    public static class KnownLocalization
    {
        private static readonly Dictionary<string, string> CategoryKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Товарный бизнес", "category.goods_business" },
            { "Автобизнес", "category.auto_business" },
            { "Строительство", "category.construction" },
            { "Морской бизнес", "category.sea_business" },
            { "Авиабизнес", "category.aviation_business" },
            { "Goods", "category.goods_business" },
            { "Cars", "category.auto_business" },
            { "Construction", "category.construction" },
            { "Море", "category.sea_business_short" },
            { "Aviation", "category.aviation_business_short" },
            { "All", "scene.all" },
            { "Все", "scene.all" },
            { "Clothes", "scene.clothes" },
            { "Тачки", "scene.auto" },
            { "Real Estate", "scene.real_estate" },
            { "Yachts", "scene.boats" },
            { "Это самолет", "scene.aviation" },
        };

        private static readonly Dictionary<string, string> StaticTextKeys = new(StringComparer.Ordinal)
        {
            { "Кристаллы + монеты", "shop.offer_gems_coins" },
            { "Кристаллы", "scene.gems" },
            { "Игровая валюта", "scene.game_currency" },
            { "Отключает рекламу в игре на неделю", "shop.no_ads_week_description" },
            { "Заходи каждый день и получай подарки", "daily.description" },
            { "Заходи каждый день и получай награды", "daily.description" },
            { "Покупай компьютеры и улучшай их и майни крипту автоматически", "mining.tab_description" },
            { "Покупай компьютеры, улучшай их и майни крипту автоматически", "mining.tab_description" },
            { "Улучшения влияют на все риги", "mining.upgrades_affect_all_rigs" },
            { "Доход", "scene.income" },
            { "шкала добычи", "mining.progress_scale" },
            { "Шкала добычи", "mining.progress_scale" },
            { "Потенциальный выигрыш", "crash.potential_win_title" },
            { "Макс улучшеных ригов", "mining.maxed_rigs_stat" },
            { "Макс улучшенных ригов", "mining.maxed_rigs_stat" },
            { "Бонус к времени добычи", "mining.speed_bonus_stat" },
            { "бизнесов этого типа", "collections.this_business_type" },
            { "income бизнесов этого типа", "collections.income_this_business_type" },
            { "Вкладывай деньги и получай стабильный пассивный доход Это путь к успеху!", "business.tab_description" },
            { "Рискуй и побеждай! Темки могут помочь быстро подняться, но будь аккуратен!", "temki.tab_description" },
            { "Анализ сделки...", "scene.analysis_deal" },
            { "Успешная сделка", "risky.success" },
            { "Сделка сорвалась", "scene.failed_deal" },
            { "Прогноз не сбылся", "scene.forecast_failed" },
            { "Ожидание новых возможностей", "risky.waiting_new_opportunities" },
            { "🏆 Успешная сделка", "risky.success_with_icon" },
            { "✖ Сделка сорвалась", "scene.failed_deal_with_icon" },
            { "✖️ Сделка сорвалась", "scene.failed_deal_with_icon" },
            { "↻ Анализ сделки...", "scene.analysis_deal" },
            { "⟳ Анализ сделки...", "scene.analysis_deal" },
        };

        public static string TranslateCategory(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw ?? string.Empty;

            var normalized = NormalizeWhitespace(raw);
            return CategoryKeys.TryGetValue(normalized, out var key)
                ? LocalizationManager.Tr(key, raw)
                : raw;
        }

        public static string ToCanonicalCategoryKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw ?? string.Empty;

            return NormalizeWhitespace(raw) switch
            {
                "Goods" => "Товарный бизнес",
                "Clothes" => "Товарный бизнес",
                "Cars" => "Автобизнес",
                "Тачки" => "Автобизнес",
                "Construction" => "Строительство",
                "Real Estate" => "Строительство",
                "Море" => "Морской бизнес",
                "Yachts" => "Морской бизнес",
                "Aviation" => "Авиабизнес",
                "Это самолет" => "Авиабизнес",
                _ => raw,
            };
        }

        public static string TranslateBusinessName(string saveId, string fallback)
        {
            return TranslateById("business.name.", saveId, fallback);
        }

        public static string TranslateBusinessSkillTitle(string fallback)
        {
            var key = NormalizeWhitespace(fallback) switch
            {
                "Собрать деньги" => "business.skill.collect_money",
                "Собрать прибыль" => "business.skill.collect_profit",
                "Увеличить доход" => "business.skill.increase_income",
                "Усилить доход" => "business.skill.boost_income",
                "Получить выручку" => "business.skill.collect_revenue",
                _ => string.Empty,
            };

            return string.IsNullOrEmpty(key) ? fallback : LocalizationManager.Tr(key, fallback);
        }

        public static string TranslateTemkaName(string saveId, string fallback)
        {
            return TranslateById("temki.name.", saveId, fallback);
        }

        public static string TranslateTemkaDescription(string saveId, string fallback)
        {
            return TranslateById("temki.description.", saveId, fallback);
        }

        public static string TranslateCollectionName(string saveId, string fallback)
        {
            return TranslateById("collection.name.", saveId, fallback);
        }

        public static string TranslateCollectionDescription(string saveId, string fallback)
        {
            return TranslateById("collection.description.", saveId, fallback);
        }

        public static string TranslateCollectionCardTitle(string collectionSaveId, string cardSaveId, string fallback)
        {
            return TranslateById($"collection.card.{NormalizeId(collectionSaveId)}.", cardSaveId, fallback);
        }

        public static string TranslateCollectionFinalTitle(string collectionSaveId, string finalSaveId, string fallback)
        {
            return TranslateById($"collection.final.{NormalizeId(collectionSaveId)}.", finalSaveId, fallback);
        }

        public static bool TryTranslateStaticText(string originalText, out string translated)
        {
            translated = originalText;
            if (string.IsNullOrWhiteSpace(originalText))
                return false;

            var normalized = NormalizeWhitespace(originalText);
            if (!StaticTextKeys.TryGetValue(normalized, out var key))
                return false;

            translated = LocalizationManager.Tr(key, originalText);
            return true;
        }

        private static string TranslateById(string prefix, string id, string fallback)
        {
            var normalizedId = NormalizeId(id);
            return string.IsNullOrEmpty(normalizedId)
                ? fallback
                : LocalizationManager.Tr(prefix + normalizedId, fallback);
        }

        private static string NormalizeId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            var builder = new StringBuilder(id.Length);
            for (var i = 0; i < id.Length; i++)
            {
                var c = char.ToLowerInvariant(id[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                    builder.Append(c);
                else if (c == '_' || c == '-' || c == '.' || char.IsWhiteSpace(c))
                    builder.Append('_');
            }

            return builder.ToString().Trim('_');
        }

        private static string NormalizeWhitespace(string value)
        {
            return string.Join(" ", (value ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
