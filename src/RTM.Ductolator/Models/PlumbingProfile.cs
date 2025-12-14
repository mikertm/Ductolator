using System.Collections.Generic;
using System.Linq;

namespace RTM.Ductolator.Models
{
    public enum PlumbingProfileFamily
    {
        IPC,
        UPC,
        ASPE,
        Chicago,
        CPC
    }

    public class PlumbingProfile
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public PlumbingProfileFamily BaseFamily { get; set; }
        public string Notes { get; set; }
        public double MaxColdFps { get; set; }
        public double MaxHotFps { get; set; }

        // Table keys
        public string FixtureDemandKey { get; set; }
        public string SanitaryDfuKey { get; set; }
        public string VentSizingKey { get; set; }
        public string StormSizingKey { get; set; }
        public string GasSizingKey { get; set; }

        public double GetMaxVelocity(bool isHot) => isHot ? MaxHotFps : MaxColdFps;

        public override string ToString() => DisplayName;

        public IEnumerable<string> AllTableKeys()
        {
            if (!string.IsNullOrEmpty(FixtureDemandKey)) yield return FixtureDemandKey;
            if (!string.IsNullOrEmpty(SanitaryDfuKey)) yield return SanitaryDfuKey;
            if (!string.IsNullOrEmpty(VentSizingKey)) yield return VentSizingKey;
            if (!string.IsNullOrEmpty(StormSizingKey)) yield return StormSizingKey;
            if (!string.IsNullOrEmpty(GasSizingKey)) yield return GasSizingKey;
        }
    }
}
