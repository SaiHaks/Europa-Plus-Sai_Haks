using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Europa.Speech
{
    public sealed class SoulnreakerAccentSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ReplacementAccentSystem _replacement = default!;

        private static readonly IReadOnlyList<string> SoulbreakInvocation = new List<string>{
            ", во имя Ненавидящего!",
            ", колесо вращается!",
            ", цикл повторяется!",
            ", прими муку!",
            ", славься Немилосердный!",
            ", восторг крушения!",
            ", да свершится пытка!"
        }.AsReadOnly();

        private static readonly IReadOnlyList<string> SoulbreakGreetings = new List<string>{
            "Узник Ненавидящего,",
            "Раб Цикла,",
            "Слуга Колеса,",
            "Жертва Творца,",
            "Пленник Мучения,"
        }.AsReadOnly();

        private static readonly IReadOnlyList<string> SufferingPhrases = new List<string>{
            ", боль очищает...",
            ", мука приближает...",
            ", страдание есть истина...",
            ", прими свою пытку...",
            ", крушение неизбежно...",
            ", наслаждайся болью..."
        }.AsReadOnly();

        public override void Initialize()
        {
            SubscribeLocalEvent<SoulbreakerAccentComponent, AccentGetEvent>(OnAccent);
        }

        private void OnAccent(EntityUid uid, SoulbreakerAccentComponent component, AccentGetEvent args)
        {
            var message = args.Message;

            message = _replacement.ApplyReplacements(message, "SoulbreakerAccent");

            // if (_random.Prob(0.1f))
            // {
            //     message = TrimAllEndPunctuation(message);
            //     message += _random.Pick(SoulbreakInvocation);
            // }
            //
            // if (_random.Prob(0.05f))
            // {
            //     if (message.Length > 0)
            //     {
            //         var firstCharLower = char.ToLower(message[0]);
            //         message = firstCharLower + message[1..];
            //         message = _random.Pick(SoulbreakGreetings) + " " + message;
            //         message = char.ToUpper(message[0]) + message[1..];
            //     }
            // }
            //
            // if (_random.Prob(0.05f))
            // {
            //     message = TrimAllEndPunctuation(message);
            //     message += _random.Pick(SufferingPhrases);
            // }

            args.Message = ApplyPhoneticReplacements(message);
        }

        private string ApplyPhoneticReplacements(string message)
        {
            // Фонетические замены букв для грубого арабского акцента
            return message
                .Replace("и", "ы").Replace("И", "Ы")
                .Replace("е", "э").Replace("Е", "Э")
                .Replace("я", "йа").Replace("Я", "Йа")
                .Replace("ё", "йо").Replace("Ё", "Йо")
                .Replace("ю", "йу").Replace("Ю", "Йу")
                .Replace("ть", "т").Replace("Ть", "Т")
                .Replace("сь", "с").Replace("Сь", "С")
                .Replace("чь", "ч").Replace("Чь", "Ч")
                .Replace("зь", "з").Replace("Зь", "З")
                .Replace("ь", "").Replace("Ь", "") // Убираем мягкий знак
                .Replace("ъ", "").Replace("Ъ", ""); // Убираем твердый знак
        }

        // Убирает все знаки препинания с конца до первого не-знака
        private string TrimAllEndPunctuation(string message)
        {
            int i = message.Length - 1;
            while (i >= 0 && IsPunctuation(message[i]))
            {
                i--;
            }
            return message.Substring(0, i + 1);
        }

        private bool IsPunctuation(char c)
        {
            return c == '.' || c == ',' || c == '!' || c == '?' || c == ';' || c == ':' || c == ' ';
        }
    }
}
