namespace OmniAudioTranslator.Core.Transcription;

/// <summary>音声認識プロバイダの抽象。WAVストリームを受け取り認識結果を返す。</summary>
public interface ITranscriptionProvider
{
    Task<TranscriptionResult> TranscribeAsync(Stream wavStream, string language = "auto", CancellationToken cancellationToken = default);
}
