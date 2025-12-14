using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    public enum PlumbingBaseFamily
    {
        IPC,
        UPC,
        ASPE,
        Chicago,
        CPC
    }

    public record PlumbingProfile(
        string Id,
        string DisplayName,
        PlumbingBaseFamily BaseFamily,
        string Notes,
        double? MaxColdFps,
        double? MaxHotFps,
        string FixtureDemandKey,
        string SanitaryDfuKey,
        string VentDfuLengthKey,
        string StormLeaderKey,
        string StormHorizontalKey,
        string GasSizingKey)
    {
        public IEnumerable<string> AllTableKeys()
        {
            if (!string.IsNullOrEmpty(FixtureDemandKey)) yield return FixtureDemandKey;
            if (!string.IsNullOrEmpty(SanitaryDfuKey)) yield return SanitaryDfuKey;
            if (!string.IsNullOrEmpty(VentDfuLengthKey)) yield return VentDfuLengthKey;
            if (!string.IsNullOrEmpty(StormLeaderKey)) yield return StormLeaderKey;
            if (!string.IsNullOrEmpty(StormHorizontalKey)) yield return StormHorizontalKey;
            if (!string.IsNullOrEmpty(GasSizingKey)) yield return GasSizingKey;
        }

        public string TraceLabel => $"{DisplayName} [{Id}]";

        public double GetMaxVelocity(bool isHot)
        {
            if (isHot) return MaxHotFps ?? 0;
            return MaxColdFps ?? 0;
        }
    }
}
