using System.Collections.Generic;

namespace RTM.Ductolator.Models
{
    public record DuctFitting(string Category, string Name, double KCoefficient, double EquivalentLengthFt);
    public record PipeFitting(string Category, string Name, double KCoefficient, double EquivalentLengthFt);

    public static class FittingLibrary
    {
        public static readonly List<DuctFitting> DuctFittings = new()
        {
            new("Elbow", "Smooth radius elbow (R/D=1.5)", 0.15, 10),
            new("Elbow", "Medium radius elbow (R/D=1.0)", 0.25, 15),
            new("Elbow", "Mitered/square elbow with vanes", 0.45, 25),
            new("Elbow", "Sharp mitered elbow without vanes", 1.50, 55),
            new("Elbow", "45° elbow", 0.08, 6),
            new("Branch/Tee", "Straight-through tee", 0.60, 25),
            new("Branch/Tee", "45° side takeoff", 0.40, 18),
            new("Branch/Tee", "90° branch takeoff", 1.00, 40),
            new("Transition", "5° conical transition", 0.05, 5),
            new("Transition", "15° conical transition", 0.10, 8),
            new("Transition", "Square-to-round bell mouth", 0.04, 4)
        };

        public static readonly List<PipeFitting> PipeFittings = new()
        {
            new("Elbow", "Long-radius 90° elbow", 0.75, 30),
            new("Elbow", "Standard 90° elbow", 1.50, 50),
            new("Elbow", "45° elbow", 0.40, 15),
            new("Branch/Tee", "Straight-through tee", 0.60, 20),
            new("Branch/Tee", "Branch side of tee", 1.80, 60),
            new("Branch/Tee", "Wye (45°)", 0.75, 25),
            new("Valve", "Gate valve (open)", 0.19, 8),
            new("Valve", "Ball valve (open)", 0.05, 3),
            new("Valve", "Globe valve (open)", 10.0, 340),
            new("Coupling", "Coupling / union", 0.04, 1),
            new("Coupling", "Check valve (swing)", 2.5, 85)
        };
    }
}
