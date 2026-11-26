# iPhone実機で起動するまで

M9を含む `rebuild/ios-device-build` ブランチにiPhone用の設定とXcode出力メニューを追加した。ゲーム本体・Android用APKの内容は変更していない。

**現在は準備段階。Windowsで設定の適用・Editor再コンパイル・Boot起動を確認済み（エラー0・警告0、Coreテスト548件成功）。iOS Build SupportがこのPCにないためXcodeプロジェクトの出力、Macでのコンパイル・署名、iPhone起動は未確認。** 学校のMacが手元にあるとき、下の手順で続ける。

## Macに用意するもの

- Unity Hubと **Unity 6000.3.21f1**。このUnityへ **iOS Build Support** を追加する。Mac上でAndroidをビルドしないならAndroidモジュールは不要。
- Xcode。Unity 6.3の条件はXcode16以降、iOS15以降。接続するiPhoneのiOSを扱えるXcodeを使う（MacのmacOSによって入れられるXcodeが異なる）。[Unity 6.3の条件](https://docs.unity.com/en-us/engine/6000.3/manual/get-started/install-and-upgrade/getting-started-installing-unity/system-requirements)
- iPhone、接続ケーブル、Apple Account。自分の端末の動作テストは無料アカウントで行える。[Appleの案内](https://developer.apple.com/help/account/membership/programs-overview/)

## プロジェクトをMacへ持っていく

GitHub Desktopなどで `Yukura-dev/Imoni-kai` をcloneし、`rebuild/ios-device-build`を選択する。画像・音声はGit LFSで管理されているので、実ファイルまで取得する。ターミナルを使う場合は、Git LFS導入後に次を実行する。

```sh
git lfs install
git clone --branch rebuild/ios-device-build https://github.com/Yukura-dev/Imoni-kai.git
cd Imoni-kai
git lfs pull
```

Unity Hubからcloneしたフォルダを開く。`Assets` / `Packages` / `ProjectSettings`が同じ階層にあるフォルダが正本。Windowsの`Library`をMacへコピーする必要はない。

## UnityからXcodeへ出力

1. Unityのインポート・コンパイルを待ち、Play Modeを終了する。
2. **Imoni > Setup Project** を実行する。
3. **File > Build Profiles > iOS > Switch Platform** でiOSへ切り替える。iOSが無ければUnity HubでiOS Build Supportを追加する。
4. 再インポート・コンパイルが完了したら、**Imoni > Build > iPhone Xcode Project** を実行する。
5. `Builds/iOS/Unity-iPhone.xcodeproj` ができる。出力結果は`Library/iOS-export.json`。この時点では、まだ署名済みのiPhoneアプリではない。

出力先は再生成用。Xcodeで行った追加変更を残す場合は、再出力前に別途保存する。

設定元は `Assets/Imoni/Config/GameConfig.json` の `setup.ios`。Bundle ID `jp.yukura.imoni`、build番号5、iOS15.0以上、iPhone実機、IL2CPP、Metal、縦固定、自動署名を設定する。Apple Teamはコードで上書きしない。共通アプリアイコンには、ホームと同じ新しい鍋を使用する。

各プラットフォームのGraphics APIは、そのBuild Supportが入っているEditorでのみ適用する。WindowsにiOSモジュールがなくてもSetupは実行でき、MacでのSetup時にMetalを明示設定する。

## XcodeからiPhoneへ入れる

1. Xcodeを開き、初回に求められるiOS用コンポーネントのインストールを完了する。
2. **Xcode > Settings > Apple Accounts** で自分のApple Accountへサインインする。
3. `Unity-iPhone.xcodeproj`を開き、`Unity-iPhone`ターゲットの **Signing & Capabilities** で **Automatically manage signing** を有効にし、**Team**を自分のPersonal Teamまたは所属する開発チームにする。
4. Bundle Identifierが使用済みと表示された場合は、`GameConfig.setup.ios.applicationIdentifier`を自分の一意なIDへ変更して再出力する。
5. iPhoneを接続する。iPhone側の「このコンピュータを信頼」を本人が確認し、Xcodeから求められた場合はiPhoneの開発者モードを本人が有効にする。
6. Xcode上部の実行先に接続したiPhoneを選び、**Run（▶）** を押す。コンパイル・署名・インストール後にゲームが開く。

詳細：[Apple 実機での実行手順](https://developer.apple.com/documentation/Xcode/running-your-app-on-simulated-or-physical-devices)、[UnityのiOSビルド環境](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/iphone/getting-started/ios-environment-setup)。

## 最初の実機確認

- タイトル→ホーム→ロビー→Loading→Gameへ進む。
- ノッチ付近のHUDと下ボタンが欠けず、資源をタップして移動・採集できる。
- 遊び方を開閉して元のゲームへ戻れる。
- BGMのイントロ→ループ、設定のBGM/効果音、演出中のBGM停止を確認する。
- ホーム画面へ一度退避して戻り、音と操作が復帰するか確認する。

この実機確認が済むまでは「iPhoneで起動確認済み」としない。今回のWindows側の確認結果は `validation/ios-preparation/` に保存する。
