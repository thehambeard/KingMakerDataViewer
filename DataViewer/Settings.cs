using UnityModManagerNet;

namespace DataViewer
{
    public class Settings : UnityModManager.ModSettings
    {
        public int selectedTab = 0;
        public int selectedRawDataType = 0;
        public int maxRows = 20;
        public int maxSearchDepth = 3;
    }
}
