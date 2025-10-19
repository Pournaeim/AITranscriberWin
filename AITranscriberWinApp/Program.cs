using System;
using System.Collections;
using System.Configuration;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AITranscriberWinApp
{
    internal static class Program
    {
        private const string MissingConfigurationFileMessage = "The configuration file could not be found.";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            RunApplication();
        }

        private static void RunApplication()
        {
            try
            {
                Application.Run(new MainForm());
            }
            catch (ConfigurationErrorsException configurationException)
            {
                ShowConfigurationErrorDialog(
                    configurationException,
                    "AITranscriberWin could not load its configuration settings.");
            }
        }

        internal static void ShowConfigurationErrorDialog(
            ConfigurationErrorsException configurationException,
            string? contextMessage = null)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine(contextMessage ?? "AITranscriberWin encountered a configuration error.");

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
                else if (deleteResult == MissingConfigurationFileMessage)
                {
                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine("The configuration file could not be found. If this message appears again, delete the file manually and restart the application.");
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
            var fromException = ExtractFilenameFromException(configurationException);
            if (!string.IsNullOrWhiteSpace(fromException))
            {
                return fromException;
            }

            return TryGetUserConfigurationFilePath();
        }

        private static string? TryDeleteConfigurationFile(string configurationFilePath)
        {
            try
            {
                if (!File.Exists(configurationFilePath))
                {
                    return MissingConfigurationFileMessage;
                }

                File.Delete(configurationFilePath);

                return null;
            }
            catch (Exception deleteException)
            {
                return deleteException.Message;
            }
        }

        private static string? TryGetUserConfigurationFilePath()
        {
            foreach (var level in new[]
                     {
                         ConfigurationUserLevel.PerUserRoamingAndLocal,
                         ConfigurationUserLevel.PerUserRoaming
                     })
            {
                var path = TryGetUserConfigurationFilePath(level);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static string? TryGetUserConfigurationFilePath(ConfigurationUserLevel level)
        {
            try
            {
                var configuration = ConfigurationManager.OpenExeConfiguration(level);
                if (!string.IsNullOrWhiteSpace(configuration.FilePath))
                {
                    return configuration.FilePath;
                }
            }
            catch (ConfigurationErrorsException configurationException)
            {
                var fallback = ExtractFilenameFromException(configurationException);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    return fallback;
                }
            }
            catch
            {
                // Swallow exceptions from secondary resolution attempts. We'll fall back to a generic message.
            }

            return null;
        }

        private static string? ExtractFilenameFromException(ConfigurationErrorsException configurationException)
        {
            for (var current = configurationException; current != null; current = current.InnerException as ConfigurationErrorsException)
            {
                var filename = ExtractFilename(current);
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    return filename;
                }
            }

            return null;
        }

        private static string? ExtractFilename(ConfigurationErrorsException configurationException)
        {
            if (!string.IsNullOrWhiteSpace(configurationException.Filename))
            {
                return configurationException.Filename;
            }

            if (configurationException.Data is IDictionary data)
            {
                foreach (DictionaryEntry entry in data)
                {
                    if (entry.Key is string key &&
                        key.EndsWith("filename", StringComparison.OrdinalIgnoreCase) &&
                        entry.Value is string value &&
                        !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }
}
