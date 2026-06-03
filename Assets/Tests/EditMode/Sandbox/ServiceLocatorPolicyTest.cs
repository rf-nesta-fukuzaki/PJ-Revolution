using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 横断ガードレール: 新規 Singleton（static Instance）の増殖を Test Runner で凍結する「ラチェット」。
/// 横断サービスは GameServices 経由に一本化する方針。詳細は Assets/Doc/ServiceLocatorPolicy.md。
/// </summary>
public sealed class ServiceLocatorPolicyTest
{
    /// <summary>
    /// Assets/Sandbox/Script 配下で許容する <c>static &lt;Type&gt; Instance</c> 宣言の上限。
    /// 既存を GameServices へ移行して減らしたらこの値も下げる（ラチェットを締める）。
    /// やむを得ず増やす場合のみ上げて、理由を PR に明記すること。
    /// </summary>
    private const int SingletonBaseline = 33;

    // Bash 計測（Assets/Doc/ServiceLocatorPolicy.md）と同一の宣言パターン。
    private static readonly Regex SingletonDecl =
        new Regex(@"static\s+[A-Za-z_][A-Za-z0-9_<>.]*\s+Instance\s*(=>|[{;=])", RegexOptions.Compiled);

    [Test]
    public void NoNewSingletonsBeyondBaseline()
    {
        string scriptRoot = Path.Combine(Application.dataPath, "Sandbox", "Script");
        Assert.That(Directory.Exists(scriptRoot), Is.True, $"スクリプト root が見つからない: {scriptRoot}");

        var declaringFiles = Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories)
            .Select(f => (name: Path.GetFileNameWithoutExtension(f), count: SingletonDecl.Matches(File.ReadAllText(f)).Count))
            .Where(x => x.count > 0)
            .OrderBy(x => x.name)
            .ToList();

        int total = declaringFiles.Sum(x => x.count);

        Assert.That(total, Is.LessThanOrEqualTo(SingletonBaseline),
            $"新規 Singleton (static Instance) を検出: {total} 個（上限 {SingletonBaseline}）。\n" +
            "横断サービスは GameServices 経由に一本化してください（Assets/Doc/ServiceLocatorPolicy.md 参照）。\n" +
            "どうしても増やす場合は SingletonBaseline を更新し、理由を PR に明記すること。\n" +
            "現在 static Instance を宣言している型:\n  " +
            string.Join("\n  ", declaringFiles.Select(x => x.name)));
    }
}
