using System;

namespace AITranscriberWinApp.Services
{
    public class RealtimeTranscriptionUpdatedEventArgs : EventArgs
    {
        public RealtimeTranscriptionUpdatedEventArgs(
            string segmentText,
            string segmentTranslation,
            string fullTranscript,
            string fullTranslation,
            string translationError,
            bool isFinalSegment)
        {
            SegmentText = segmentText ?? string.Empty;
            SegmentTranslation = segmentTranslation ?? string.Empty;
            FullTranscript = fullTranscript ?? string.Empty;
            FullTranslation = fullTranslation ?? string.Empty;
            TranslationError = translationError ?? string.Empty;
            IsFinalSegment = isFinalSegment;
        }

        public string SegmentText { get; }

        public string SegmentTranslation { get; }

        public string FullTranscript { get; }

        public string FullTranslation { get; }

        public string TranslationError { get; }

        public bool IsFinalSegment { get; }
    }
}
