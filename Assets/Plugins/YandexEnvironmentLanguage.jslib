mergeInto(LibraryManager.library, {
    GetYandexEnvironmentLanguage_js: function () {
        var lang = "ru";

        try {
            if (typeof ysdk !== "undefined" &&
                ysdk !== null &&
                ysdk.environment !== undefined &&
                ysdk.environment !== null &&
                ysdk.environment.i18n !== undefined &&
                ysdk.environment.i18n !== null &&
                ysdk.environment.i18n.lang) {
                lang = ysdk.environment.i18n.lang;
            }
        } catch (error) {
            console.warn("Failed to read Yandex environment language", error);
        }

        var length = lengthBytesUTF8(lang) + 1;
        var buffer = _malloc(length);
        stringToUTF8(lang, buffer, length);
        return buffer;
    }
});
