using UnityModManagerNet;

namespace DvMod.HandBrake
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public string? version;

        [Draw("Add physical handbrake wheels")]
        public bool addWheels = true;
        [Draw("% of cars with handbrake set when spawned", Min = 0, Max = 100)]
        public int handbrakeSpawnPercent = 10;
        [Draw("% of cars requiring handbrake set for job completion", Min = 0, Max = 100)]
        public int handbrakeRatioRequired = 10;
        [Draw("Enable logging")]
        public bool enableLogging = false;

        override public void Save(UnityModManager.ModEntry entry)
        {
            Save(this, entry);
        }

        public void OnChange()
        {
        }
    }
}