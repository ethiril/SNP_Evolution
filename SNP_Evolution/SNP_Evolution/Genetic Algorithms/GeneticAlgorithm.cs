using System;
using System.Collections.Generic;
using SNP_First_Test.Utilities;
using SNP_Network = SNP_First_Test.Network.Network;

namespace SNP_First_Test.Genetic_Algorithms
{
    public class GeneticAlgorithm
    {
        // full population list for the GA
        public List<DNA> Population { get; private set; }
        // which generation is currently running
        public int Generation { get; private set; }
        // Stored best current fitness for this generation
        public float BestFitness { get; private set; }
        // List of best fitnesses
        public  List<List<float>> GenerationList = new List<List<float>>();
        public List<float> FitnessList = new List<float>();
        // Best current network
        public SNP_Network BestGenes { get; private set; }
        
        // Amount of parent networks that will 100% reproduce
        public int Elitism;
        // Rate of mutation
        public float MutationRate;

        // List of new DNA for another generation
        private List<DNA> newPopulation;
        // max amount of population
        private int PopulationSize;
        // Random()
        private Random random;
        // Sum of all fitnesses in a population, used to calculate which parent to crossover
        private float fitnessSum;
        // How many networks didn't give a valid fitness (or a fitness of 0)
        private int erroneousNetworks;
        // Function to get a random new network
        private Func<SNP_Network> getRandomNetwork;
        // list of accepted rules
        private List<string> acceptedRegex;
        // Get next random rule
        private Func<List<string>, Random, string> getModifiedRule;
        // Calculate the fitness
        private Func<int, float> fitnessFunction;

        public GeneticAlgorithm(int populationSize, Random random, Func<SNP_Network> getRandomNetwork, Func<List<string>, Random, string> getModifiedRule, List<string> acceptedRegex, Func<int, float> fitnessFunction,
            int elitism, float mutationRate = 0.01f)
        {
            Generation = 1;
            erroneousNetworks = 0;
            Elitism = elitism;
            MutationRate = mutationRate;
            Population = new List<DNA>(populationSize);
            PopulationSize = populationSize;
            newPopulation = new List<DNA>(populationSize);
            this.random = random;
            this.getRandomNetwork = getRandomNetwork;
            this.getModifiedRule = getModifiedRule;
            this.acceptedRegex = acceptedRegex;
            this.fitnessFunction = fitnessFunction;
            for (int i = 0; i < populationSize; i++)
            {
                Population.Add(new DNA(random, getRandomNetwork, getModifiedRule, acceptedRegex, fitnessFunction, init: true));
            }
        }

        /// <summary>
        /// Start a new generation
        /// </summary>
        /// <param name="NewDNACount"> Amount of new random added DNA </param>
        /// <param name="crossoverNewDNA"> Should the new DNA be used in crossovers in the first generation they are included in</param>
        public void NewGeneration(int NewDNACount = 0, bool crossoverNewDNA = false)
        {
            int finalCount = Population.Count + NewDNACount;

            if (finalCount <= 0)
            {
                return;
            }
            List<DNA> tempPopulation = new List<DNA>();
            if (Generation == 1)
            {
                CalculateFitness();
                Population.Sort(CompareDNA);
                erroneousNetworks = Population.Count;
                while (erroneousNetworks > 0)
                {
                    for (int i = 0; i < Population.Count; i++)
                    {
                        // Eliminate the possibility of erroneous networks or 0 fitness networks. The networks should all at least provide a minimal fitness (produce one number out of the 
                        // target set
                        if (Population[i].Fitness <= 0 || float.IsNaN(Population[i].Fitness) || Population[i].Equals(null) || Population[i].Fitness > 1)
                        {
                            Population[i] = new DNA(random, getRandomNetwork, getModifiedRule, acceptedRegex, fitnessFunction, init: true);
                        }
                        else
                        {
                            tempPopulation.Add(Population[i]);
                            Population.Remove(Population[i]);
                            erroneousNetworks--;
                        }
                    }
                    Console.WriteLine("Removing an erroneous network and repopulating the remaining entries. Amount of networks to replace: {0}", erroneousNetworks);
                    if (erroneousNetworks == 0)
                    {
                        Population.AddRange(tempPopulation);
                        break;
                    }
                    else
                    {
                        NewGeneration();
                    }
                }
            }
            else if (Population.Count > 0)
            {
                CalculateFitness();
                Population.Sort(CompareDNA);
            }
            newPopulation.Clear();

            for (int i = 0; i < finalCount; i++)
            {
                if (i < Elitism && i < Population.Count)
                {
                    newPopulation.Add(Population[i]);
                }
                else if (i < Population.Count || crossoverNewDNA)
                {
                    DNA parent1 = ChooseParent();
                    DNA parent2 = ChooseParent();
                    DNA child = parent1.Crossover(parent2);

                    child.Mutate(MutationRate);

                    newPopulation.Add(child);
                }
                else
                {
                    newPopulation.Add(new DNA(random, getRandomNetwork, getModifiedRule, acceptedRegex, fitnessFunction, init: true));
                }
            }
            List<DNA> tmpList = Population;
            Population = newPopulation;
            newPopulation = tmpList;
            Generation++;
        }

        /// <summary>
        /// Currently unused debugging function
        /// </summary>
        private void TestFitnesses()
        {
            for (int i = 0; i < Population.Count; i++)
            {
                Console.Write("Fitness {0}: {1};", i, Population[i].Fitness);
            }
        }

        /// <summary>
        /// Compare the two provided DNA , used for sorting objects
        /// </summary>
        /// <param name="a">DNA 1</param>
        /// <param name="b">DNA 2</param>
        /// <returns>Should A be higher, above or in the same position as B</returns>
        private int CompareDNA(DNA a, DNA b)
        {
            if (a.Fitness > b.Fitness)
            {
                return -1;
            }
            else if (a.Fitness < b.Fitness)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        ///  compare the fitnesses of the two networks, save the best genes
        /// </summary>
        private void CalculateFitness()
        {
            fitnessSum = 0;
            DNA best = Population[0];

            for (int i = 0; i < Population.Count; i++)
            {
                fitnessSum += Population[i].CalculateFitness(i);
                if (Generation > 1 && Population[i].Fitness >= 0 && !float.IsNaN(Population[i].Fitness) && !Population[i].Equals(null) && Generation > 1 && Population[i].Fitness <= 1)
                {
                    Console.WriteLine("Current Fitness: {0},", Population[i].Fitness);
                    FitnessList.Add(Population[i].Fitness);
                }
                if (Population[i].Fitness >= best.Fitness && Generation > 1)
                {
                    best = Population[i];
                    best.Genes.minifiedPrint();
                    best.Genes.OutputSet.ForEach(x => Console.Write("{0}\t", x));
                    Console.WriteLine("\nCurrent best fitness: {0}", best.Fitness);
                   // FitnessList.Add(best.Fitness);
                }
            }
            BestFitness = best.Fitness;
            BestGenes = ReflectionCloner.DeepFieldClone(best.Genes);
        }

        /// <summary>
        /// Parents with a higher fitness will have a higher chance of reproducing, however all parents should have a chance.
        /// If for some reason a parent does not find a partner, select one from the top 10% of parents (rounded down)
        /// </summary>
        /// <returns>DNA of the selected parent</returns>
        private DNA ChooseParent()
        {
            double randomNumber = random.NextDouble() * fitnessSum;

            for (int i = 0; i < Population.Count; i++)
            {
                if (randomNumber < Population[i].Fitness)
                {
                    return Population[i];
                }
                randomNumber -= Population[i].Fitness;
            }
            // if the parent could not be chosen for some reason, choose randomly from the top 10% of fittest parents
            int randomParent = random.Next(0, (Convert.ToInt32(Math.Floor(Population.Count * 0.1)) + 1));
            return Population[randomParent];
        }
    }
}
