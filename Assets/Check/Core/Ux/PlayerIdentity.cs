using System.Text;

namespace Check.Core
{
    /// <summary>Prénom choisi par le joueur : nettoyé, raccourci, « Toi » s'il est vide.</summary>
    public static class PlayerIdentity
    {
        public const string DefaultName = "Toi";
        public const int MaxLength = 14;

        public static string Sanitize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DefaultName;
            var sb = new StringBuilder();
            bool space = false;
            foreach (char ch in raw.Trim())
            {
                // Pas de balises de texte enrichi ni de caractères de contrôle dans un nom affiché partout.
                if (char.IsControl(ch) || ch == '<' || ch == '>') continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (!space && sb.Length > 0) sb.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                sb.Append(ch);
                if (sb.Length >= MaxLength) break;
            }
            string name = sb.ToString().Trim();
            return name.Length == 0 ? DefaultName : name;
        }
    }
}
