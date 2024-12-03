using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HouseWhisperHomework
{
    public static partial class Utils
    {
        private static string[] allowed_code_chars = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        public static float NextFractionalFloat(this Random random, int minValue, int maxValue, int divisions)
        {
          if (minValue > maxValue)
            throw new ArgumentException("minValue must be less than or equal to maxValue.");
          int scaledRandom = random.Next(minValue * divisions, maxValue * divisions + 1);
          return scaledRandom / (float)divisions;
        }
        public static string Base64UrlEncode(this byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }
        public static Exception AddData(this Exception ex, object Key, object? Value)
        {
            ex.Data.Add(Key, Value);
            return ex;
        }

        public static string LettersAndDigits(this string input)
        {
            return String.Join("", input.Where(chr => char.IsLetterOrDigit(chr)).ToArray());
        }

        public static string CssStyle(this string input)
        {
            return String.Join("", input.ToLower().Select(chr => char.IsWhiteSpace(chr) ? '-' : chr).ToArray());
        }

        public static string ToFileName(this string input)
        {
            return String.Join("", input.Replace(' ', '_').Where(chr => char.IsLetterOrDigit(chr) || chr == '_').ToArray());
        }

        public static bool IsDigits(this string input)
        {
            return input.All(chr => char.IsDigit(chr));
        }

        public static IEnumerable<string> Break(this string input)
        {
            return input.Split(' ').Select(word => word.LettersAndDigits()); 
        }

        public static IEnumerable<(T Item, int Count)> CountDistinct<T>(this IEnumerable<T> input)
        {
            return input
                .GroupBy(word => word)
                .Select(wordGroup => (Item: wordGroup.Key, Count: wordGroup.Count()));
        }

        static string[] stopWords = new string[] { "on", "with", "and", "for" };
        public static IEnumerable<string> WordBreak(this string input)
        {
            return
                String.Join(
                    "",
                    input.ToLower().Select(chr => char.IsLetterOrDigit(chr) ? chr : ' '))
                .Split(' ')
                .Where(word => !stopWords.Contains(word) && word.Length > 1);
        }

        public static string? LettersOrDigits(this string input)
        {
            if (String.IsNullOrWhiteSpace(input))
                return null;
            else
                return String.Join("", input.Where(chr => char.IsLetterOrDigit(chr)).ToArray());
        }

        public static byte[] FromHex(this string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        public static IEnumerable<(int, IEnumerable<T>)> Chunk<T>(this IEnumerable<T> input, int chunkSize)
        {
            return input
                .Select((product, index) => new { Chunk = index / chunkSize, Product = product })
                .GroupBy(pair => pair.Chunk)
                .Select(grouping => (Chunk: grouping.Key, Products: grouping.Select(pair => pair.Product)));
        }
    }
}
