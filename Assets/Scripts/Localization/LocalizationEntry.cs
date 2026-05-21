using System;

namespace TraidingIDLE.Localization
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string ru;
        public string en;
        public string de;
        public string es;

        public string Get(string languageCode)
        {
            if (languageCode == "en")
                return en;

            if (languageCode == "de")
                return de;

            if (languageCode == "es")
                return es;

            return ru;
        }
    }
}
