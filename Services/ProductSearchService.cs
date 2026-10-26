using System.Text.RegularExpressions;
using WholesaleRetailStore.Models;

namespace WholesaleRetailStore.Services
{
    /// <summary>
    /// Result of a product search, including the "did you mean" correction if one was made.
    /// </summary>
    /// <param name="Query">The query exactly as typed.</param>
    /// <param name="CorrectedQuery">The query after fixing typos, or null if nothing needed fixing.</param>
    /// <param name="Products">Matching products, best match first.</param>
    public sealed record ProductSearchResult(string Query, string? CorrectedQuery, List<Product> Products);

    /// <summary>
    /// Typo-tolerant product search: typing "mlk" finds "milk", like a search engine's "did you mean".
    /// </summary>
    /// <remarks>
    /// Each word of the query is checked against the words used in the catalogue. A word that
    /// already matches something (as a whole word, a prefix, or a substring) is left alone. Otherwise
    /// it is replaced by the closest catalogue word, measured by edit distance (insert, delete,
    /// substitute, swap two neighbours) or by being the same word with letters left out ("mlk" in "milk").
    /// It works on a plain list of products, so it needs no database and no external service.
    /// </remarks>
    public class ProductSearchService
    {
        private static readonly Regex WordPattern = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

        /// <summary>
        /// Searches the given products.
        /// </summary>
        /// <param name="products">Products to search.</param>
        /// <param name="query">What the user typed. Blank returns every product.</param>
        /// <param name="autoCorrect">When false the query is used exactly as typed (the "search instead for" link).</param>
        public ProductSearchResult Search(IReadOnlyList<Product> products, string? query, bool autoCorrect = true)
        {
            var typed = (query ?? string.Empty).Trim();
            var tokens = Tokenize(typed);

            if (tokens.Count == 0)
            {
                return new ProductSearchResult(typed, null, products.ToList());
            }

            var vocabulary = BuildVocabulary(products);
            var resolved = new List<string>();
            var corrected = false;

            foreach (var token in tokens)
            {
                if (IsKnown(token, vocabulary) || !autoCorrect)
                {
                    resolved.Add(token);
                    continue;
                }

                var fix = ClosestWord(token, vocabulary);
                if (fix != null)
                {
                    resolved.Add(fix);
                    corrected = true;
                }
                else
                {
                    resolved.Add(token);
                }
            }

            var matches = products
                .Select(p => (Product: p, Score: Score(p, resolved)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Product.Name)
                .Select(x => x.Product)
                .ToList();

            return new ProductSearchResult(typed, corrected ? string.Join(' ', resolved) : null, matches);
        }

        /// <summary>
        /// Maximum edit distance allowed for a word of the given length. Short words get less
        /// room, otherwise nearly everything would match everything.
        /// </summary>
        private static int MaxDistance(int length) => length switch
        {
            <= 2 => 0,
            <= 5 => 1,
            _ => 2
        };

        private static List<string> Tokenize(string text) =>
            WordPattern.Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

        /// <summary>Words used in the catalogue, mapped to whether any product has them in its name.</summary>
        private static Dictionary<string, bool> BuildVocabulary(IReadOnlyList<Product> products)
        {
            var vocabulary = new Dictionary<string, bool>();
            foreach (var product in products)
            {
                foreach (var word in Tokenize(product.Description))
                {
                    vocabulary.TryAdd(word, false);
                }

                foreach (var word in Tokenize(product.Name))
                {
                    vocabulary[word] = true;
                }
            }

            return vocabulary;
        }

        /// <summary>True if the token already matches a catalogue word, so it needs no correction.</summary>
        private static bool IsKnown(string token, Dictionary<string, bool> vocabulary) =>
            vocabulary.Keys.Any(w => w.StartsWith(token, StringComparison.Ordinal)
                                     || (token.Length >= 3 && w.Contains(token, StringComparison.Ordinal)));

        /// <summary>
        /// Picks the closest catalogue word within the allowed distance. Ties go to a word that
        /// starts with the same letter, then to product-name words, then alphabetically.
        /// </summary>
        private static string? ClosestWord(string token, Dictionary<string, bool> vocabulary)
        {
            string? best = null;
            var bestKey = (Distance: int.MaxValue, DifferentFirst: 1, NotInName: 1);

            foreach (var (word, inName) in vocabulary)
            {
                var distance = EditDistance(token, word);

                // Letters dropped ("mlk" -> "milk", "blk" -> "black"): every letter of the token
                // appears in order in the word and the first letters agree.
                if (token.Length >= 3 && word[0] == token[0] && IsSubsequence(token, word) && word.Length - token.Length <= token.Length / 2 + 1)
                {
                    distance = Math.Min(distance, word.Length - token.Length);
                }
                else if (distance > MaxDistance(token.Length))
                {
                    continue;
                }

                var key = (distance, word[0] == token[0] ? 0 : 1, inName ? 0 : 1);
                if (best == null || key.CompareTo(bestKey) < 0
                    || (key.CompareTo(bestKey) == 0 && string.CompareOrdinal(word, best) < 0))
                {
                    best = word;
                    bestKey = key;
                }
            }

            return best;
        }

        /// <summary>Relevance of a product to the resolved query words; 0 means no match.</summary>
        private static int Score(Product product, List<string> words)
        {
            var nameWords = Tokenize(product.Name);
            var descriptionWords = Tokenize(product.Description);
            var score = 0;

            foreach (var word in words)
            {
                if (nameWords.Contains(word)) score += 10;
                else if (nameWords.Any(w => w.StartsWith(word, StringComparison.Ordinal))) score += 8;
                else if (word.Length >= 3 && nameWords.Any(w => w.Contains(word, StringComparison.Ordinal))) score += 5;

                if (descriptionWords.Contains(word)) score += 4;
                else if (descriptionWords.Any(w => w.StartsWith(word, StringComparison.Ordinal))) score += 3;
                else if (word.Length >= 3 && descriptionWords.Any(w => w.Contains(word, StringComparison.Ordinal))) score += 1;
            }

            return score;
        }

        private static bool IsSubsequence(string small, string big)
        {
            var i = 0;
            foreach (var c in big)
            {
                if (i < small.Length && small[i] == c) i++;
            }

            return i == small.Length;
        }

        /// <summary>
        /// Optimal-string-alignment distance: the fewest insertions, deletions, substitutions or
        /// swaps of two neighbouring letters needed to turn one word into the other.
        /// </summary>
        public static int EditDistance(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (var j = 0; j <= b.Length; j++) d[0, j] = j;

            for (var i = 1; i <= a.Length; i++)
            {
                for (var j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                    }
                }
            }

            return d[a.Length, b.Length];
        }
    }
}
