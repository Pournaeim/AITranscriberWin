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
            bool isFinalSegment)
        {
            SegmentText = segmentText ?? string.Empty;
            SegmentTranslation = segmentTranslation ?? string.Empty;
            FullTranscript = fullTranscript ?? string.Empty;
            FullTranslation = fullTranslation ?? string.Empty;
            IsFinalSegment = isFinalSegment;
        }

        public string SegmentText { get; }

        public string SegmentTranslation { get; }

        public string FullTranscript { get; }

        public string FullTranslation { get; }

        public bool IsFinalSegment { get; }
    }
}
