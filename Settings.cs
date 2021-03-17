using UnityModManagerNet;

namespace DvMod.HandBrake
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public string? version;

        [Draw("Add physical handbrake wheels")]
        public bool addWheels = true;
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