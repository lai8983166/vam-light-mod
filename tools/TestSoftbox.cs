using System;
using LaiLighting;

class TestSoftbox
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        foreach (int grid in new int[] { 2, 3 })
        {
            double energy = 0, centerX = 0, centerY = 0, variance = 0;
            int lit = 0, count = grid * grid;
            for (int sample = 0; sample < count; sample++)
            {
                float x = QuickFillSampling.Offset(sample % grid, grid);
                float y = QuickFillSampling.Offset(sample / grid, grid);
                float weight = QuickFillSampling.Weight(grid);
                Require(Math.Abs(x) < 0.5 && Math.Abs(y) < 0.5, "Sample outside panel");
                Require(weight > 0, "Negative or zero sample energy");
                energy += weight;
                centerX += x * weight;
                centerY += y * weight;
                variance += (x * x + y * y) * weight;
                if (x > 0) lit++;
            }
            Require(Math.Abs(energy - 1) < 0.000001, "Sample count changes total key intensity");
            Require(Math.Abs(centerX) < 0.000001 && Math.Abs(centerY) < 0.000001,
                "Panel moves the weighted light center");
            Require(variance > 0, "Panel collapsed to a point");
            Require(lit > 0 && lit < count, "A half-plane blocker must partially occlude the panel");
            Console.WriteLine(grid + "x" + grid + ": energy, centroid, bounds and partial visibility passed.");
        }
    }
}
