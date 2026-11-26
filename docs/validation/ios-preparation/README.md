# iPhone出力の準備確認（2026-11-26）

M9の1b06815を基点に、iOS向け設定・Xcode出力メニューとMacでの実機インストール手順を追加。

## 確認したこと

- `dotnet test tools/CoreTests --logger 'trx;LogFileName=ios-preparation.trx' --results-directory Library/iOS-tests`：548件成功、失敗0、スキップ0。`dotnet-test.trx`。
- Windows / Unity 6000.3.21f1でEditor再コンパイル後にSetupを実行。最終Consoleはエラー0・警告0。`console.json`。
- `ios-settings.json`：iPhoneOnly、iOS15.0、IL2CPP、Metal、縦画面、自動署名、Bundle IDとbuild番号をEditor APIで読み戻した。
- `editor-boot.json`：Windows Editorの390×844でBoot→Main起動、画面内のボタン、Consoleエラー0・警告0。実機テストではない。

## 初回の警告と修正

最初のSetupはiOS Build Supportなしで`SetGraphicsAPIs(iOS, Metal)`を呼び、Unityが候補を除外して`Trying to set empty graphics device types list for platform iOSSupport; setting default list instead`を1件出した。`console-first-attempt.json`に件数を保存。

Graphics APIの設定を対象Build Supportがある場合だけに限定した。Android側も同様にして、iOS専用MacにAndroidモジュールを要求しない。修正後にConsoleをクリアし、再コンパイル・Setup・Boot起動で再確認した。

## 未確認

- WindowsのiOS Build Supportは未導入、学校のMacは現在利用できない。
- Xcodeプロジェクト出力、iOS/Metal向けコンパイル、署名、iPhoneへのインストール・起動は未実施。IPAは生成していない。
- MacでAndroidモジュールを入れない構成の実行は未確認。EditorコードはAndroid専用DLLをSetup経路で直接参照しない構造。
- iPhoneのノッチ・タッチ・音・バックグラウンド復帰は実機で確認する。

続きは[Macでの手順](../../IOS_DEVICE_SETUP.md)。
