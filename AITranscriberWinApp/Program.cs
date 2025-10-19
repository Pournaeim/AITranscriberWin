using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AITranscriberWinApp
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (ConfigurationErrorsException configurationException)
            {
                HandleConfigurationError(configurationException);
            }
        }

        private static void HandleConfigurationError(ConfigurationErrorsException configurationException)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("AITranscriberWin could not load its configuration settings.");

            var configurationFilePath = ResolveConfigurationFilePath(configurationException);
            if (!string.IsNullOrWhiteSpace(configurationFilePath))
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Configuration file:");
                messageBuilder.AppendLine(configurationFilePath);

                var deleteResult = TryDeleteConfigurationFile(configurationFilePath);
                if (deleteResult == null)
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("The corrupted configuration file has been deleted. Please restart the application.");
                }
                else
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("Please delete this file manually and restart the application.");
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine($"Deletion failed: {deleteResult}");
                }
            }
            else
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("Unable to determine the configuration file path automatically.");
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine("Original error:");
            messageBuilder.AppendLine(configurationException.Message);

            MessageBox.Show(
                messageBuilder.ToString(),
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static string? ResolveConfigurationFilePath(ConfigurationErrorsException configurationException)
        {
            var current = configurationException;
            while (current != null)
            {
                if (!string.IsNullOrWhiteSpace(current.Filename) && File.Exists(current.Filename))
                {
                    return current.Filename;
                }

                current = current.InnerException as ConfigurationErrorsException;
            }

            return null;
        }

        private static string? TryDeleteConfigurationFile(string configurationFilePath)
        {
            try
            {
                if (File.Exists(configurationFilePath))
                {
                    File.Delete(configurationFilePath);
                }

                return null;
            }
            catch (Exception deleteException)
            {
                return deleteException.Message;
            }
        }
    }
}
