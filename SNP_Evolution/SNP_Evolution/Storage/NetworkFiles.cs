using System;
using System.IO;
using Newtonsoft.Json;
using SnpEvolution.Networks;

namespace SnpEvolution.Storage
{
    public static class NetworkFiles
    {
        public static string ToJson(Network network) => Json.Write(network);

        // Returns null for malformed JSON or an invalid rule expression, since both come from user-supplied files.
        public static Network? FromJson(string json)
        {
            try
            {
                return Json.Read<Network>(json);
            }
            catch (Exception exception) when (exception is JsonException || exception is ArgumentException)
            {
                Console.WriteLine(exception.Message);
                return null;
            }
        }

        public static void Save(Network network, string path)
        {
            Console.WriteLine("----------- Saving Network -----------");
            SaveText(ToJson(network), path);
        }

        public static Network? Load(string path)
        {
            try
            {
                return FromJson(File.ReadAllText(path));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                Console.WriteLine(exception.Message);
                return null;
            }
        }

        public static void SaveText(string text, string path)
        {
            try
            {
                File.WriteAllText(path, text);
                Console.WriteLine("Saved the file to: {0}", path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Console.WriteLine(exception.Message);
            }
        }
    }
}
