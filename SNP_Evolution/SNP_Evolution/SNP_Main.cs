using System;
using SNP_First_Test.Network;
using SNP_Network = SNP_First_Test.Network.Network;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SNP_First_Test.Utilities;
using SNP_First_Test.Genetic_Algorithms;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SNP_First_Test
{
    /// <summary>
    /// 15068126, Michael Stachowicz
    /// Reflection Cloner (ClonerHelpers.cs, ICloneFactory.cs, ReflectionCloner.cs) provided by Nuclex and was not created by myself, you can find it at: http://blog.nuclex-games.com/mono-dotnet/fast-deep-cloning/
    /// </summary>
    class SNP_Main
    {
        // maximum steps that each network will take, default value
        static int maxSteps = 50;
        // amount of repetitions each network will undergo to generate an output list
        static int stepRepetition = 50;
        // population size of the Genetic Algorithm
        static int populationSize = 4;
        // Percentage chance for mutation
        static float mutationRate = 0.1f;
        // max amount of generations 
        static int maximumGenerations = 125;
        // how many times the best fitness will be tested before concluding the test
        static int testBestFitness = 5;
        // maximum size of each spike grouping in the random new gene
        static private int maxExpSize = 4;
        // set of numbers that I expect to see after the evolution
        static private List<int> expectedSet = new List<int>() {1,2,3,4,5,6,7,8,9};
        // use the extended rule set
        static private bool experimentalRules = true;
        // accepted rules
        static private List<string> acceptedRegex = new List<string>()
        {
            "x" // direct match
        };
        // extended list of accepted rules
        static private List<string> acceptedRegexExperimental = new List<string>() {
            "x", // direct match 
            "x+", // x followed by one or more
            "x*", // x followed by zero or more
            "x?", // x followed by zero or one 
            "x(y)+", // x followed by one or more y groupings
            "x(y)*", // x followed by zero or more y groupings
            "x(y)?", // x followed by zero or one y groupings
        };
        // List of options for the main menu
        static string[] options = new string[8] {
            "1. Change the configuration",
            "2. Evolve a natural numbers network",
            "3. Evolve an even numbers network",
            "4. Run natural numbers network",
            "5. Run evens network",
            "6. Evolve experimental network",
            "7. Import Network from file",
            "8. Exit"
        };
        // will always keep the 4 best networks 
        static private int elitism = 1;
        private static GeneticAlgorithm ga;
        // seed the random func with the current time
        static private Random random = new Random((int)DateTime.Now.Ticks);
        static private bool active = true;

        static void Main(string[] args)
        {
            // slightly expand the console
            Console.SetWindowSize(Console.WindowWidth, Console.WindowHeight + 5);
            Menu();
            Console.WriteLine("Thanks for testing! :)");
            Console.ReadLine();
        }


        // based on https://stackoverflow.com/questions/46908148/controlling-menu-with-the-arrow-keys-and-enter
        /// <summary>
        /// Simple arrow controlled menu with the option to display a message above the choices.
        /// </summary>
        /// <param name="cancel">Should the menu accept the ESC key to go back</param>
        /// <param name="message">Message to display on top of the menu</param>
        /// <param name="options">The options the menu will go display</param>
        /// <returns>integer value of option chosen </returns>
        static int MultipleChoiceMenuDisplay(bool cancel, string message, params string[] options)
        {

            const int startX = 4;
            const int startY = 21;
            const int optionsPerLine = 1;
            const int spacingPerLine = 2;
            int maxLength = 0;
            for (int i = 0; i < options.Length; i++)
            {
                maxLength = (options[i].Length > maxLength) ? options[i].Length + 4 : maxLength;
            }
            int currentSelection = 0;

            ConsoleKey key;

            Console.CursorVisible = false;

            do
            {
                Console.Clear();
                Splash();
                ConfigurationDisplay();
                Console.WriteLine("  " + message);
                Console.WriteLine(new String(' ', startX) + new String('-', maxLength));
                for (int i = 0; i < options.Length; i++)
                {
                    Console.SetCursorPosition(startX + (i % optionsPerLine) * spacingPerLine, startY + i / optionsPerLine);
                    if (i == currentSelection)
                    {
                        Console.BackgroundColor = ConsoleColor.DarkCyan;
                    }
                    Console.Write("| " + options[i] + new String(' ', maxLength - options[i].Length - 4) + " |\n");
                    Console.ResetColor();
                }
                Console.WriteLine(new String(' ', startX) + new String('-', maxLength));
                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                        {
                            if (currentSelection % optionsPerLine > 0)
                                currentSelection--;
                            break;
                        }
                    case ConsoleKey.RightArrow:
                        {
                            if (currentSelection % optionsPerLine < optionsPerLine - 1)
                                currentSelection++;
                            break;
                        }
                    case ConsoleKey.UpArrow:
                        {
                            if (currentSelection >= optionsPerLine)
                                currentSelection -= optionsPerLine;
                            break;
                        }
                    case ConsoleKey.DownArrow:
                        {
                            if (currentSelection + optionsPerLine < options.Length)
                                currentSelection += optionsPerLine;
                            break;
                        }
                    case ConsoleKey.Escape:
                        {
                            if (cancel)
                                return -1;
                            break;
                        }
                }
            } while (key != ConsoleKey.Enter);

            Console.CursorVisible = true;

            return currentSelection;
        }

        /// <summary>
        /// Display the current set configuration that will run
        /// </summary>
        static void ConfigurationDisplay()
        {
            Console.WriteLine(" Current Configuration:");
            Console.Write(" Enabled Experimental Rules : ");
            if (experimentalRules)
            {
                SingleColour(ConsoleColor.Green, experimentalRules);
            }
            else
            {
                SingleColour(ConsoleColor.Red, experimentalRules);
            }
            Console.Write("; Max Steps per network: ");
            SingleColour(ConsoleColor.Cyan, maxSteps);
            Console.Write("; Step-Through amount per network: ");
            SingleColour(ConsoleColor.Cyan, stepRepetition);
            Console.Write(";\n Genetic Algorithm Population Size: ");
            SingleColour(ConsoleColor.Cyan, populationSize);
            Console.Write("; Mutation Rate: ");
            SingleColour(ConsoleColor.Cyan, mutationRate);
            Console.Write("; Maximum Number of Generations: ");
            SingleColour(ConsoleColor.Cyan, maximumGenerations);
            Console.Write(";\n Expected Set: {");
            expectedSet.ForEach(x => Console.Write("{0}\t", x));
            Console.Write("}\n\n");
        }

        /// <summary>
        /// Change the colour of a single string to appear next
        /// </summary>
        /// <param name="color">Colour of the string</param>
        /// <param name="word">The string to display</param>
        static void SingleColour(ConsoleColor color, object word)
        {
            Console.ForegroundColor = color;
            Console.Write(word);
            Console.ResetColor();
        }

        /// <summary>
        /// ASCII Art for the menu
        /// </summary>
        static void Splash()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  _____ _   _  ______   _____           _                     ");
            Console.WriteLine(" /  ___| \\ | | | ___ \\ /  ___|         | |                    ");
            Console.WriteLine(" \\ `--.|  \\| | | |_/ / \\ `--. _   _ ___| |_ ___ _ __ ___  ___ ");
            Console.WriteLine("  `--. \\ . ` | |  __/   `--. \\ | | / __| __/ _ \\ '_ ` _ \\/ __|");
            Console.WriteLine(" /\\__/ / |\\  | | |     /\\__/ / |_| \\__ \\ ||  __/ | | | | \\__ \\");
            Console.WriteLine(" \\____/\\_| \\_/ \\_|     \\____/ \\__, |___/\\__\\___|_| |_| |_|___/");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("  _____         _           _");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  ._/ |\n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(" |   __|_ _ ___| |_ _ ___ _| |");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(".|___/ \n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(" |   __| | | . | | | | -_| . |");
            Console.WriteLine(" |_____|\\_/|___|_|\\_/|___|___|\n\n");
            Console.ResetColor();
            Console.WriteLine(" Please select an option from the menu below using your arrow and enter keys:\n");
        }

        /// <summary>
        /// Main menu code, large switch statement that will execute the code accordingly. 
        /// </summary>
        /// <param name="defaultChoice">Should the menu display another sub menu</param>
        /// <param name="configChoice">Should the menu display a sub menu of a sub menu</param>
        /// <param name="defaultNum">Which menu</param>
        /// <param name="defaultConfig">Which sub-menu</param>
        static void Menu(bool defaultChoice = false, bool configChoice = false, int defaultNum = 0, int defaultConfig = 0)
        {
            int choice;
            int config;
            if (defaultChoice)
            {
                choice = defaultNum;
            }
            else
            {
                choice = MultipleChoiceMenuDisplay(false, "", options);
            }
            switch (choice)
            {
                // Configuration 
                case 0:
                    if (configChoice)
                    {
                        config = defaultConfig;
                    }
                    else
                    {
                        config = MultipleChoiceMenuDisplay(true, "", "Max Steps", "Step-Through Amount", "GA Population Size", "Mutation Rate", "Max Generations", "Expected Set", "Experimental Rules", "Default Config");
                        if (config == -1)
                        {
                            Menu();
                        }
                    }
                    switch (config)
                    {
                        // Max Steps
                        case 0:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the maximum steps amount, or press ESC to return to the last menu: ");
                            Console.Write("Enter Number: ");
                            int steps;
                            if (int.TryParse(readLineWithCancel(false, 0), out steps))
                            {
                                maxSteps = steps;
                            }
                            else
                            {
                                Console.WriteLine("\nNumber was not an integer, please try press enter to try again. ");
                                Console.ReadLine();
                                Menu(true, true, 0, 0);
                            }
                            Menu();
                            break;
                        // Step-through amount 
                        case 1:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the step-through amount, or press ESC to return to the last menu: ");
                            Console.Write("Enter Number: ");
                            int st;
                            if (int.TryParse(readLineWithCancel(false, 0), out st))
                            {
                                stepRepetition = st;
                            }
                            else
                            {
                                Console.WriteLine("\nNumber was not an integer, please try press enter to try again. ");
                                Console.ReadLine();
                                Menu(true, true, 0, 1);
                            }
                            Menu();
                            break;
                        // GA Population size
                        case 2:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the GA Population size, or press ESC to return to the last menu: ");
                            Console.Write("Enter Number: ");
                            int gaSize;
                            if (int.TryParse(readLineWithCancel(false, 0), out gaSize))
                            {
                                populationSize = gaSize;
                            }
                            else
                            {
                                Console.WriteLine("\nNumber was not an integer, please try press enter to try again. ");
                                Console.ReadLine();
                                Menu(true, true, 0, 2);
                            }
                            Menu();
                            break;
                        // Mutation rate
                        case 3:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the mutation rate, or press ESC to return to the last menu: ");
                            Console.Write("Enter float: ");
                            float mutation;
                            if (float.TryParse(readLineWithCancel(false, 0), out mutation))
                            {
                                mutationRate = mutation;
                            }
                            else
                            {
                                Console.WriteLine("\nNumber was not a float, please try press enter to try again. ");
                                Console.ReadLine();
                                Menu(true, true, 0, 3);
                            }
                            Menu();
                            break;
                        // Max Generations
                        case 4:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the maximum generations amount, or press ESC to return to the last menu: ");
                            Console.Write("Enter Number: ");
                            int maxGen;
                            if (int.TryParse(readLineWithCancel(false, 0), out maxGen))
                            {
                                maximumGenerations = maxGen;
                            }
                            else
                            {
                                Console.WriteLine("\nNumber was not an integer, please try press enter to try again. ");
                                Console.ReadLine();
                                Menu(true, true, 0, 4);
                            }
                            Menu();
                            break;
                        // Expected Set
                        case 5:
                            Console.Clear();
                            Splash();
                            Console.WriteLine("Please provide the list of integers separated by a comma that you wish to use as your expected set, or press ESC to return to the last menu: ");
                            Console.Write("Enter Set: ");
                            string result = readLineWithCancel(false, 0);
                            List<int> list = new List<int>();
                            list = result.Split(',')
                                        .Select(s =>
                                         {
                                             int i;
                                             return Int32.TryParse(s, out i) ? i : -1;
                                         }).ToList();
                            foreach (int i in list)
                            {
                                if (i == -1)
                                {
                                    Console.WriteLine("\nA Number in the set was not an integer, please try press enter to try again. ");
                                    Console.ReadLine();
                                    expectedSet = new List<int>() { 2, 4, 6, 8, 10, 12, 14, 16 };
                                    Menu(true, true, 0, 5);
                                }
                            }
                            expectedSet = list;
                            Menu();
                            break;
                        // Experimental Rules
                        case 6:
                            int expChoice = MultipleChoiceMenuDisplay(true, "Please select which rule setting you would like to use: ", "DISABLED", "ENABLED");
                            if (expChoice == -1)
                            {
                                Menu(true, false);
                            }
                            else
                            {
                                experimentalRules = (expChoice == 1);
                            }
                            Menu();
                            break;
                        // Default configs
                        case 7:
                            int defaults = MultipleChoiceMenuDisplay(true, "Please confirm whether to load default values for the configuration: ", "YES", "NO");
                            if (defaults == -1)
                            {
                                Menu(true, false);
                            }
                            else
                            {
                                if (defaults == 0)
                                {
                                    LoadDefaultConfiguration();
                                }
                            }
                            Menu();
                            break;
                        default:
                            Menu();
                            break;
                    }
                    break;
                // Evolve the natural numbers network
                case 1:
                    Console.Clear();
                    long time = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                    string normFileName = "/" + time + "/NatNumsNet.json";
                    Console.WriteLine("The File will be saved to: {0}/{1}", Directory.GetCurrentDirectory(), normFileName);
                    Console.WriteLine("Press the enter key to carry out this test.");
                    Console.ReadLine();
                    Console.WriteLine("---------- Evolving a network based on the Natural Numbers Spiking Neural P System ----------");
                    ga = new GeneticAlgorithm(populationSize, random, CreateNewRandomNormalNetwork, GenerateRandomExpression, (experimentalRules) ? acceptedRegexExperimental : acceptedRegex, FitnessFunction, elitism, mutationRate);
                    UpdateGA(ga);
                    Utils.CreateFolder(time.ToString());
                    string normCSVFile = "/" + time + "/NatNumsNet.csv";
                    Utils.SaveCSV(ga.GenerationList, normCSVFile);
                    Utils.SaveNetwork(ga.BestGenes, normFileName);
                    break;
                // Evolve the Even numbers network
                case 2:
                    Console.Clear();
                    long evensTime = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                    string evensFileName = "/" + evensTime + "/EvenNumsNet.json";
                    Console.WriteLine("The File will be saved to: {0}/{1}", Directory.GetCurrentDirectory(), evensFileName);
                    Console.WriteLine("Press the enter key to carry out this test.");
                    Console.ReadLine();
                    Console.WriteLine("---------- Evolving a network based on the Evens Spiking Neural P System ----------");
                    ga = new GeneticAlgorithm(populationSize, random, CreateNewRandomEvensNetwork, GenerateRandomExpression, (experimentalRules) ? acceptedRegexExperimental : acceptedRegex, FitnessFunction, elitism, mutationRate);
                    UpdateGA(ga);
                    Utils.CreateFolder(evensTime.ToString());
                    string evensCSVFile = "/" + evensTime + "/EvenNumsNet.csv";
                    Utils.SaveCSV(ga.GenerationList, evensCSVFile);
                    Utils.SaveNetwork(ga.BestGenes, evensFileName);
                    break;
                // Standard Nat Numbers
                case 3:
                    Console.Clear();
                    Console.WriteLine("---------- Running a standard test to get an output from a Natural Numbers Network ----------");
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    List<int> currentOutputs = SNPRun(CreateNaturalNumbersNetwork());
                    stopWatch.Stop();
                    Console.WriteLine("Final output set: ");
                    currentOutputs.ForEach(x => Console.Write("{0}\t", x)); ;
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit, time elapsed: " + stopWatch.Elapsed.ToString() + "s");
                    Console.ReadLine();
                    break;
                // Standard Evens
                case 4:
                    Console.Clear();
                    Console.WriteLine("---------- Running a standard test to get an output from an Even Numbers Network ----------");
                    Stopwatch evenStopWatch = new Stopwatch();
                    evenStopWatch.Start();
                    List<int> evenOutputs = SNPRun(CreateEvenNumbersNetwork());
                    evenStopWatch.Stop();
                    Console.WriteLine("Final output set: ");
                    evenOutputs.ForEach(x => Console.Write("{0}\t", x)); ;
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit, time elapsed: " + evenStopWatch.Elapsed.ToString() + "s");
                    Console.ReadLine();
                    break;
                // experimental network
                case 5:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine("!!! These features are experimental and will not provide meaningful results, are you sure you wish to continue? !!!");
                    Console.ResetColor();
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.Clear();
                        string expFileName = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) + "ExpNet.json";
                        Console.WriteLine("The File will be saved to: {0}/{1}.json", Directory.GetCurrentDirectory(), expFileName);
                        Console.WriteLine("Press the enter key to carry out this test.");
                        Console.ReadLine();
                        Console.WriteLine("---------- Evolving a network based on the Evens Spiking Neural P System ----------");
                        ga = new GeneticAlgorithm(populationSize, random, GenerateNewRandomNetwork, GenerateRandomExpression, (experimentalRules) ? acceptedRegexExperimental : acceptedRegex, FitnessFunction, elitism, mutationRate);
                        string expCSVFile = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) + "ExpNet.csv";
                        Utils.SaveCSV(ga.GenerationList, expCSVFile);
                    }
                    else
                    {
                        Menu();
                        break;
                    }
                    break;
                // import network
                case 6:
                    Console.Clear();
                    Splash();
                    Console.WriteLine("Please enter a filename to import from, or press the ESC key to return to the last menu: ");
                    Console.Write("Enter Filename (WITH the extension): ");
                    string import = readLineWithCancel(true, 6);
                    SNP_Network importedNetwork = Utils.ReadNetworkFromFile(import);
                    if (importedNetwork == null)
                    {
                        Console.WriteLine("Filepath was wrong. Press Enter to try again. ");
                        Console.ReadLine();
                        Menu(true, false, 6);
                    }
                    else
                    {
                        Console.WriteLine("\n");
                        importedNetwork.minifiedPrint();
                        Stopwatch importStopWatch = new Stopwatch();
                        importStopWatch.Start();
                        List<int> importOutputs = SNPRun(importedNetwork);
                        importStopWatch.Stop();
                        Console.WriteLine("Final output set: ");
                        importOutputs.ForEach(x => Console.Write("{0}\t", x)); ;
                        Console.WriteLine();
                        Console.WriteLine("Press any key to exit, time elapsed: " + importStopWatch.Elapsed.ToString() + "s");
                        Console.ReadLine();
                    }
                    break;
                // quit
                case 7:
                    Console.Clear();
                    Splash();
                    int exit = MultipleChoiceMenuDisplay(true, "Are you sure you wish to quit?", "YES", "NO");
                    if (exit == -1)
                    {
                        Menu();
                    }
                    else
                    {
                        if (exit == 0)
                        {
                            Environment.Exit(0);
                        }
                        else
                        {
                            Menu();
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Check if string is alphanumeric
        /// </summary>
        /// <param name="strToCheck">String to check</param>
        /// <returns>True or false</returns>
        public static Boolean isAlphaNumeric(string strToCheck)
        {
            Regex rg = new Regex(@"^[a-zA-Z0-9\s.,]*$");
            return rg.IsMatch(strToCheck);
        }

        /// <summary>
        /// Console.ReadLine() but you can press esc and return to the last menu
        /// </summary>
        /// <param name="main">return to menu</param>
        /// <param name="moveToMenu">return to submenu</param>
        /// <returns>string after ENTER is pressed</returns>
        private static string readLineWithCancel(bool main = false, int moveToMenu = 0)
        {
            string result = null;

            StringBuilder buffer = new StringBuilder();

            //The key is read passing true for the intercept argument to prevent
            //any characters from displaying when the Escape key is pressed.
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter && info.Key != ConsoleKey.Escape)
            {
                if (info.Key != ConsoleKey.Backspace && isAlphaNumeric(info.KeyChar.ToString()))
                {
                    Console.Write(info.KeyChar);
                    buffer.Append(info.KeyChar);
                    info = Console.ReadKey(true);
                }
                else
                {
                    if (buffer.Length != 0)
                    {
                        buffer.Length--;
                        Console.Write("\b \b");
                    }
                    else
                    {
                        buffer.Length = 0;
                    }
                    info = Console.ReadKey(true);
                }

            }
            if (info.Key == ConsoleKey.Escape)
            {
                if (main)
                {
                    Menu(false, false);
                }
                else
                {
                    Menu(true, false, moveToMenu);
                }
            }

            if (info.Key == ConsoleKey.Enter)
            {
                result = buffer.ToString();
            }

            return result;
        }

        /// <summary>
        /// This will loop through the given networks until the repetitions provided have been executed, then it will compile the full output
        /// </summary>
        /// <param name="network">SNP Network</param>
        /// <returns>List of integers that are the output for that network</returns>
        static List<int> SNPRun(SNP_Network network)
        {
            List<int> allOutputs = new List<int>();
            SNP_Network clone = GenerateIdenticalNetwork(network);
            clone.CurrentOutput = 0;
            for (int i = 0; i < stepRepetition; i++)
            {
                clone = GenerateIdenticalNetwork(network);
                while (clone.IsClear == false && clone.GlobalTimer < maxSteps)
                {
                    stepThrough(clone);
                }
                allOutputs.AddRange(clone.OutputSet);
                if (allOutputs.Count == 0 && (i > 5))
                {
                    //Console.WriteLine("No outputs provided");
                    break;
                }
            }
            //allOutputs = allOutputs.Distinct().ToList();
            allOutputs.Sort();
            return allOutputs;
        }


        // Utilised by SNPRun, does a single repetition of the network execution
        static void stepThrough(SNP_Network network)
        {
            network.Spike(network);
        }
        // Create new network with the List of new rules.
        // Note that this is a NON-GENERIC way of implementing this method, as I know exactly how many neurons this network will have.

        /// <summary>
        /// Generate random regular expressions based on a list of templates
        /// </summary>
        /// <param name="templates">List of templates</param>
        /// <param name="random">Random()</param>
        /// <returns>Returns a randomly generated regular expression based on the constraints</returns>
        static string GenerateRandomExpression(List<string> templates, Random random)
        {
            int templateIndex = random.Next(0, templates.Count);
            string randomSpikeAmount = new string('a', random.Next(1, maxExpSize));
            string randomSpikeGroupingAmount = new string('a', random.Next(1, maxExpSize));
            string generatedExpression = templates[templateIndex].Replace("x", randomSpikeAmount);
            generatedExpression = generatedExpression.Replace("y", randomSpikeGroupingAmount);
            return generatedExpression;
        }

        /// <summary>
        /// Load the default configurations for the networks
        /// </summary>
        static void LoadDefaultConfiguration()
        {
            maxSteps = 50;
            stepRepetition = 50;
            populationSize = 10;
            mutationRate = 0.1f;
            maximumGenerations = 25;
            testBestFitness = 5;
            expectedSet = new List<int>() { 2, 4, 6, 8, 10, 12, 14, 16 };
            experimentalRules = true;
        }

        /// <summary>
        /// Go through the GA and execute the networks that are being evolved.
        /// </summary>
        /// <param name="ga">The Genetic Algorithm being ran</param>
        static void UpdateGA(GeneticAlgorithm ga)
        {
            for (int i = 0; i < maximumGenerations; i++)
            {
                Console.WriteLine("Running Generation {0}", i);
                ga.NewGeneration();
                //ga.NewGeneration((populationSize / 10));
                if (i > 0)
                {
                    ga.GenerationList.Add(ReflectionCloner.DeepFieldClone(ga.FitnessList));
                }
                ga.FitnessList.Clear();
                if (active == false)
                {
                    break;
                }
        
            }
        }
        /// <summary>
        /// Once a best network has been found, the fitness needs to be tested to ensure that it lives up to the standard
        /// </summary>
        /// <param name="bestNetwork">the network being tested</param>
        /// <param name="bestGAFitness">compare it against the best fitness</param>
        /// <returns></returns>
        static bool TestBestNetwork(SNP_Network bestNetwork, float bestGAFitness)
        {
            float bestFitness = 0;
            int count = 0;
            for (int i = 0; i <= testBestFitness; i++)
            {
                float currentFitness = TestFitnessFunction(SNPRun(bestNetwork));
                if (currentFitness > bestFitness)
                {
                    bestFitness = currentFitness;
                    count++;
                }
            }
            return (bestFitness >= bestGAFitness && count == testBestFitness);
        }


        /// <summary>
        /// Fitness Function, will go through and work out the sensitivity, precision and combine them for a general fitness of the network
        /// </summary>
        /// <param name="index">Index of the network in the population of the genetic algorithm</param>
        /// <returns></returns>
        private static float FitnessFunction(int index)
        {
            /* If a number is in output and in target then it is a true positive
             * If a number is in output but not in target then it is a false positive 
             * If a number is not in output but is in the target then it is a false negative
             * If a number is not in output and not in the target then it is a true negative
             * 
             * We will never deal in True Negative members as the numbers will always be in the target
             * 
             * Sensitivity = number of true positives / number of true positives + number of false negatives
             * 
             * Specificity = number of true negatives / number of true negatives + number false positives. 
             * 
             * Precision (Positive predictive value) = The number of true positives / number of true positives + false positives 
             * 
             * We will never be able to check for specificity but we can work out the sensitivity and use it for our fitness function.
             * A higher sensitivity is better, as it means the amount of false negatives is lower. 
             * We can check for sensitivity and precision.
             * 
             */
            float tp = 0, fp = 0;
            DNA dna = ga.Population[index];
            List<int> output = SNPRun(dna.Genes);
            List<int> tpFound = new List<int>();
            for (int i = 0; i < output.Count(); i++)
            {
                foreach (int value in expectedSet)
                {
                    if (expectedSet.Contains(output[i]))
                    {
                        tp++;
                        if (!tpFound.Contains(output[i]))
                        {
                            tpFound.Add(output[i]);
                        }
                    }
                    else
                    {
                        fp++;
                    }
                }
            }
            if (output.Count > 0)
            {
                // normalize the data
                tp = (tp > expectedSet.Count) ? (tp - expectedSet.Count) / (output.Count - expectedSet.Count) : 0;
                fp = (fp > expectedSet.Count) ? (fp - expectedSet.Count) / (output.Count - expectedSet.Count) : 0;
                float scaledFitness = 0, targetCount = tpFound.Count, tpCount = expectedSet.Count;
                if (tpFound.Count != 0)
                {
                    scaledFitness = targetCount / tpCount;
                }
                float fn = expectedSet.Except(output).Count();
                float sensitivity = (tp / (tp + fn));
                float precision = tp / (tp + fp);
                float fitness = ((2 * tp) / ((2 * tp) + fp + fn)) * scaledFitness;
                if (fitness >= 0.985 && fitness <= 1) 
                {
                    Console.WriteLine("Testing the best fitness for repeated success.");
                    if (TestBestNetwork(ga.BestGenes, ga.BestFitness))
                    {
                        Console.WriteLine("Fitness over 0.985, stopping . . .");
                        active = false;
                    }
                }
                return fitness;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Generatate a completely identical network
        /// </summary>
        /// <param name="network">Network to clone</param>
        /// <returns>Identical network as a new object</returns>
        private static SNP_Network GenerateIdenticalNetwork(SNP_Network network)
        {
            int neuronAmount = network.Neurons.Count;
            int outputNeuron = 0;
            for (int i = 0; i < neuronAmount; i++)
            {
                if (network.Neurons[i].IsOutput)
                {
                    outputNeuron = i;
                }
            }
            List<Neuron> neurons = new List<Neuron>();
            for (int i = 0; i < neuronAmount; i++)
            {
                int ruleAmount = network.Neurons[i].Rules.Count;
                List<Rule> rules = new List<Rule>();
                for (int j = 0; j < ruleAmount; j++)
                {
                    rules.Add(new Rule(network.Neurons[i].Rules[j].RuleExpression, network.Neurons[i].Rules[j].Delay, network.Neurons[i].Rules[j].Fire));
                }
                List<int> connections = new List<int>();
                int connectionCount = network.Neurons[i].Connections.Count;
                for (int k = 0; k < connectionCount; k++)
                {
                    connections.Add(network.Neurons[i].Connections[k]);
                }
                neurons.Add(new Neuron(rules, network.Neurons[i].SpikeCount, connections, (i == outputNeuron)));
            }
            return new SNP_Network(neurons);
        }

        /// <summary>
        /// Generate a completely random network
        /// </summary>
        /// <returns>Completely random network based on provided constraints</returns>
        private static SNP_Network GenerateNewRandomNetwork()
        {
            int neuronAmount = random.Next(2, 8);
            int outputNeuron = random.Next(1, neuronAmount + 1);
            List<Neuron> neurons = new List<Neuron>();
            for (int i = 0; i < neuronAmount; i++)
            {
                int ruleAmount = random.Next(1, 4);
                List<Rule> rules = new List<Rule>();
                for (int j = 0; j < ruleAmount; j++)
                {
                    bool next = (random.Next(0, 2) == 1);
                    rules.Add(new Rule(GenerateRandomExpression(acceptedRegex, random), random.Next(0, 2), next));
                }
                List<int> connections = new List<int>();
                for (int k = 0; k < neuronAmount; k++)
                {
                    // add at least one connection
                    if (connections.Count == 0)
                    {
                        int randomFirstConnection = random.Next(1, neuronAmount + 1);
                        while (randomFirstConnection == (i + 1))
                        {
                            randomFirstConnection = random.Next(1, neuronAmount + 1);
                        }
                        connections.Add(randomFirstConnection);
                    }
                    else if (i != k && !(connections.Contains(k + 1)))
                    {
                        if (random.Next(0, 2) == 1)
                        {
                            connections.Add(k + 1);
                        }
                    }
                }
                connections.Sort();
                neurons.Add(new Neuron(rules, new string('a', random.Next(0, maxExpSize + 1)), connections, (i + 1 == outputNeuron)));
            }
            return new SNP_Network(neurons);
        }

        /// <summary>
        /// Generate new random expressions for a network we know works, in this case, the Evens SN P system
        /// 
        /// </summary>
        /// <returns>Evens network with randomly generated rules</returns>
        private static SNP_Network CreateNewRandomEvensNetwork()
        {
            return new SNP_Network(new List<Neuron>() {
                      new Neuron(new List<Rule>(){
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                      }, "aa", new List<int>() {4}, false),
                       new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                      }, "aa", new List<int>() {5}, false),
                       new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                      }, "aa", new List<int>() {6}, false),
                        new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),1,true),
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true)
                      }, "", new List<int>() {1, 2, 3, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                      }, "", new List<int>() {1, 2, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                      }, "", new List<int>() { 3, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                          new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                      }, "aa", new List<int>() { }, true),
                  });
        }

        /// <summary>
        /// Generate new random expressions for a network we know works, in this case, the Natural Numbers SN P system
        /// </summary>
        /// <returns>Natural Numbers network with randomly generated rules</returns>
        private static SNP_Network CreateNewRandomNormalNetwork()
        {
            return new SNP_Network(new List<Neuron>() {
                   new Neuron(new List<Rule>(){
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                }, "aa", new List<int>() {2, 3, 4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                    new Rule(GenerateRandomExpression(acceptedRegex, random),1,false)
                }, "aa", new List<int>() {1,3,4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                    new Rule(GenerateRandomExpression(acceptedRegex, random),1,true)
                }, "aa", new List<int>() {1,2,4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,true),
                    new Rule(GenerateRandomExpression(acceptedRegex, random),0,false)
                }, "aa",new List<int>() { }, true),
            });
        }

        /// <summary>
        /// Natural numbers network original system with correct rules
        /// </summary>
        /// <returns></returns>
        static SNP_Network CreateNaturalNumbersNetwork()
        {
            return new SNP_Network(new List<Neuron>() {
                   new Neuron(new List<Rule>(){
                    new Rule("aa",0,true),
                    new Rule("a",0,false)
                }, "aa", new List<int>() {2, 3, 4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule("aa",0,true),
                    new Rule("a",1,false)
                }, "aa", new List<int>() {1,3,4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule("aa",0,true),
                    new Rule("aa",1,true)
                }, "aa", new List<int>() {1,2,4}, false),
                   new Neuron(new List<Rule>() {
                    new Rule("aa",0,true),
                    new Rule("aaa",0,false)
                }, "aa",new List<int>() { }, true),
            });
        }

        /// <summary>
        /// Even numbers network original system with correct rules
        /// </summary>
        /// <returns></returns>
        static SNP_Network CreateEvenNumbersNetwork()
        {
            return new SNP_Network(new List<Neuron>() {
                      new Neuron(new List<Rule>(){
                          new Rule("aa",0,true),
                          new Rule("a",0,false)
                      }, "aa", new List<int>() {4}, false),
                       new Neuron(new List<Rule>() {
                          new Rule("aa",0,true),
                          new Rule("a",0,false)
                      }, "aa", new List<int>() {5}, false),
                       new Neuron(new List<Rule>() {
                          new Rule("aa",0,true),
                          new Rule("a",0,false)
                      }, "aa", new List<int>() {6}, false),
                        new Neuron(new List<Rule>() {
                          new Rule("a",1,true),
                          new Rule("a",0,true)
                      }, "", new List<int>() {1, 2, 3, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule("a",0,true),
                      }, "", new List<int>() {1, 2, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule("a",0,true),
                      }, "", new List<int>() { 3, 7}, false),
                         new Neuron(new List<Rule>() {
                          new Rule("aa",0,true),
                          new Rule("aaa",0,false)
                      }, "aa", new List<int>() { }, true),
                  });
        }
        /*
         * Natural Numbers fully auto generated rules, delays and spiking decision
         * Didn't end up implementing this function.
         * But will keep it commented out for future development or research
         * /
        return new SNP_Network(new List<Neuron>() {
                new Neuron(new List<Rule>(){
                    new Rule(networkConfiguration[0]["Expression"], networkConfiguration[0]["Delay"], networkConfiguration[0]["Fire"]),
                    new Rule(networkConfiguration[1]["Expression"], networkConfiguration[1]["Delay"], networkConfiguration[1]["Fire"]),
             }, "aa", new List<int>() {2, 3, 4}, false),
                new Neuron(new List<Rule>() {
                    new Rule(networkConfiguration[2]["Expression"], networkConfiguration[2]["Delay"], networkConfiguration[2]["Fire"]),
                    new Rule(networkConfiguration[3]["Expression"], networkConfiguration[3]["Delay"], networkConfiguration[3]["Fire"]),
             }, "aa", new List<int>() {1,3,4}, false),
                new Neuron(new List<Rule>() {
                    new Rule(networkConfiguration[4]["Expression"], networkConfiguration[4]["Delay"], networkConfiguration[4]["Fire"]),
                    new Rule(networkConfiguration[5]["Expression"], networkConfiguration[5]["Delay"], networkConfiguration[5]["Fire"]),
             }, "aa", new List<int>() {1,2,4}, false),
                new Neuron(new List<Rule>() {
                    new Rule(networkConfiguration[6]["Expression"], networkConfiguration[6]["Delay"], networkConfiguration[6]["Fire"]),
                    new Rule(networkConfiguration[7]["Expression"], networkConfiguration[7]["Delay"], networkConfiguration[7]["Fire"]),
             }, "aa",new List<int>() { }, true),
         }); */

        /// <summary>
        /// Testing fitness for non-ga testing (when a network reaches > 0.985 fitness)
        /// </summary>
        /// <param name="output">List of outputs that the network provides</param>
        /// <returns>Fitness</returns>
        private static float TestFitnessFunction(List<int> output)
        {
            float tp = 0, fp = 0;
            List<int> tpFound = new List<int>();
            for (int i = 0; i < output.Count(); i++)
            {
                foreach (int value in expectedSet)
                {
                    if (expectedSet.Contains(output[i]))
                    {
                        tp++;
                        if (!tpFound.Contains(output[i]))
                        {
                            tpFound.Add(output[i]);
                        }
                    }
                    else
                    {
                        fp++;
                    }
                }
            }
            if (output.Count > 0)
            {
                // normalize the data
                tp = (tp - expectedSet.Count) / (output.Count - expectedSet.Count);
                fp = (fp > expectedSet.Count) ? (fp - expectedSet.Count) / (output.Count - expectedSet.Count) : 0;
                float scaledFitness = 0, targetCount = tpFound.Count, tpCount = expectedSet.Count;
                if (tpFound.Count != 0)
                {
                    scaledFitness = targetCount / tpCount;
                }
                float fn = expectedSet.Except(output).Count();
                float sensitivity = (tp / (tp + fn));
                float precision = tp / (tp + fp);
                float fitness = ((2 * tp) / ((2 * tp) + fp + fn)) * scaledFitness;
                output = output.Distinct().ToList();
                output.Sort();
                return fitness;
            }
            else
            {
                // if there are no outputs then the fitness will be 0 
                return 0;
            }

        }

    }
}