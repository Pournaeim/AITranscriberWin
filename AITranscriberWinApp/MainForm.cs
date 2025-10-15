using System;
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
        private readonly OpenAiTranscriptionService _transcriptionService = new OpenAiTranscriptionService();
        private readonly TranslationService _translationService = new TranslationService();
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
            UpdateStatus("Ready.");
        }

        private void btnSaveKey_Click(object sender, EventArgs e)
        {
            Settings.Default.OpenAIApiKey = txtApiKey.Text.Trim();
            Settings.Default.Save();
            MessageBox.Show("API key saved locally for this user.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _waveIn = new WaveInEvent();
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
                UpdateStatus("Uploading to Whisper...");
                var transcription = await _transcriptionService.TranscribeAsync(audioPath, apiKey, token);

                UpdateStatus("Translating to Persian...");
                try
                {
                    var translation = await _translationService.TranslateToPersianAsync(transcription.Text, token);
                    transcription.Translation = translation;
                }
                catch (Exception translateError)
                {
                    transcription.Translation = string.Empty;
                    MessageBox.Show($"Translation failed: {translateError.Message}", "Translation Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                txtTranscript.Text = transcription.Text;
                txtTranslation.Text = transcription.Translation;

                SaveTranscript(audioPath, transcription);
                UpdateStatus(string.IsNullOrEmpty(transcription.Translation)
                    ? "Completed (translation unavailable)."
                    : "Completed.");
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
                builder.AppendLine(transcription.Translation);

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
                markdownBuilder.AppendLine(transcription.Translation);

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
