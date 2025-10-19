using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AITranscriberWinApp.Properties;
using AITranscriberWinApp.Services;
using NAudio.Wave;

namespace AITranscriberWinApp
{
    public partial class MainForm : Form
    {
        private enum TranslationEndpointConfiguration
        {
            Disabled,
            Configured,
            Invalid
        }

        private readonly OpenAiTranscriptionService _transcriptionService = new OpenAiTranscriptionService();
        private TranslationService _translationService;
        private TranslationEndpointConfiguration _translationEndpointStatus = TranslationEndpointConfiguration.Disabled;
        private const string TranslationDisabledMessage = "Translation disabled. Provide a translation service URL in Settings to enable it.";
        private const string TranslationInvalidMessage = "Translation disabled until a valid service URL is saved.";
        private const int FileReadyMaxAttempts = 10;
        private static readonly TimeSpan FileReadyRetryDelay = TimeSpan.FromMilliseconds(200);
        private readonly string _recordingsDirectory;
        private readonly string _transcriptsDirectory;

        private WaveInEvent _waveIn;
        private WaveFileWriter _waveWriter;
        private string _currentRecordingPath;
        private bool _isRecording;
        private CancellationTokenSource _processingCts;

        public MainForm()
        {
            InitializeComponent();
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var appRoot = Path.Combine(documentsPath, "AITranscriberWin");
            _recordingsDirectory = Path.Combine(appRoot, "Recordings");
            _transcriptsDirectory = Path.Combine(appRoot, "Transcripts");
            Directory.CreateDirectory(_recordingsDirectory);
            Directory.CreateDirectory(_transcriptsDirectory);
            txtApiKey.Text = Settings.Default.OpenAIApiKey;
            var savedEndpoint = Settings.Default.TranslationEndpoint;
            txtTranslationEndpoint.Text = savedEndpoint;
            var endpointStatus = ApplyTranslationEndpoint(savedEndpoint, showFeedback: false);
            string statusMessage;
            string translationHint = string.Empty;

            switch (endpointStatus)
            {
                case TranslationEndpointConfiguration.Configured:
                    statusMessage = "Ready.";
                    break;
                case TranslationEndpointConfiguration.Disabled:
                    statusMessage = "Ready (translation disabled).";
                    translationHint = TranslationDisabledMessage;
                    break;
                case TranslationEndpointConfiguration.Invalid:
                    statusMessage = "Ready (translation disabled: invalid translation URL).";
                    translationHint = TranslationInvalidMessage;
                    break;
                default:
                    statusMessage = "Ready.";
                    break;
            }

            UpdateStatus(statusMessage);

            if (!string.IsNullOrEmpty(translationHint))
            {
                txtTranslation.Text = translationHint;
            }
        }

        private void btnSaveKey_Click(object sender, EventArgs e)
        {
            var apiKey = txtApiKey.Text.Trim();
            var endpointText = txtTranslationEndpoint.Text?.Trim() ?? string.Empty;
            txtTranslationEndpoint.Text = endpointText;

            var endpointStatus = ApplyTranslationEndpoint(endpointText, showFeedback: true);
            if (endpointStatus == TranslationEndpointConfiguration.Invalid)
            {
                txtTranslation.Text = TranslationInvalidMessage;
                return;
            }

            Settings.Default.OpenAIApiKey = apiKey;
            Settings.Default.TranslationEndpoint = endpointText;

            try
            {
                Settings.Default.Save();
            }
            catch (ConfigurationErrorsException configurationException)
            {
                Program.ShowConfigurationErrorDialog(
                    configurationException,
                    "AITranscriberWin could not save its configuration settings.");

                try
                {
                    Settings.Default.Reload();
                    txtApiKey.Text = Settings.Default.OpenAIApiKey;
                    var reloadedEndpoint = Settings.Default.TranslationEndpoint;
                    txtTranslationEndpoint.Text = reloadedEndpoint;
                    ApplyTranslationEndpoint(reloadedEndpoint, showFeedback: false);
                }
                catch
                {
                    // If reloading still fails we keep the current in-memory values so the user can adjust them.
                }

                return;
            }

            var message = endpointStatus == TranslationEndpointConfiguration.Configured
                ? "Settings saved. Translation is enabled."
                : "Settings saved. Translation is disabled.";

            MessageBox.Show(message, "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (!_isRecording && _processingCts == null)
            {
                UpdateStatus(endpointStatus == TranslationEndpointConfiguration.Configured
                    ? "Ready."
                    : "Ready (translation disabled)."
                );
            }

            if (endpointStatus == TranslationEndpointConfiguration.Configured)
            {
                if (txtTranslation.Text == TranslationDisabledMessage || txtTranslation.Text == TranslationInvalidMessage)
                {
                    txtTranslation.Clear();
                }
            }
            else if (endpointStatus == TranslationEndpointConfiguration.Disabled)
            {
                txtTranslation.Text = TranslationDisabledMessage;
            }
            else
            {
                txtTranslation.Text = TranslationInvalidMessage;
            }
        }

        private void btnToggleRecording_Click(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            try
            {
                _currentRecordingPath = Path.Combine(_recordingsDirectory, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                _waveWriter = new WaveFileWriter(_currentRecordingPath, _waveIn.WaveFormat);
                _waveIn.StartRecording();
                _isRecording = true;
                btnToggleRecording.Text = "Stop Recording";
                UpdateStatus("Recording...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to start recording. {ex.Message}", "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CleanupRecordingResources();
            }
        }

        private void StopRecording()
        {
            try
            {
                _waveIn?.StopRecording();
                UpdateStatus("Processing audio...");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to stop recording. {ex.Message}", "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            _waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            _waveWriter?.Flush();
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            CleanupRecordingResources();
            var audioPath = _currentRecordingPath;
            _isRecording = false;

            if (e.Exception != null)
            {
                MessageBox.Show($"Recording stopped with an error: {e.Exception.Message}", "Recording Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            BeginInvoke(new Action(async () =>
            {
                btnToggleRecording.Text = "Start Recording";
                btnToggleRecording.Enabled = false;
                await ProcessRecordingAsync(audioPath);
                btnToggleRecording.Enabled = true;
            }));
        }

        private async Task ProcessRecordingAsync(string audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                UpdateStatus("No audio file found.");
                return;
            }

            var apiKey = GetApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                UpdateStatus("Recording saved. Provide an API key to transcribe.");
                MessageBox.Show("Recording saved successfully. Enter your OpenAI API key and click \"Transcribe Audio File...\" to process the recording.", "API Key Needed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            txtTranscript.Clear();
            txtTranslation.Clear();

            _processingCts?.Dispose();
            _processingCts = new CancellationTokenSource();
            var token = _processingCts.Token;

            try
            {
                if (!await WaitForFileReadyAsync(audioPath, token))
                {
                    UpdateStatus("Audio file unavailable.");
                    MessageBox.Show("The recorded audio file is not accessible yet. Please try recording again.", "Audio File Busy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var fileInfo = new FileInfo(audioPath);
                if (!fileInfo.Exists || fileInfo.Length < 100)
                {
                    UpdateStatus("Audio file is empty.");
                    MessageBox.Show("The recorded audio appears to be empty. Please ensure your microphone is working and try again.", "Empty Audio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                UpdateStatus("Uploading to Whisper...");
                var transcription = await _transcriptionService.TranscribeAsync(audioPath, apiKey, token);
                txtTranscript.Text = transcription.Text;

                string completionStatus;

                if (_translationService == null)
                {
                    transcription.Translation = string.Empty;
                    var disabledMessage = _translationEndpointStatus == TranslationEndpointConfiguration.Invalid
                        ? TranslationInvalidMessage
                        : TranslationDisabledMessage;
                    txtTranslation.Text = disabledMessage;
                    completionStatus = _translationEndpointStatus == TranslationEndpointConfiguration.Invalid
                        ? "Completed (translation disabled: invalid translation URL)."
                        : "Completed (translation disabled).";
                }
                else
                {
                    UpdateStatus("Translating to Persian...");
                    txtTranslation.Text = "Translating...";

                    try
                    {
                        var translation = await _translationService.TranslateToPersianAsync(transcription.Text, token);
                        transcription.Translation = translation;
                        txtTranslation.Text = string.IsNullOrWhiteSpace(translation)
                            ? "[No translation returned]"
                            : translation;
                        completionStatus = string.IsNullOrWhiteSpace(translation)
                            ? "Completed (translation unavailable)."
                            : "Completed.";
                    }
                    catch (Exception translateError)
                    {
                        transcription.Translation = string.Empty;
                        txtTranslation.Text = "Translation failed.";
                        completionStatus = "Completed (translation unavailable).";
                        MessageBox.Show($"Translation failed: {translateError.Message}", "Translation Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }

                SaveTranscript(audioPath, transcription);
                UpdateStatus(completionStatus);
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Operation cancelled.");
            }
            catch (Exception ex)
            {
                UpdateStatus("Failed.");
                MessageBox.Show($"Processing failed: {ex.Message}", "Processing Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _processingCts.Dispose();
                _processingCts = null;
                _currentRecordingPath = null;
            }
        }

        private async Task<bool> WaitForFileReadyAsync(string audioPath, CancellationToken token)
        {
            for (var attempt = 0; attempt < FileReadyMaxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    using (File.Open(audioPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        return true;
                    }
                }
                catch (IOException)
                {
                    // File is still being written to disk. Retry shortly.
                }
                catch (UnauthorizedAccessException)
                {
                    // Some antivirus tools may keep the file locked briefly. Retry.
                }

                await Task.Delay(FileReadyRetryDelay, token);
            }

            return false;
        }

        private void SaveTranscript(string audioPath, TranscriptionResult transcription)
        {
            try
            {
                var baseFileName = Path.GetFileNameWithoutExtension(audioPath);
                var textOutputPath = Path.Combine(_transcriptsDirectory, $"{baseFileName}.txt");
                var markdownOutputPath = Path.Combine(_transcriptsDirectory, $"{baseFileName}.md");

                Directory.CreateDirectory(_transcriptsDirectory);

                var builder = new StringBuilder();
                builder.AppendLine($"Recorded File: {audioPath}");
                builder.AppendLine($"Generated On: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine(new string('-', 60));
                builder.AppendLine("English Transcript:");
                builder.AppendLine(transcription.Text);
                builder.AppendLine();
                builder.AppendLine("Persian Translation:");
                var translationText = string.IsNullOrWhiteSpace(transcription.Translation)
                    ? "[No translation available]"
                    : transcription.Translation;
                builder.AppendLine(translationText);

                File.WriteAllText(textOutputPath, builder.ToString(), Encoding.UTF8);

                var markdownBuilder = new StringBuilder();
                markdownBuilder.AppendLine($"# Session {baseFileName}");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine("## English Transcript");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(transcription.Text);
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine("## Persian Translation");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(translationText);

                File.WriteAllText(markdownOutputPath, markdownBuilder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to save transcript: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async void btnSelectAudio_Click(object sender, EventArgs e)
        {
            if (_isRecording)
            {
                MessageBox.Show("Stop the current recording before selecting another audio file.", "Recording In Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Audio Files (*.wav;*.mp3;*.m4a;*.aac)|*.wav;*.mp3;*.m4a;*.aac|All Files (*.*)|*.*";
                dialog.Title = "Select audio file for transcription";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _currentRecordingPath = dialog.FileName;
                    try
                    {
                        btnSelectAudio.Enabled = false;
                        btnToggleRecording.Enabled = false;
                        await ProcessRecordingAsync(dialog.FileName);
                    }
                    finally
                    {
                        btnSelectAudio.Enabled = true;
                        btnToggleRecording.Enabled = true;
                    }
                }
            }
        }

        private void UpdateStatus(string message)
        {
            lblStatus.Text = $"Status: {message}";
        }

        private string GetApiKey()
        {
            return txtApiKey.Text.Trim();
        }

        private TranslationEndpointConfiguration ApplyTranslationEndpoint(string endpoint, bool showFeedback)
        {
            var trimmed = (endpoint ?? string.Empty).Trim();

            TranslationEndpointConfiguration result;

            if (string.IsNullOrEmpty(trimmed))
            {
                _translationService = null;

                if (showFeedback)
                {
                    MessageBox.Show(
                        "Translation has been disabled. Provide a translation service URL to enable automatic translation.",
                        "Translation Disabled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                result = TranslationEndpointConfiguration.Disabled;
            }
            else if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
                     (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                _translationService = null;

                if (showFeedback)
                {
                    MessageBox.Show(
                        "The translation service URL must start with http:// or https:// and be a valid absolute URL.",
                        "Invalid Translation URL",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                result = TranslationEndpointConfiguration.Invalid;
            }
            else
            {
                _translationService = new TranslationService(uri);
                result = TranslationEndpointConfiguration.Configured;
            }

            _translationEndpointStatus = result;
            return result;
        }

        private void CleanupRecordingResources()
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
                _waveWriter = null;
            }
        }

        private void btnOpenOutputFolder_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(_transcriptsDirectory))
                {
                    Directory.CreateDirectory(_transcriptsDirectory);
                }

                Process.Start("explorer.exe", _transcriptsDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open folder: {ex.Message}", "Folder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
