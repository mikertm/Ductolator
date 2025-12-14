namespace RTM.Ductolator.Tests
{
    // Strong types for reference cases to keep tests readable

    public record DuctReferenceCase(
        string ScenarioName,
        double Cfm,
        double DiameterIn,
        double VelocityFpm,
        double DpPer100Ft,
        double VpInWg,
        double ExpectedDpToleranceRel = 0.05
    );

    public record PlumbingReferenceCase(
        string ScenarioName,
        double Gpm,
        double DiameterIn,
        double CFactor,
        double ExpectedPsiPer100Ft,
        double ExpectedVelocityFps
    );
}
