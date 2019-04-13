
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SNP_Network = SNP_First_Test.Network.Network;


/// <summary>
/// 15068126 - Michael Stachowicz
/// Network To Json conversion done using the Newtonsoft JSON library (CloneHelper.cs, ICloneFactory.cs, ReflectionCloner.cs)
/// </summary>

namespace SNP_First_Test.Utilities
{
    class Utils
    {
        /// <summary>
        /// Append strict search to regex
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string RegexAppendStrict(string input)
        {
            input = "^" + input + "$";
            return input;
        }

        /// <summary>
        /// Convert the provided SN P Network to a JSON format for exporting
        /// </summary>
        /// <param name="network">SNP network to convert</param>
        /// <returns>JSON String</returns>
        public static string ConvertNetworkToJson(SNP_Network network)
        {
            return JsonConvert.SerializeObject(network, Formatting.Indented);
        }

        /// <summary>
        /// Convert JSON back to a SN P Network
        /// </summary>
        /// <param name="json">JSON string to turn into a SN P network</param>
        /// <returns>new SNP Network object based on the JSON string</returns>
        public static SNP_Network ConvertJsonToNetwork(string json)
        {
            return JsonConvert.DeserializeObject<SNP_Network>(json);
        }

        /// <summary>
        /// Save data to file
        /// </summary>
        /// <param name="data">string to save</param>
        /// <param name="filename">path / filename</param>
        public static void SaveToFile(string data, string filename)
        {
            try
            {
                string path = Directory.GetCurrentDirectory() + "/" + filename;
                File.WriteAllText(path, data);
                Console.WriteLine("Saved the file to: {0}", path);
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Read network from provided file
        /// </summary>
        /// <param name="filename">Filename, must start at the current directory</param>
        /// <returns>SNP network</returns>
        public static SNP_Network ReadNetworkFromFile(string filename)
        {
            try
            {
                string path = Directory.GetCurrentDirectory();
                try
                {
                    using (StreamReader r = new StreamReader(filename))
                    {
                        string parsedJson = r.ReadToEnd();
                        return ConvertJsonToNetwork(parsedJson);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return null;
                }
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        /// <summary>
        /// Save network to file
        /// </summary>
        /// <param name="network">Network to save</param>
        /// <param name="filename">FileName</param>
        public static void SaveNetwork(SNP_Network network, string filename)
        {
            Console.WriteLine("----------- Saving Network -----------");
            string json = ConvertNetworkToJson(network);
            SaveToFile(json, filename);
        }

        /// <summary>
        /// Convert fitness list of best fitnesses to a CSV file
        /// </summary>
        /// <param name="fitnessList">List of fitnesses</param>
        /// <returns>CSV string</returns>
        public static string ConvertToCSV(List<List<float>> fitnessList)
        {
            Console.WriteLine("----------- Saving Data -----------");
            String lines = "";
            foreach (List<float> list in fitnessList)
            {
                Console.WriteLine("Data: {0}, Count {1}", list[0], list.Count);
                lines += String.Join(",", list.Select(x => x.ToString()).ToArray());
                lines += "\n";
            }
            return lines;
        }

        /// <summary>
        ///  Create a new folder with the provided name
        /// </summary>
        /// <param name="folderName">Create new folder with given name</param>
        public static void CreateFolder(string folderName)
        {
            Console.WriteLine("----------- Creating Folder {0} -----------", folderName);
            Directory.CreateDirectory(Directory.GetCurrentDirectory() + "/" + folderName);
        }

        /// <summary>
        /// Save CSV to file
        /// </summary>
        /// <param name="fitnessList"></param>
        /// <param name="filename"></param>
        public static void SaveCSV(List<List<float>> fitnessList, string filename)
        {
            Console.WriteLine("----------- Saving Data -----------");
            string csv = ConvertToCSV(fitnessList);
            SaveToFile(csv, filename);
        }




    }





}
