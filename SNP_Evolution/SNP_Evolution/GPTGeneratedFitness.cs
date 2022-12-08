private static float FitnessFunction(int index, Func<List<int>, List<int>, float> fitnessAlgorithm)
{
    DNA dna = ga.Population[index];
    List<int> output = SNPRun(dna.Genes);
    float fitness = fitnessAlgorithm(output, expectedSet);

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

private static float CalculateF1Score(List<int> output, List<int> expectedSet)
{
    if (output.Count == 0)
    {
        return 0;
    }

    int truePositives = expectedSet.Intersect(output).Count();
    int falsePositives = output.Count - truePositives;

    float sensitivity = truePositives / (float)expectedSet.Count;
    float precision = truePositives / (truePositives + falsePositives);

    return 2 * ((precision * sensitivity) / (precision + sensitivity));
}

private static float CalculateJaccardIndex(List<int> output, List<int> expectedSet)
{
    if (output.Count == 0)
    {
        return 0;
    }

    return (float)expectedSet.Intersect(output).Count() / expectedSet.Union(output).Count();
}

private static float CalculateMatthewsCorrelationCoefficient(List<int> output, List<int> expectedSet)
{
    if (output.Count == 0)
    {
        return 0;
    }

    int truePositives = expectedSet.Intersect(output).Count();
    int falsePositives = output.Count - truePositives;
    int falseNegatives = expectedSet.Count - truePositives;

    return (truePositives * truePositives - falsePositives * falseNegatives) /
        (float)(Math.Sqrt((truePositives + falsePositives) * (truePositives + falseNegatives) * (falsePositives + truePositives) * (falsePositives + falseNegatives)));
}
