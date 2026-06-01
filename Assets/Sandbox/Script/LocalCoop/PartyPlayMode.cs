/// <summary>
/// SandboxOfflineCombined のマルチプレイ方式。
/// </summary>
public enum PartyPlayMode
{
    /// <summary>同一マシン・ローカル Co-op（ゲームパッド後入り/後抜け）。</summary>
    OfflineLocal,

    /// <summary>オンライン（NGO）。接続=後入り、切断=後抜け。不足分は NPC。</summary>
    Online,
}
