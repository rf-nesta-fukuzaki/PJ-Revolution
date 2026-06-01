using System.Diagnostics;
using Debug = UnityEngine.Debug;

/// <summary>
/// 開発用トレースログ。<c>SANDBOX_VERBOSE</c> スクリプティングシンボルが定義されているときだけ
/// コンパイルされる。未定義のビルド／通常のエディタ実行では、呼び出し自体（引数の文字列補間を含む）が
/// コンパイラによって完全に除去されるため、文字列生成もログ I/O も一切走らない（ゼロコスト）。
///
/// 毎フレーム級に発火する開発トレース（VoiceChat 妨害強度・RelicSync HP 変化など）を、
/// ビルドのコストを払わずに残すための受け皿。
/// 既存の <see cref="Contract"/> と同じ <see cref="ConditionalAttribute"/> パターン。
///
/// 注意: 警告／エラーは握り潰さないこと。<c>Debug.LogWarning</c>／<c>Debug.LogError</c> を従来どおり使う。
/// 再有効化は Player/Editor の Scripting Define Symbols に <c>SANDBOX_VERBOSE</c> を追加するだけ。
/// </summary>
public static class SandboxLog
{
    [Conditional("SANDBOX_VERBOSE")]
    public static void Trace(string message) => Debug.Log(message);

    [Conditional("SANDBOX_VERBOSE")]
    public static void Trace(string message, UnityEngine.Object context) => Debug.Log(message, context);
}
