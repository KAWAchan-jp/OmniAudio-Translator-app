namespace OmniAudioTranslator.Core.Localization;

/// <summary>
/// アプリ全体(Core/App共通)の日英切り替え用文字列テーブル。UI・状態通知・例外メッセージのユーザー向け文言は
/// すべてここを経由する。<see cref="CurrentLanguage"/>を変更すると<see cref="LanguageChanged"/>が発火し、
/// 開いているウィンドウが表示を更新できる。
/// </summary>
public static class Strings
{
    private static AppLanguage _currentLanguage = AppLanguage.Japanese;

    public static AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value)
            {
                return;
            }

            _currentLanguage = value;
            LanguageChanged?.Invoke();
        }
    }

    public static event Action? LanguageChanged;

    public static string Get(string key) =>
        Table.TryGetValue(key, out (string Ja, string En) entry)
            ? (CurrentLanguage == AppLanguage.English ? entry.En : entry.Ja)
            : key;

    /// <summary>キーの文言を<see cref="string.Format(string, object?[])"/>のテンプレートとして使う。</summary>
    public static string Format(string key, params object?[] args) => string.Format(Get(key), args);

    private static readonly Dictionary<string, (string Ja, string En)> Table = new()
    {
        // TranslationSessionService
        ["session.starting_capture"] = ("音声キャプチャを開始しています...", "Starting audio capture..."),
        ["session.recognizing"] = ("音声を認識中...", "Recognizing audio..."),
        ["session.stopped"] = ("停止しました", "Stopped"),
        ["session.silence_skipped"] = ("無音チャンクをスキップ ({0}%)", "Silent chunk skipped ({0}%)"),
        ["session.chunk_dropped_busy"] = ("前のチャンクを処理中のため、このチャンクは間引きました", "Previous chunk is still processing; this chunk was dropped"),
        ["session.recognizing_chunk"] = ("音声チャンクを認識中 ({0}KB)...", "Recognizing audio chunk ({0}KB)..."),
        ["session.recognizing_chunk_amplified"] = (
            "音声チャンクを認識中 ({0}KB, 入力ピーク{1}%を+{2:0.#}dB増幅)...",
            "Recognizing audio chunk ({0}KB, amplified input peak {1}% by +{2:0.#} dB)..."),
        ["session.empty_result"] = ("認識結果は空でした", "Recognition result was empty"),
        ["session.hallucination_discarded"] = ("ハルシネーションらしい認識結果を破棄しました", "Discarded a likely hallucinated recognition result"),
        ["session.translating"] = ("翻訳中...", "Translating..."),
        ["session.translation_rate_limited"] = ("翻訳がレート制限されました。認識結果のみ表示します", "Translation was rate-limited. Showing recognition result only"),
        ["session.translation_error"] = ("翻訳エラー: {0}。認識結果のみ表示します", "Translation error: {0}. Showing recognition result only"),
        ["session.result_shown"] = ("認識結果を表示しました", "Recognition result shown"),
        ["session.recognition_error"] = ("認識エラー: {0}", "Recognition error: {0}"),
        ["session.translation_identical"] = (
            "訳文が原文と同一のため1行で表示しています（宛先言語が音声の言語と同じ可能性があります）",
            "Translation matched the original, so it is shown on one line (the target language may be the same as the spoken language)"),
        ["session.capture_failed"] = ("音声キャプチャが停止しました: {0}", "Audio capture stopped: {0}"),
        ["session.transcription_rate_limited"] = (
            "音声認識がレート制限されました（Groq 429）。少し待って再試行してください",
            "Speech recognition was rate-limited (Groq 429). Wait a moment and try again"),
        ["session.transcription_error"] = ("音声認識エラー ({0}): {1}", "Recognition error ({0}): {1}"),
        ["session.detail_body"] = ("本文:", "Body:"),

        // Translation providers
        ["translation.google_free_error"] = ("Google翻訳(無料)がエラーを返しました ({0}): {1}", "Google Translate (Free) returned an error ({0}): {1}"),
        ["translation.deepl_error"] = ("DeepLがエラーを返しました ({0}): {1}", "DeepL returned an error ({0}): {1}"),
        ["translation.gemini_error"] = ("Geminiがエラーを返しました ({0}): {1}", "Gemini returned an error ({0}): {1}"),

        // Whisper runtime
        ["whisper.runtime_unknown"] = ("不明", "Unknown"),

        // Transcription providers
        ["transcription_provider.whisper"] = ("内蔵 Whisper（ローカル）", "Built-in Whisper (Local)"),
        ["transcription_provider.groq"] = ("Groq（クラウド）", "Groq (Cloud)"),
        ["transcription.groq_error"] = ("Groqがエラーを返しました ({0}): {1}", "Groq returned an error ({0}): {1}"),
        ["transcription.groq_network_error"] = ("Groqへの通信に失敗しました: {0}", "Failed to communicate with Groq: {0}"),
        ["transcription.groq_invalid_response"] = ("Groqの応答を解析できませんでした: {0}", "Could not parse the Groq response: {0}"),
        ["groq_model.turbo_label"] = ("whisper-large-v3-turbo（高速）", "whisper-large-v3-turbo (Fast)"),
        ["groq_model.large_label"] = ("whisper-large-v3（高精度）", "whisper-large-v3 (Accurate)"),
        ["groq_model.turbo_hint"] = (
            "高速・低コスト（既定）。会話や配信のリアルタイム字幕向け。",
            "Fast and low-cost (default). Best for conversation and live subtitles."),
        ["groq_model.large_hint"] = (
            "高精度だが低速・高コスト。聞き取りにくい音声や専門用語向け。",
            "More accurate but slower and costlier. Best for hard-to-hear audio and technical terms."),

        // MainWindow
        ["mainwindow.title_with_engine"] = ("OmniAudio Translator - 認識エンジン: {0}", "OmniAudio Translator - Recognition Engine: {0}"),
        ["mainwindow.no_window_selected"] = ("対象ウィンドウ: 未選択", "Target window: not selected"),
        ["mainwindow.window_selected"] = ("対象ウィンドウ: [{0}.exe] {1}", "Target window: [{0}.exe] {1}"),
        ["mainwindow.window_select_error"] = ("ウィンドウ選択でエラー: {0}", "Error selecting window: {0}"),
        ["mainwindow.preparing_model"] = ("モデルを準備中（初回は自動ダウンロードのため時間がかかります）...", "Preparing model (first run downloads automatically and may take a while)..."),
        ["mainwindow.start_failed"] = ("開始に失敗しました: {0}", "Failed to start: {0}"),
        ["mainwindow.stopped"] = ("停止しました", "Stopped"),
        ["mainwindow.display_settings_save_failed"] = ("表示設定の保存に失敗しました: {0}", "Failed to save display settings: {0}"),
        ["mainwindow.language_save_failed"] = ("言語設定の保存に失敗しました: {0}", "Failed to save language setting: {0}"),
        ["mainwindow.chunk_interval_save_failed"] = ("チャンク間隔設定の保存に失敗しました: {0}", "Failed to save chunk interval setting: {0}"),
        ["mainwindow.waiting"] = ("待機中", "Waiting"),
        ["mainwindow.translation_key_required"] = (
            "翻訳プロバイダを選択した場合はAPIキーを入力してください（不要なら「なし（認識結果のみ）」または「Google翻訳（無料）」を選んでください）",
            "Please enter an API key when a translation provider is selected (choose \"None (recognition only)\" or \"Google Translate (Free)\" if you don't need one)"),

        ["mainwindow.label_source_language"] = ("元言語（音声）:", "Source language (audio):"),
        ["mainwindow.label_translation"] = ("翻訳:", "Translation:"),
        ["mainwindow.label_target_language"] = ("宛先言語:", "Target language:"),
        ["mainwindow.label_api_key_deepl"] = ("DeepL APIキー:", "DeepL API key:"),
        ["mainwindow.label_api_key_gemini"] = ("Gemini APIキー:", "Gemini API key:"),
        ["mainwindow.label_api_key_groq"] = ("Groq APIキー:", "Groq API key:"),
        ["mainwindow.label_transcription_engine"] = ("認識エンジン:", "Recognition engine:"),
        ["mainwindow.label_groq_model"] = ("Groqモデル:", "Groq model:"),
        ["mainwindow.groq_key_required"] = (
            "Groqを選んだ場合はAPIキーを入力してください（GPU負荷を避けたいときはGroq、オフラインなら内蔵Whisperを選べます）",
            "Please enter the Groq API key when Groq is selected (use Groq to avoid GPU load, built-in Whisper for offline use)"),
        ["mainwindow.groq_preparing"] = ("Groqに接続しています...", "Connecting to Groq..."),
        ["mainwindow.transcription_save_failed"] = ("認識エンジン設定の保存に失敗しました: {0}", "Failed to save recognition engine setting: {0}"),
        ["mainwindow.transcription_locked_running"] = ("認識エンジンは停止後に変更できます", "The recognition engine can be changed after stopping"),
        ["mainwindow.api_key_show"] = ("表示", "Show"),
        ["mainwindow.api_key_load_failed"] = ("APIキーの読み込みに失敗しました: {0}", "Failed to load API key: {0}"),
        ["mainwindow.api_key_save_failed"] = ("APIキーの保存に失敗しました: {0}", "Failed to save API key: {0}"),
        ["mainwindow.label_language"] = ("言語:", "Language:"),
        ["mainwindow.label_chunk_interval"] = ("チャンク間隔:", "Chunk interval:"),
        ["mainwindow.chunk_interval_value"] = ("{0}秒", "{0}s"),
        ["mainwindow.chunk_interval_hint"] = (
            "短くすると字幕の反応は速くなりますが、認識精度が下がったりCPU負荷で間引き（チャンクのスキップ）が増えたりします。長くすると精度は上がりますが反応が遅くなります。",
            "Shorter intervals make subtitles appear faster, but recognition accuracy may drop and more chunks may be skipped under CPU load. Longer intervals improve accuracy at the cost of responsiveness."),

        ["mainwindow.label_vad_threshold"] = ("発話のしきい値:", "Speech threshold:"),
        ["mainwindow.vad_threshold_value"] = ("{0}%", "{0}%"),
        ["mainwindow.vad_threshold_hint"] = (
            "この音量（波形のピーク）未満の音声は無音として捨てます。配信やアプリの音量が小さいと認識されない場合は1〜2%まで下げてください。値を下げると雑音も拾いやすくなりますが、発話は自動で増幅してから認識します。",
            "Audio below this level (waveform peak) is treated as silence and discarded. If nothing is recognized because the stream or app volume is low, lower it to 1-2%. Lower values pick up more noise, but speech is amplified before recognition."),
        ["mainwindow.vad_threshold_save_failed"] = ("発話しきい値の保存に失敗しました: {0}", "Failed to save speech threshold: {0}"),

        ["mainwindow.reset_to_default"] = ("既定値に戻す（{0}）", "Reset to default ({0})"),
        ["mainwindow.reset_chunk_interval_tooltip"] = ("チャンク間隔を既定の6秒に戻します", "Reset the chunk interval to the default of 6 seconds"),
        ["mainwindow.reset_vad_threshold_tooltip"] = ("発話のしきい値を既定の2%に戻します", "Reset the speech threshold to the default of 2%"),

        ["mainwindow.button_select_window"] = ("ウィンドウを選択...", "Select window..."),
        ["mainwindow.button_start"] = ("開始", "Start"),
        ["mainwindow.button_stop"] = ("停止", "Stop"),
        ["mainwindow.button_display_settings"] = ("表示設定...", "Display settings..."),

        ["translation_provider.none"] = ("なし（認識結果のみ）", "None (recognition only)"),
        ["translation_provider.google_free"] = ("Google翻訳（無料）", "Google Translate (Free)"),
        ["translation_provider.deepl"] = ("DeepL", "DeepL"),
        ["translation_provider.gemini"] = ("Gemini (gemini-4-hydrogen, Low)", "Gemini (gemini-4-hydrogen, Low)"),

        // 言語名(元言語/宛先言語セレクター共通)
        ["language.auto"] = ("自動判定", "Auto-detect"),
        ["language.ja"] = ("日本語", "Japanese"),
        ["language.en"] = ("英語", "English"),
        ["language.zh"] = ("中国語", "Chinese"),
        ["language.ko"] = ("韓国語", "Korean"),
        ["language.es"] = ("スペイン語", "Spanish"),
        ["language.fr"] = ("フランス語", "French"),
        ["language.de"] = ("ドイツ語", "German"),
        ["language.ru"] = ("ロシア語", "Russian"),
        ["language.pt"] = ("ポルトガル語", "Portuguese"),
        ["language.it"] = ("イタリア語", "Italian"),

        // DisplaySettingsWindow
        ["display_settings.window_title"] = ("字幕パネルの表示", "Subtitle Panel Display"),
        ["display_settings.description"] = (
            "文字サイズ・色・背景の透過率を変更できます。変更は即座にパネルへ反映されます。",
            "You can change the font size, color, and background transparency. Changes are reflected in the panel immediately."),
        ["display_settings.section_status"] = ("読み取りステータス", "Reading Status"),
        ["display_settings.section_source"] = ("配信音声の文字起こし", "Source Audio Transcription"),
        ["display_settings.section_translated"] = ("翻訳された文", "Translated Text"),
        ["display_settings.show"] = ("表示", "Show"),
        ["display_settings.size_px"] = ("サイズ(px)", "Size (px)"),
        ["display_settings.color"] = ("色", "Color"),
        ["display_settings.opacity_label"] = ("背景の黒四角の不透明度", "Background box opacity"),
        ["display_settings.reset_button"] = ("表示設定を初期値に戻す", "Reset display settings to defaults"),

        // Error detail panel
        ["error_detail.title"] = ("エラー詳細（デバッグ用）", "Error details (debug)"),
        ["error_detail.copy"] = ("クリップボードへコピー", "Copy to clipboard"),
        ["error_detail.clear"] = ("クリア", "Clear"),
        ["error_detail.copied"] = ("コピーしました", "Copied"),

        // WindowPickerDialog
        ["window_picker.title"] = ("キャプチャするウィンドウを選択", "Select a window to capture"),
        ["window_picker.description"] = (
            "音声をキャプチャしたいウィンドウを選んでください。実際のキャプチャはウィンドウの所有プロセス単位で行われるため、同じアプリの別ウィンドウの音声も混ざる場合があります。特にChrome/Edgeなどのブラウザは、同じブラウザプロセス内の複数タブ／ウィンドウの音声を分離できません。",
            "Choose the window whose audio you want to capture. Capture actually happens per owning process, so audio from other windows of the same app may be mixed in. In particular, browsers such as Chrome/Edge cannot separate audio between tabs or windows that share the same browser process."),
        ["window_picker.refresh"] = ("更新", "Refresh"),
        ["window_picker.select"] = ("選択", "Select"),
        ["window_picker.cancel"] = ("キャンセル", "Cancel"),
        ["window_picker.keyword_group"] = ("強調キーワード", "Highlight keywords"),
        ["window_picker.keyword_input_label"] = ("キーワード:", "Keyword:"),
        ["window_picker.keyword_add"] = ("追加", "Add"),
        ["window_picker.keyword_remove"] = ("このキーワードを削除", "Remove this keyword"),
        ["window_picker.keyword_hint"] = (
            "タイトルまたはプロセス名に含まれる言葉を部分一致（大文字小文字を区別しない）で強調します。Enterキーでも追加できます。",
            "Words contained in the title or process name are highlighted as a case-insensitive partial match. Press Enter to add."),
        ["window_picker.keyword_save_failed"] = ("キーワードの保存に失敗しました: {0}", "Failed to save keywords: {0}"),
    };
}
