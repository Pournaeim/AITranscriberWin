using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Utils;
using NAudio.Wave;

namespace AITranscriberWinApp.Services
{
    public sealed class RealtimeTranscriptionManager : IDisposable
    {
        private const int DefaultChunkSeconds = 5;

        private readonly OpenAiTranscriptionService _transcriptionService;
        private readonly Func<string> _apiKeyProvider;
        private readonly WaveFormat _waveFormat;
        private readonly int _bytesPerChunk;
        private readonly List<byte> _buffer = new List<byte>();
        private readonly object _bufferLock = new object();
        private readonly object _processingLock = new object();
        private readonly object _aggregationLock = new object();
        private readonly StringBuilder _transcriptBuilder = new StringBuilder();
        private readonly StringBuilder _translationBuilder = new StringBuilder();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Task _processingChain = Task.CompletedTask;
        private bool _disposed;

        public RealtimeTranscriptionManager(
            OpenAiTranscriptionService transcriptionService,
            Func<string> apiKeyProvider,
            WaveFormat waveFormat,
            TimeSpan? chunkDuration = null)
        {
            _transcriptionService = transcriptionService ?? throw new ArgumentNullException(nameof(transcriptionService));
            _apiKeyProvider = apiKeyProvider ?? throw new ArgumentNullException(nameof(apiKeyProvider));
            _waveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));

            var seconds = Math.Max(1, (int)Math.Round((chunkDuration ?? TimeSpan.FromSeconds(DefaultChunkSeconds)).TotalSeconds));
            _bytesPerChunk = Math.Max(_waveFormat.AverageBytesPerSecond * seconds, _waveFormat.BlockAlign);
        }

        public event EventHandler<RealtimeTranscriptionUpdatedEventArgs> TranscriptionUpdated;

        public event EventHandler<Exception> TranscriptionFailed;

        public void AddAudio(byte[] buffer, int bytesRecorded)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (bytesRecorded <= 0 || _cts.IsCancellationRequested)
            {
                return;
            }

            lock (_bufferLock)
            {
                for (var i = 0; i < bytesRecorded; i++)
                {
                    _buffer.Add(buffer[i]);
                }
            }

            TryStartProcessing(force: false);
        }

        public async Task<TranscriptionResult> CompleteAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            TryStartProcessing(force: true);

            Task processingTask;

            lock (_processingLock)
            {
                processingTask = _processingChain;
            }

            using (cancellationToken.Register(() => _cts.Cancel()))
            {
                try
                {
                    await processingTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested || cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
            }

            lock (_aggregationLock)
            {
                var fullTranscript = _transcriptBuilder.ToString().Trim();
                var fullTranslation = _translationBuilder.ToString().Trim();

                return new TranscriptionResult
                {
                    Text = fullTranscript,
                    Translation = fullTranslation
                };
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
        }

        private void TryStartProcessing(bool force)
        {
            while (true)
            {
                byte[] chunk;
                bool isFinal;

                lock (_bufferLock)
                {
                    if (_buffer.Count >= _bytesPerChunk)
                    {
                        chunk = _buffer.GetRange(0, _bytesPerChunk).ToArray();
                        _buffer.RemoveRange(0, _bytesPerChunk);
                        isFinal = force && _buffer.Count == 0;
                    }
                    else if (force && _buffer.Count > 0)
                    {
                        chunk = _buffer.ToArray();
                        _buffer.Clear();
                        isFinal = true;
                    }
                    else
                    {
                        break;
                    }
                }

                QueueChunk(chunk, isFinal);
            }
        }

        private void QueueChunk(byte[] chunk, bool isFinalSegment)
        {
            if (chunk == null || chunk.Length == 0)
            {
                return;
            }

            lock (_processingLock)
            {
                _processingChain = _processingChain.ContinueWith(
                        async _ => await ProcessChunkAsync(chunk, isFinalSegment, _cts.Token).ConfigureAwait(false),
                        CancellationToken.None,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }

        private async Task ProcessChunkAsync(byte[] chunk, bool isFinalSegment, CancellationToken token)
        {
            if (chunk == null || chunk.Length == 0)
            {
                return;
            }

            token.ThrowIfCancellationRequested();

            var apiKey = _apiKeyProvider();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return;
            }

            try
            {
                using (var chunkStream = new MemoryStream())
                {
                    using (var writer = new WaveFileWriter(new IgnoreDisposeStream(chunkStream), _waveFormat))
                    {
                        writer.Write(chunk, 0, chunk.Length);
                    }

                    chunkStream.Position = 0;
                    var transcription = await _transcriptionService.TranscribeAsync(chunkStream, "realtime.wav", apiKey, token).ConfigureAwait(false);
                    var segmentText = transcription.Text ?? string.Empty;
                    var segmentTranslation = transcription.Translation ?? string.Empty;

                    AppendAndNotify(segmentText, segmentTranslation, isFinalSegment);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnTranscriptionError(ex);
            }
        }

        private void AppendAndNotify(string segmentText, string segmentTranslation, bool isFinalSegment)
        {
            string fullTranscript;
            string fullTranslation;

            lock (_aggregationLock)
            {
                if (!string.IsNullOrWhiteSpace(segmentText))
                {
                    AppendWithSpace(_transcriptBuilder, segmentText);
                }

                if (!string.IsNullOrWhiteSpace(segmentTranslation))
                {
                    AppendWithSpace(_translationBuilder, segmentTranslation);
                }

                fullTranscript = _transcriptBuilder.ToString();
                fullTranslation = _translationBuilder.ToString();
            }

            TranscriptionUpdated?.Invoke(
                this,
                new RealtimeTranscriptionUpdatedEventArgs(
                    segmentText,
                    segmentTranslation,
                    fullTranscript,
                    fullTranslation,
                    isFinalSegment));
        }

        private static void AppendWithSpace(StringBuilder builder, string value)
        {
            if (builder.Length > 0 && !char.IsWhiteSpace(builder[builder.Length - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(value.Trim());
        }

        private void OnTranscriptionError(Exception exception)
        {
            TranscriptionFailed?.Invoke(this, exception);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RealtimeTranscriptionManager));
            }
        }
    }
}
