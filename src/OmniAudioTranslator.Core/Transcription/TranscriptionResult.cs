namespace OmniAudioTranslator.Core.Transcription;

public sealed record TranscriptionSegment(TimeSpan Start, TimeSpan End, string Text);

public sealed record TranscriptionResult(string Text, string? Language, IReadOnlyList<TranscriptionSegment> Segments);
