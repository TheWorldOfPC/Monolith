using System.Linq;
using System;

namespace Monolith.Classes
{
    public class BootConfiguration
    {
        public string CurrentEntryName { get; set; }
        public string BootloaderPath { get; set; }
        public string TimeoutSeconds { get; set; }
        public bool MetroBootEnabled { get; set; }

        public static BootConfiguration GetBootConfiguration()
        {
            var output = Utils.RunCommand("bcdedit", "/enum");
            var config = new BootConfiguration();

            string defaultGuid = null;

            foreach (var line in output)
            {
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("default", StringComparison.OrdinalIgnoreCase))
                    defaultGuid = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim();

                else if (trimmed.StartsWith("timeout", StringComparison.OrdinalIgnoreCase))
                    config.TimeoutSeconds = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim();

                else if (trimmed.StartsWith("displaybootmenu", StringComparison.OrdinalIgnoreCase))
                    config.MetroBootEnabled = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim().ToLower() == "yes";
            }

            if (!string.IsNullOrEmpty(defaultGuid))
            {
                for (int i = 0; i < output.Count(); i++)
                {
                    if (output[i].Trim().Equals($"identifier              {defaultGuid}", StringComparison.OrdinalIgnoreCase))
                    {
                        for (int j = i; j < output.Count() && !string.IsNullOrWhiteSpace(output[j]); j++)
                        {
                            string line = output[j].Trim();

                            if (line.StartsWith("description", StringComparison.OrdinalIgnoreCase))
                                config.CurrentEntryName = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim();

                            if (line.StartsWith("path", StringComparison.OrdinalIgnoreCase))
                                config.BootloaderPath = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1].Trim();
                        }
                        break;
                    }
                }
            }

            return config;
        }
    }
}