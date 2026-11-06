namespace NPUtil.Core.Translation;

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken ct = default);
    string DetectLanguage(string text);
}
